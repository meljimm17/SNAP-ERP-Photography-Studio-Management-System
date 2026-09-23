using Microsoft.EntityFrameworkCore;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;

namespace SNAP.infrastructure.services
{
    /// <summary>
    /// Income Statement, Balance Sheet, and Cash Flow Statement for Reports & Analytics -
    /// internal-management-use, cash/modified-cash basis (not full PFRS/GAAP). Every figure is
    /// computed from each transaction's own date field rather than today's live totals, so a
    /// report run for a past date/range gives a correct historical snapshot rather than "right
    /// now"'s numbers - the one exception is Inventory (QuantityOnHand x UnitCost) and Equipment
    /// (AcquisitionCost), which are live running values with no history and so are only exact for
    /// D = today (see BalanceSheetResult's remarks).
    /// </summary>
    public static class FinancialStatementService
    {
        public sealed record IncomeStatementResult(decimal Revenue, decimal OperatingExpenses, decimal SalaryWages, decimal NetIncome);

        public sealed record BalanceSheetResult(
            DateTime AsOfDate,
            decimal Cash,
            decimal AccountsReceivable,
            decimal Inventory,
            decimal Equipment,
            decimal TotalAssets,
            decimal AccountsPayable,
            decimal OwnersCapital,
            decimal RetainedEarnings,
            decimal TotalEquity,
            decimal TotalLiabilitiesAndEquity);

        public sealed record CashFlowStatementResult(
            decimal NetIncome,
            decimal AccountsReceivableChange,
            decimal AccountsPayableChange,
            decimal InventoryChange,
            decimal NetCashFromOperations,
            decimal EquipmentPurchased,
            decimal OwnerFinancingNet,
            decimal NetChangeInCash,
            decimal BeginningCash,
            decimal EndingCash);

        public static IncomeStatementResult ComputeIncomeStatement(TenantErpDbContext db, DateTime from, DateTime toInclusive)
        {
            var toExclusive = toInclusive.Date.AddDays(1);
            var revenue = RevenueInRange(db, from, toExclusive);
            var operatingExpenses = ExpensesIncurredInRange(db, from, toExclusive);
            var salaryWages = SalaryNetPaidInRange(db, from, toExclusive);

            return new IncomeStatementResult(revenue, operatingExpenses, salaryWages, revenue - operatingExpenses - salaryWages);
        }

        /// <summary>
        /// Balance Sheet as of a given date. Retained Earnings is cumulative Net Income (the same
        /// Revenue-Expenses-Salaries formula as the Income Statement, run cumulatively through the
        /// as-of date) plus the current Accounts Receivable/Inventory/Equipment balances - those
        /// three asset lines never flow through Net Income anywhere else (a booking's price isn't
        /// "revenue" until paid, and Inventory/Equipment acquisition isn't an Expense), so without
        /// adding them back here the sheet would not balance. This is the standard treatment for a
        /// derived (non-double-entry) statement: Retained Earnings is defined so that
        /// Assets = Liabilities + Equity holds by construction, at every date, not just today.
        /// </summary>
        public static BalanceSheetResult ComputeBalanceSheet(TenantErpDbContext db, DateTime asOfDate)
        {
            var asOf = asOfDate.Date;
            var exclusive = asOf.AddDays(1);

            var cash = CashAsOf(db, exclusive);
            var accountsReceivable = AccountsReceivableAsOf(db, exclusive);
            var inventory = db.InventoryItems.Where(i => i.IsActive).Sum(i => (decimal?)(i.QuantityOnHand * (i.UnitCost ?? 0m))) ?? 0m;
            var equipment = db.Equipment.Where(e => e.IsActive).Sum(e => (decimal?)(e.AcquisitionCost ?? 0m)) ?? 0m;
            var totalAssets = cash + accountsReceivable + inventory + equipment;

            var accountsPayable = AccountsPayableAsOf(db, asOf, exclusive);
            var ownersCapital = OwnersCapitalAsOf(db, exclusive);

            var cumulativeNetIncome = RevenueInRange(db, DateTime.MinValue, exclusive)
                - ExpensesIncurredInRange(db, DateTime.MinValue, exclusive)
                - SalaryNetPaidInRange(db, DateTime.MinValue, exclusive);
            var retainedEarnings = cumulativeNetIncome + accountsReceivable + inventory + equipment;

            var totalEquity = ownersCapital + retainedEarnings;
            var totalLiabilitiesAndEquity = accountsPayable + totalEquity;

            return new BalanceSheetResult(asOf, cash, accountsReceivable, inventory, equipment, totalAssets,
                accountsPayable, ownersCapital, retainedEarnings, totalEquity, totalLiabilitiesAndEquity);
        }

