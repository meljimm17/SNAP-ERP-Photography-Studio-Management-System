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
    ///
    /// Every entry point takes a branchId: null means "All Branches" - no filter, the consolidated
    /// company statement, which is byte-for-byte what these methods produced before branches
    /// existed. A non-null branchId narrows every branch-scoped figure (Payments via their
    /// Booking, Product Sales, Expenses, Bookings, Inventory, Equipment) to that one branch.
    ///
    /// Salaries and Owner Transactions are company-wide by design (they carry no BranchId) and so
    /// are excluded entirely from a single-branch statement rather than repeated in each one.
    /// That's what makes the branch views sum correctly: adding every branch's figures back
    /// together plus the company-level payroll and owner capital reconstructs the consolidated
    /// statement, where charging each branch the full company payroll would overcount it N times.
    /// The accounting identity holds either way - those terms cancel on both sides of the sheet.
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

        public static IncomeStatementResult ComputeIncomeStatement(TenantErpDbContext db, DateTime from, DateTime toInclusive, int? branchId = null)
        {
            var toExclusive = toInclusive.Date.AddDays(1);
            var revenue = RevenueInRange(db, from, toExclusive, branchId);
            var operatingExpenses = ExpensesIncurredInRange(db, from, toExclusive, branchId);
            var salaryWages = SalaryNetPaidInRange(db, from, toExclusive, branchId);

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
        public static BalanceSheetResult ComputeBalanceSheet(TenantErpDbContext db, DateTime asOfDate, int? branchId = null)
        {
            var asOf = asOfDate.Date;
            var exclusive = asOf.AddDays(1);

            var cash = CashAsOf(db, exclusive, branchId);
            var accountsReceivable = AccountsReceivableAsOf(db, exclusive, branchId);
            var inventory = db.InventoryItems.Where(i => i.IsActive && (branchId == null || i.BranchId == branchId)).Sum(i => (decimal?)(i.QuantityOnHand * (i.UnitCost ?? 0m))) ?? 0m;
            var equipment = db.Equipment.Where(e => e.IsActive && (branchId == null || e.BranchId == branchId)).Sum(e => (decimal?)(e.AcquisitionCost ?? 0m)) ?? 0m;
            var totalAssets = cash + accountsReceivable + inventory + equipment;

            var accountsPayable = AccountsPayableAsOf(db, asOf, exclusive, branchId);
            var ownersCapital = OwnersCapitalAsOf(db, exclusive, branchId);

            var cumulativeNetIncome = RevenueInRange(db, DateTime.MinValue, exclusive, branchId)
                - ExpensesIncurredInRange(db, DateTime.MinValue, exclusive, branchId)
                - SalaryNetPaidInRange(db, DateTime.MinValue, exclusive, branchId);
            var retainedEarnings = cumulativeNetIncome + accountsReceivable + inventory + equipment;

            var totalEquity = ownersCapital + retainedEarnings;
            var totalLiabilitiesAndEquity = accountsPayable + totalEquity;

            return new BalanceSheetResult(asOf, cash, accountsReceivable, inventory, equipment, totalAssets,
                accountsPayable, ownersCapital, retainedEarnings, totalEquity, totalLiabilitiesAndEquity);
        }

        public static CashFlowStatementResult ComputeCashFlowStatement(TenantErpDbContext db, DateTime from, DateTime toInclusive, int? branchId = null)
        {
            var toExclusive = toInclusive.Date.AddDays(1);
            var fromExclusive = from.Date;

            var incomeStatement = ComputeIncomeStatement(db, from, toInclusive, branchId);

            var arBegin = AccountsReceivableAsOf(db, fromExclusive, branchId);
            var arEnd = AccountsReceivableAsOf(db, toExclusive, branchId);
            var apBegin = AccountsPayableAsOf(db, from.Date.AddDays(-1), fromExclusive, branchId);
            var apEnd = AccountsPayableAsOf(db, toInclusive.Date, toExclusive, branchId);

            // Inventory has no valuation history (see class remarks) - it's always today's live
            // value regardless of date, so its period-over-period change is always 0. Kept as an
            // explicit line for statement completeness/clarity rather than silently omitted.
            const decimal inventoryChange = 0m;

            var accountsReceivableChange = arBegin - arEnd;
            var accountsPayableChange = apEnd - apBegin;
            var netCashFromOperations = incomeStatement.NetIncome + accountsReceivableChange + accountsPayableChange + inventoryChange;

            var equipmentPurchased = db.Equipment
                .Where(e => e.IsActive && e.CreatedAt >= from.Date && e.CreatedAt < toExclusive && (branchId == null || e.BranchId == branchId))
                .Sum(e => (decimal?)(e.AcquisitionCost ?? 0m)) ?? 0m;

            // Owner capital is raised by the company, not by a branch - a single-branch cash flow
            // shows no financing activity rather than claiming the whole company's.
            var ownerFinancingNet = branchId == null
                ? OwnerNetInRange(db, from.Date, toExclusive)
                : 0m;

            var beginningCash = CashAsOf(db, fromExclusive, branchId);
            var endingCash = CashAsOf(db, toExclusive, branchId);
            var netChangeInCash = endingCash - beginningCash;

            return new CashFlowStatementResult(
                incomeStatement.NetIncome, accountsReceivableChange, accountsPayableChange, inventoryChange,
                netCashFromOperations, equipmentPurchased, ownerFinancingNet, netChangeInCash, beginningCash, endingCash);
        }

        /// <summary>
        /// A Payment carries no BranchId of its own - it belongs to whichever branch its Booking
        /// was made at, which is how the Branch Comparison chart attributes revenue too.
        /// </summary>
        private static decimal RevenueInRange(TenantErpDbContext db, DateTime from, DateTime toExclusive, int? branchId)
        {
            var payments = db.Payments
                .Where(p => !p.IsDeleted && p.PaymentDate >= from && p.PaymentDate < toExclusive
                            && (branchId == null || db.Bookings.Any(b => b.BookingId == p.BookingId && b.BranchId == branchId)))
                .Sum(p => (decimal?)p.AmountPaid) ?? 0m;
            // Booking add-ons are billed into the booking's amount due and collected through its
            // Payments, so only walk-in sales (no BookingId) count here - otherwise add-on money
            // would be income twice.
            var productSales = db.ProductSales
                .Where(p => !p.IsDeleted && p.BookingId == null && p.SaleDate >= from && p.SaleDate < toExclusive && (branchId == null || p.BranchId == branchId))
                .Sum(p => (decimal?)p.TotalAmount) ?? 0m;
            return payments + productSales;
        }

        private static decimal ExpensesIncurredInRange(TenantErpDbContext db, DateTime from, DateTime toExclusive, int? branchId) =>
            db.Expenses.Where(e => !e.IsDeleted && e.ExpenseDate >= from && e.ExpenseDate < toExclusive && (branchId == null || e.BranchId == branchId)).Sum(e => (decimal?)e.Amount) ?? 0m;

        /// <summary>
        /// Payroll is company-wide (SalaryPayment carries no BranchId), so a single-branch
        /// statement reports none rather than charging every branch the whole company's.
        /// </summary>
        private static decimal SalaryNetPaidInRange(TenantErpDbContext db, DateTime from, DateTime toExclusive, int? branchId) =>
            branchId != null
                ? 0m
                : db.SalaryPayments.Where(s => !s.IsDeleted && s.IsPaid && s.PaidDate >= from && s.PaidDate < toExclusive).Sum(s => (decimal?)s.NetAmount) ?? 0m;

        /// <summary>Net owner capital (contributions less withdrawals) in a window. Company-wide -
        /// callers skip it entirely for a single-branch view.</summary>
        private static decimal OwnerNetInRange(TenantErpDbContext db, DateTime from, DateTime toExclusive)
        {
            var contributions = db.OwnerTransactions.Where(o => o.Type == OwnerTransactionType.Contribution && o.TransactionDate >= from && o.TransactionDate < toExclusive).Sum(o => (decimal?)o.Amount) ?? 0m;
            var withdrawals = db.OwnerTransactions.Where(o => o.Type == OwnerTransactionType.Withdrawal && o.TransactionDate >= from && o.TransactionDate < toExclusive).Sum(o => (decimal?)o.Amount) ?? 0m;
            return contributions - withdrawals;
        }

        private static decimal CashAsOf(TenantErpDbContext db, DateTime exclusive, int? branchId)
        {
            var revenue = RevenueInRange(db, DateTime.MinValue, exclusive, branchId);
            var paidExpenses = db.Expenses.Where(e => !e.IsDeleted && e.PaidDate != null && e.PaidDate < exclusive && (branchId == null || e.BranchId == branchId)).Sum(e => (decimal?)e.Amount) ?? 0m;
            var salaries = SalaryNetPaidInRange(db, DateTime.MinValue, exclusive, branchId);
            var ownerNet = branchId == null ? OwnerNetInRange(db, DateTime.MinValue, exclusive) : 0m;

            return revenue - paidExpenses - salaries + ownerNet;
        }

        /// <summary>Same figure Reports' Outstanding Payments tab already computes: for every
        /// non-cancelled booking scheduled on/before the cutoff, its price less payments made
        /// on/before the cutoff, floored at 0 per booking.</summary>
        private static decimal AccountsReceivableAsOf(TenantErpDbContext db, DateTime exclusive, int? branchId)
        {
            var bookings = db.Bookings
                .Where(b => b.Status != BookingStatus.Cancelled && b.SessionDate < exclusive && (branchId == null || b.BranchId == branchId))
                .Select(b => new { b.BookingId, b.PriceSnapshot })
                .ToList();
            if (bookings.Count == 0) return 0m;

            var bookingIds = bookings.Select(b => b.BookingId).ToList();
            var paidTotals = db.Payments
                .Where(p => !p.IsDeleted && p.PaymentDate < exclusive && bookingIds.Contains(p.BookingId))
                .GroupBy(p => p.BookingId)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.AmountPaid));
            var addOnTotals = AddOnsService.GetAddOnsTotals(db, bookingIds);

            return bookings.Sum(b => Math.Max(0m, b.PriceSnapshot + addOnTotals.GetValueOrDefault(b.BookingId) - paidTotals.GetValueOrDefault(b.BookingId)));
        }

        /// <summary>Expense.Amount where ExpenseDate is on/before the cutoff and it's still unpaid
        /// as of the cutoff (PaidDate is null or falls after it).</summary>
        private static decimal AccountsPayableAsOf(TenantErpDbContext db, DateTime asOf, DateTime exclusive, int? branchId) =>
            db.Expenses.Where(e => !e.IsDeleted && e.ExpenseDate < exclusive && (e.PaidDate == null || e.PaidDate > asOf)
                                   && (branchId == null || e.BranchId == branchId))
                .Sum(e => (decimal?)e.Amount) ?? 0m;

        /// <summary>Owner capital is company-wide, so a branch reports none of it - see the class
        /// remarks on why that keeps the branch views summing to the consolidated one.</summary>
        private static decimal OwnersCapitalAsOf(TenantErpDbContext db, DateTime exclusive, int? branchId) =>
            branchId != null ? 0m : OwnerNetInRange(db, DateTime.MinValue, exclusive);
    }
}
