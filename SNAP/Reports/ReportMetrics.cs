using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;

namespace SNAP
{
    /// <summary>
    /// The KPI figures and chart series behind Reports &amp; Analytics, computed in one place so
    /// the home Dashboard - which shows the same KPIs and charts - can never disagree with Reports.
    /// Both screens call Compute with their own date range and branch; neither re-queries any of
    /// these numbers itself.
    ///
    /// includeExtended mirrors the tier split every other screen uses: Micro has only Bookings and
    /// Payments, so everything that needs Finance/Inventory/Equipment stays empty (null for the
    /// money KPIs) rather than being computed from tables that tier doesn't use.
    /// </summary>
    internal sealed class ReportMetrics
    {
        public decimal TotalIncome { get; private init; }
        public int BookingsInRange { get; private init; }
        public int PaymentsInRange { get; private init; }
        public decimal? TotalSalaryPaid { get; private init; }
        public decimal? TotalExpenses { get; private init; }

        public IReadOnlyList<ChartPoint> RevenueByMonth { get; private init; } = Array.Empty<ChartPoint>();
        public IReadOnlyList<ChartPoint> BookingsByStatus { get; private init; } = Array.Empty<ChartPoint>();
        public IReadOnlyList<ChartPoint> ExpensesByCategory { get; private init; } = Array.Empty<ChartPoint>();
        public IReadOnlyList<ChartPoint> EquipmentStatus { get; private init; } = Array.Empty<ChartPoint>();
        public IReadOnlyList<ChartPoint> LowStockWorstFirst { get; private init; } = Array.Empty<ChartPoint>();

        /// <summary>
        /// Null whenever comparing branches doesn't apply: below Medium tier (no branches), or
        /// when the view is already narrowed to one branch.
        /// </summary>
        public IReadOnlyList<ChartPoint>? BranchNetIncome { get; private init; }

        // The underlying rows Reports' Detailed Reports tabs bind to - exposed so those tabs show
        // exactly the rows the KPIs/charts above them were computed from.
        public List<Booking> BookingRows { get; private init; } = new();
        public List<ReportsForm.EquipmentUsageRow> EquipmentUsageRows { get; private init; } = new();
        public List<ReportsForm.LowStockRow> LowStockRows { get; private init; } = new();

        public static ReportMetrics Compute(TenantErpDbContext db, DateTime from, DateTime toExclusive, int? branchId, bool includeExtended)
        {
            // Revenue Summary: income actually collected in the window (by PaymentDate) versus
            // bookings scheduled in the window (by SessionDate) - the gap between the two
            // counts is how missing/partial payments show up at a glance.
            // A Payment has no branch of its own - it belongs to the branch its Booking was
            // made at, the same attribution the Branch Comparison chart and the financial
            // statements use.
            var paymentsInRange = db.Payments
                .Where(p => !p.IsDeleted && p.PaymentDate >= from && p.PaymentDate < toExclusive
                            && (branchId == null || db.Bookings.Any(b => b.BookingId == p.BookingId && b.BranchId == branchId)))
                .ToList();

            // Walk-in sales only - booking add-ons are collected through the booking's Payments
            // (they're part of its amount due), so counting them here too would double them.
            var productSalesInRange = db.ProductSales
                .Where(p => !p.IsDeleted && p.BookingId == null && p.SaleDate >= from && p.SaleDate < toExclusive
                            && (branchId == null || p.BranchId == branchId))
                .ToList();

            var bookingsInRange = db.Bookings
                .Include(b => b.Customer)
                .Where(b => b.Status != BookingStatus.Cancelled && b.SessionDate >= from && b.SessionDate < toExclusive
                            && (branchId == null || b.BranchId == branchId))
                .OrderBy(b => b.SessionDate)
                .ToList();

            // Revenue is payments plus product sales, matching how Total Income is computed - the
            // two must agree or one of them is lying.
            var revenueByMonth = paymentsInRange
                .Select(p => (Month: new DateTime(p.PaymentDate.Year, p.PaymentDate.Month, 1), Amount: p.AmountPaid))
                .Concat(productSalesInRange
                    .Select(p => (Month: new DateTime(p.SaleDate.Year, p.SaleDate.Month, 1), Amount: p.TotalAmount)))
                .GroupBy(x => x.Month)
                .OrderBy(g => g.Key)
                .Select(g => new ChartPoint(g.Key.ToString("MMM yy"), g.Sum(x => x.Amount)))
                .ToList();

            // Unlike bookingsInRange above, Cancelled is included here on purpose - seeing how many
            // bookings fall through is the whole point of this chart.
            var bookingsByStatus = db.Bookings
                .Where(b => b.SessionDate >= from && b.SessionDate < toExclusive && (branchId == null || b.BranchId == branchId))
                .GroupBy(b => b.Status)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToList()
                .OrderBy(x => x.Key)
                .Select(x => new ChartPoint(x.Key.ToString(), x.Count))
                .ToList();

            var totalIncome = paymentsInRange.Sum(p => p.AmountPaid) + productSalesInRange.Sum(p => p.TotalAmount);

            if (!includeExtended)
            {
                return new ReportMetrics
                {
                    TotalIncome = totalIncome,
                    BookingsInRange = bookingsInRange.Count,
                    PaymentsInRange = paymentsInRange.Count,
                    RevenueByMonth = revenueByMonth,
                    BookingsByStatus = bookingsByStatus,
                    BookingRows = bookingsInRange
                };
            }

            var salaryPaidInRange = db.SalaryPayments
                .Where(s => !s.IsDeleted && s.IsPaid && s.PaidDate >= from && s.PaidDate < toExclusive)
                .Sum(s => (decimal?)s.NetAmount) ?? 0m;

            var expensesInRange = db.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= from && e.ExpenseDate < toExclusive
                            && (branchId == null || e.BranchId == branchId))
                .Sum(e => (decimal?)e.Amount) ?? 0m;