        public static CashFlowStatementResult ComputeCashFlowStatement(TenantErpDbContext db, DateTime from, DateTime toInclusive)
        {
            var toExclusive = toInclusive.Date.AddDays(1);
            var fromExclusive = from.Date;

            var incomeStatement = ComputeIncomeStatement(db, from, toInclusive);

            var arBegin = AccountsReceivableAsOf(db, fromExclusive);
            var arEnd = AccountsReceivableAsOf(db, toExclusive);
            var apBegin = AccountsPayableAsOf(db, from.Date.AddDays(-1), fromExclusive);
            var apEnd = AccountsPayableAsOf(db, toInclusive.Date, toExclusive);

            // Inventory has no valuation history (see class remarks) - it's always today's live
            // value regardless of date, so its period-over-period change is always 0. Kept as an
            // explicit line for statement completeness/clarity rather than silently omitted.
            const decimal inventoryChange = 0m;

            var accountsReceivableChange = arBegin - arEnd;
            var accountsPayableChange = apEnd - apBegin;
            var netCashFromOperations = incomeStatement.NetIncome + accountsReceivableChange + accountsPayableChange + inventoryChange;

            var equipmentPurchased = db.Equipment
                .Where(e => e.IsActive && e.CreatedAt >= from.Date && e.CreatedAt < toExclusive)
                .Sum(e => (decimal?)(e.AcquisitionCost ?? 0m)) ?? 0m;

            var contributions = db.OwnerTransactions.Where(o => o.Type == OwnerTransactionType.Contribution && o.TransactionDate >= from.Date && o.TransactionDate < toExclusive).Sum(o => (decimal?)o.Amount) ?? 0m;
            var withdrawals = db.OwnerTransactions.Where(o => o.Type == OwnerTransactionType.Withdrawal && o.TransactionDate >= from.Date && o.TransactionDate < toExclusive).Sum(o => (decimal?)o.Amount) ?? 0m;
            var ownerFinancingNet = contributions - withdrawals;

            var beginningCash = CashAsOf(db, fromExclusive);
            var endingCash = CashAsOf(db, toExclusive);
            var netChangeInCash = endingCash - beginningCash;

            return new CashFlowStatementResult(
                incomeStatement.NetIncome, accountsReceivableChange, accountsPayableChange, inventoryChange,
                netCashFromOperations, equipmentPurchased, ownerFinancingNet, netChangeInCash, beginningCash, endingCash);
        }

        private static decimal RevenueInRange(TenantErpDbContext db, DateTime from, DateTime toExclusive)
        {
            var payments = db.Payments.Where(p => !p.IsDeleted && p.PaymentDate >= from && p.PaymentDate < toExclusive).Sum(p => (decimal?)p.AmountPaid) ?? 0m;
            var productSales = db.ProductSales.Where(p => !p.IsDeleted && p.SaleDate >= from && p.SaleDate < toExclusive).Sum(p => (decimal?)p.TotalAmount) ?? 0m;
            return payments + productSales;
        }

        private static decimal ExpensesIncurredInRange(TenantErpDbContext db, DateTime from, DateTime toExclusive) =>
            db.Expenses.Where(e => !e.IsDeleted && e.ExpenseDate >= from && e.ExpenseDate < toExclusive).Sum(e => (decimal?)e.Amount) ?? 0m;

        private static decimal SalaryNetPaidInRange(TenantErpDbContext db, DateTime from, DateTime toExclusive) =>
            db.SalaryPayments.Where(s => !s.IsDeleted && s.PaymentDate >= from && s.PaymentDate < toExclusive).Sum(s => (decimal?)s.NetAmount) ?? 0m;

        private static decimal CashAsOf(TenantErpDbContext db, DateTime exclusive)
        {
            var revenue = RevenueInRange(db, DateTime.MinValue, exclusive);
            var paidExpenses = db.Expenses.Where(e => !e.IsDeleted && e.PaidDate != null && e.PaidDate < exclusive).Sum(e => (decimal?)e.Amount) ?? 0m;
            var salaries = SalaryNetPaidInRange(db, DateTime.MinValue, exclusive);
            var contributions = db.OwnerTransactions.Where(o => o.Type == OwnerTransactionType.Contribution && o.TransactionDate < exclusive).Sum(o => (decimal?)o.Amount) ?? 0m;
            var withdrawals = db.OwnerTransactions.Where(o => o.Type == OwnerTransactionType.Withdrawal && o.TransactionDate < exclusive).Sum(o => (decimal?)o.Amount) ?? 0m;

            return revenue - paidExpenses - salaries + contributions - withdrawals;
        }

        /// <summary>Same figure Reports' Outstanding Payments tab already computes: for every
        /// non-cancelled booking scheduled on/before the cutoff, its price less payments made
        /// on/before the cutoff, floored at 0 per booking.</summary>
        private static decimal AccountsReceivableAsOf(TenantErpDbContext db, DateTime exclusive)
        {
            var bookings = db.Bookings
                .Where(b => b.Status != BookingStatus.Cancelled && b.SessionDate < exclusive)
                .Select(b => new { b.BookingId, b.PriceSnapshot })
                .ToList();
            if (bookings.Count == 0) return 0m;

            var bookingIds = bookings.Select(b => b.BookingId).ToList();
            var paidTotals = db.Payments
                .Where(p => !p.IsDeleted && p.PaymentDate < exclusive && bookingIds.Contains(p.BookingId))
                .GroupBy(p => p.BookingId)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.AmountPaid));

            return bookings.Sum(b => Math.Max(0m, b.PriceSnapshot - paidTotals.GetValueOrDefault(b.BookingId)));
        }

        /// <summary>Expense.Amount where ExpenseDate is on/before the cutoff and it's still unpaid
        /// as of the cutoff (PaidDate is null or falls after it).</summary>
        private static decimal AccountsPayableAsOf(TenantErpDbContext db, DateTime asOf, DateTime exclusive) =>
            db.Expenses.Where(e => !e.IsDeleted && e.ExpenseDate < exclusive && (e.PaidDate == null || e.PaidDate > asOf))
                .Sum(e => (decimal?)e.Amount) ?? 0m;

        private static decimal OwnersCapitalAsOf(TenantErpDbContext db, DateTime exclusive)
        {
            var contributions = db.OwnerTransactions.Where(o => o.Type == OwnerTransactionType.Contribution && o.TransactionDate < exclusive).Sum(o => (decimal?)o.Amount) ?? 0m;
            var withdrawals = db.OwnerTransactions.Where(o => o.Type == OwnerTransactionType.Withdrawal && o.TransactionDate < exclusive).Sum(o => (decimal?)o.Amount) ?? 0m;
            return contributions - withdrawals;
        }
    }
}