            var expensesByCategory = db.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= from && e.ExpenseDate < toExclusive
                            && (branchId == null || e.BranchId == branchId))
                .GroupBy(e => e.Category)
                .Select(g => new { g.Key, Total = g.Sum(e => e.Amount) })
                .ToList()
                .OrderByDescending(x => x.Total)
                .Select(x => new ChartPoint(x.Key.ToString(), x.Total))
                .ToList();

            // Equipment Usage: counts by Status, regardless of date range - "in use right now"
            // isn't a date-ranged fact, unlike the revenue/finance figures above.
            var equipmentUsageRows = db.Equipment
                .Where(e => e.IsActive && (branchId == null || e.BranchId == branchId))
                .GroupBy(e => e.Status)
                .Select(g => new ReportsForm.EquipmentUsageRow { Status = g.Key.ToString(), Count = g.Count() })
                .ToList()
                .OrderBy(r => r.Status)
                .ToList();

            // Low Stock Alerts: also not date-ranged - current stock levels only.
            var lowStockRows = db.InventoryItems
                .Include(i => i.Supplier)
                .Where(i => i.IsActive && i.QuantityOnHand <= i.ReorderLevel && (branchId == null || i.BranchId == branchId))
                .OrderBy(i => i.QuantityOnHand)
                .Select(i => new ReportsForm.LowStockRow
                {
                    InventoryId = i.InventoryId,
                    ItemName = i.ItemName,
                    QuantityOnHand = i.QuantityOnHand,
                    ReorderLevel = i.ReorderLevel,
                    Supplier = i.Supplier != null ? i.Supplier.SupplierName : string.Empty
                })
                .ToList();

            return new ReportMetrics
            {
                TotalIncome = totalIncome,
                BookingsInRange = bookingsInRange.Count,
                PaymentsInRange = paymentsInRange.Count,
                TotalSalaryPaid = salaryPaidInRange,
                TotalExpenses = expensesInRange,
                RevenueByMonth = revenueByMonth,
                BookingsByStatus = bookingsByStatus,
                ExpensesByCategory = expensesByCategory,
                EquipmentStatus = equipmentUsageRows.Select(r => new ChartPoint(r.Status, r.Count)).ToList(),
                // The chart is a ranked "worst first" glance, not the full list (that's what the
                // Low Stock Alerts tab is for) - capped so it stays readable regardless of how
                // many items are actually low.
                LowStockWorstFirst = lowStockRows
                    .OrderByDescending(r => r.ReorderLevel - r.QuantityOnHand)
                    .Take(8)
                    .Select(r => new ChartPoint(r.ItemName, r.QuantityOnHand))
                    .ToList(),
                // Comparing branches only says anything when you're looking at all of them. Drill
                // into one and the trend/breakdown charts already tell that branch's story.
                BranchNetIncome = AppSession.IsBranchAware && branchId == null
                    ? ComputeBranchNetIncome(db, from, toExclusive)
                    : null,
                BookingRows = bookingsInRange,
                EquipmentUsageRows = equipmentUsageRows,
                LowStockRows = lowStockRows
            };
        }

        /// <summary>
        /// Net income per branch: its revenue less its own expenses. Payroll and owner capital are
        /// company-wide and deliberately excluded - attributing them to a branch would invent a
        /// split the data doesn't have.
        /// </summary>
        private static List<ChartPoint> ComputeBranchNetIncome(TenantErpDbContext db, DateTime from, DateTime toExclusive)
        {
            var branches = db.Branches.Where(b => b.IsActive).OrderBy(b => b.BranchName).ToList();
            var branchPoints = new List<ChartPoint>();

            foreach (var branch in branches)
            {
                var revenue = db.ProductSales
                    .Where(p => !p.IsDeleted && p.BookingId == null && p.BranchId == branch.BranchId && p.SaleDate >= from && p.SaleDate < toExclusive)
                    .Sum(p => (decimal?)p.TotalAmount) ?? 0m;

                revenue += db.Payments
                    .Where(p => !p.IsDeleted && p.PaymentDate >= from && p.PaymentDate < toExclusive
                                && db.Bookings.Any(b => b.BookingId == p.BookingId && b.BranchId == branch.BranchId))
                    .Sum(p => (decimal?)p.AmountPaid) ?? 0m;

                var expenses = db.Expenses
                    .Where(e => !e.IsDeleted && e.BranchId == branch.BranchId && e.ExpenseDate >= from && e.ExpenseDate < toExclusive)
                    .Sum(e => (decimal?)e.Amount) ?? 0m;

                branchPoints.Add(new ChartPoint(branch.BranchName, revenue - expenses));
            }

            return branchPoints;
        }
    }
}
