using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.Controls;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;

namespace SNAP
{
    /// <summary>
    /// Admin/SuperAdmin-only view of how the system generates income: revenue collected in a
    /// date range plus any bookings in that range that are still short of their PriceSnapshot
    /// once all of their (possibly multiple, partial) payments are summed. Also surfaces
    /// Equipment usage, low-stock Inventory alerts, and Finance totals for the same date range.
    /// </summary>
    public class ReportsForm : UserControl
    {
        private readonly RoundedDatePicker _fromDate = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedDatePicker _toDate = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedButton _apply = new() { Text = "Apply" };

        private readonly Label _totalIncomeValue = new();
        private readonly Label _bookingsValue = new();
        private readonly Label _paymentsValue = new();
        private readonly Label _totalSalaryValue = new();
        private readonly Label _totalExpensesValue = new();

        private readonly DataGridView _outstandingGrid = new();
        private readonly DataGridView _equipmentUsageGrid = new();
        private readonly DataGridView _lowStockGrid = new();

        // Equipment/Inventory/Finance are Small-tier-and-up additions - Micro's Reports screen
        // stays exactly as it always was (Total Income/Bookings/Payments + Outstanding Payments).
        private readonly bool _isMicroTier = AppSession.CurrentPlanTier == PlanTier.Micro;

        public ReportsForm(AccessLevel accessLevel = AccessLevel.Full)
        {
            Dock = DockStyle.Fill;
            BackColor = AppTheme.BackgroundColor;

            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            _fromDate.Value = monthStart;
            _toDate.Value = monthEnd;

            var toolbar = BuildToolbar();
            var summary = BuildSummarySection();
            var tabs = BuildDetailTabs();

            var contentLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            contentLayout.Controls.Add(summary, 0, 0);
            contentLayout.Controls.Add(tabs, 0, 1);

            var header = AppTheme.CreatePageHeader("Reports & Analytics", "View business reports and summarized information.");
            Controls.Add(AppTheme.BuildPage(header, toolbar, contentLayout));

            _apply.Click += (_, _) => LoadData();

            LoadData();
        }

        private FlowLayoutPanel BuildToolbar()
        {
            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = AppTheme.BackgroundColor
            };

            var fromLabel = new Label { Text = "From", ForeColor = AppTheme.TextSecondaryColor, AutoSize = true, Margin = new Padding(0, 14, 6, 0) };
            _fromDate.Width = 160;
            var toLabel = new Label { Text = "To", ForeColor = AppTheme.TextSecondaryColor, AutoSize = true, Margin = new Padding(12, 14, 6, 0) };
            _toDate.Width = 160;
            _apply.Width = 90;
            _apply.Margin = new Padding(12, 0, 0, 0);

            toolbar.Controls.Add(fromLabel);
            toolbar.Controls.Add(_fromDate);
            toolbar.Controls.Add(toLabel);
            toolbar.Controls.Add(_toDate);
            toolbar.Controls.Add(_apply);
            return toolbar;
        }

        private FlowLayoutPanel BuildSummarySection()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 0, 0, 0),
                BackColor = AppTheme.BackgroundColor
            };

            panel.Controls.Add(CreateStatCard("Total Income", _totalIncomeValue));
            panel.Controls.Add(CreateStatCard("Bookings in Range", _bookingsValue));
            panel.Controls.Add(CreateStatCard("Payments in Range", _paymentsValue));
            if (!_isMicroTier)
            {
                panel.Controls.Add(CreateStatCard("Total Salary Paid", _totalSalaryValue));
                panel.Controls.Add(CreateStatCard("Total Expenses", _totalExpensesValue));
            }
            return panel;
        }

        private static RoundedPanel CreateStatCard(string title, Label valueLabel)
        {
            var card = new RoundedPanel
            {
                Width = 220,
                Height = 110,
                BackColor = AppTheme.WhiteColor,
                Padding = new Padding(16),
                Margin = new Padding(0, 8, 16, 8)
            };

            var titleLabel = new Label
            {
                Text = title,
                ForeColor = AppTheme.TextSecondaryColor,
                Font = AppTheme.SecondaryFont,
                AutoSize = false,
                Width = 188,
                Height = 24,
                Location = new Point(16, 16)
            };

            valueLabel.Text = "—";
            valueLabel.ForeColor = AppTheme.TextPrimaryColor;
            valueLabel.Font = new Font("Segoe UI", 18, FontStyle.Bold);
            valueLabel.AutoSize = false;
            valueLabel.Width = 188;
            valueLabel.Height = 50;
            valueLabel.Location = new Point(16, 45);
            valueLabel.TextAlign = ContentAlignment.TopLeft;

            card.Controls.Add(titleLabel);
            card.Controls.Add(valueLabel);
            return card;
        }

        /// <summary>
        /// Outstanding Payments / Equipment Usage / Low Stock Alerts on three tabs of one card,
        /// rather than stacking three separate grids vertically.
        /// </summary>
        private RoundedPanel BuildDetailTabs()
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.WhiteColor,
                Padding = new Padding(16)
            };

            var tabs = new TabControl { Dock = DockStyle.Fill };
            AppTheme.StyleTabControl(tabs);
            tabs.TabPages.Add(BuildGridTab("Outstanding Payments", _outstandingGrid));
            if (!_isMicroTier)
            {
                tabs.TabPages.Add(BuildGridTab("Equipment Usage", _equipmentUsageGrid));
                tabs.TabPages.Add(BuildGridTab("Low Stock Alerts", _lowStockGrid));
            }

            card.Controls.Add(tabs);
            return card;
        }

        private static TabPage BuildGridTab(string title, DataGridView grid)
        {
            var page = new TabPage(title) { BackColor = AppTheme.WhiteColor };

            AppTheme.StyleGrid(grid);
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AutoGenerateColumns = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;

            page.Controls.Add(grid);
            return page;
        }

        private void LoadData()
        {
            try
            {
                using var db = CreateDbContext();

                var from = _fromDate.Value.Date;
                var toExclusive = _toDate.Value.Date.AddDays(1);

                // Revenue Summary: income actually collected in the window (by PaymentDate) versus
                // bookings scheduled in the window (by SessionDate) - the gap between the two
                // counts is how missing/partial payments show up at a glance.
                var paymentsInRange = db.Payments
                    .Where(p => !p.IsDeleted && p.PaymentDate >= from && p.PaymentDate < toExclusive)
                    .ToList();

                var bookingsInRange = db.Bookings
                    .Include(b => b.Customer)
                    .Where(b => b.Status != BookingStatus.Cancelled && b.SessionDate >= from && b.SessionDate < toExclusive)
                    .OrderBy(b => b.SessionDate)
                    .ToList();

                _totalIncomeValue.Text = $"₱{paymentsInRange.Sum(p => p.AmountPaid):N2}";
                _bookingsValue.Text = bookingsInRange.Count.ToString();
                _paymentsValue.Text = paymentsInRange.Count.ToString();

                if (!_isMicroTier)
                {
                    var salaryPaidInRange = db.SalaryPayments
                        .Where(s => s.PaymentDate >= from && s.PaymentDate < toExclusive)
                        .Sum(s => (decimal?)s.Amount) ?? 0m;

                    var expensesInRange = db.Expenses
                        .Where(e => e.ExpenseDate >= from && e.ExpenseDate < toExclusive)
                        .Sum(e => (decimal?)e.Amount) ?? 0m;

                    _totalSalaryValue.Text = $"₱{salaryPaidInRange:N2}";
                    _totalExpensesValue.Text = $"₱{expensesInRange:N2}";
                }

                // Outstanding Payments: sum ALL payments ever made against each booking in range
                // (not just ones dated inside the window), since a booking's balance depends on its
                // full payment history - a booking can have several partial payments recorded on
                // different dates, only some of which may fall in the selected range.
                var bookingIds = bookingsInRange.Select(b => b.BookingId).ToList();
                var paidTotalsByBooking = db.Payments
                    .Where(p => !p.IsDeleted && bookingIds.Contains(p.BookingId))
                    .GroupBy(p => p.BookingId)
                    .ToDictionary(g => g.Key, g => g.Sum(p => p.AmountPaid));

                _outstandingGrid.DataSource = bookingsInRange
                    .Select(b => new OutstandingRow
                    {
                        BookingId = b.BookingId,
                        Customer = b.Customer?.FullName ?? $"Customer #{b.CustomerId}",
                        BookingDate = b.SessionDate,
                        PriceSnapshot = b.PriceSnapshot,
                        AmountPaid = paidTotalsByBooking.GetValueOrDefault(b.BookingId),
                        AmountDue = b.PriceSnapshot - paidTotalsByBooking.GetValueOrDefault(b.BookingId)
                    })
                    .Where(r => r.AmountDue > 0)
                    .OrderByDescending(r => r.AmountDue)
                    .ToList();

                if (!_isMicroTier)
                {
                    // Equipment Usage: counts by Status, regardless of date range - "in use right
                    // now" isn't a date-ranged fact, unlike the revenue/finance figures above.
                    _equipmentUsageGrid.DataSource = db.Equipment
                        .Where(e => e.IsActive)
                        .GroupBy(e => e.Status)
                        .Select(g => new EquipmentUsageRow { Status = g.Key.ToString(), Count = g.Count() })
                        .ToList()
                        .OrderBy(r => r.Status)
                        .ToList();

                    // Low Stock Alerts: also not date-ranged - current stock levels only.
                    _lowStockGrid.DataSource = db.InventoryItems
                        .Include(i => i.Supplier)
                        .Where(i => i.IsActive && i.QuantityOnHand <= i.ReorderLevel)
                        .OrderBy(i => i.QuantityOnHand)
                        .Select(i => new LowStockRow
                        {
                            ItemName = i.ItemName,
                            QuantityOnHand = i.QuantityOnHand,
                            ReorderLevel = i.ReorderLevel,
                            Supplier = i.Supplier != null ? i.Supplier.SupplierName : string.Empty
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static TenantErpDbContext CreateDbContext()
        {
            var connectionString = AppSession.CurrentTenantConnectionString
                ?? AppDbConfig.TenantErpConnectionString;

            var options = new DbContextOptionsBuilder<TenantErpDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new TenantErpDbContext(options);
        }

        private sealed class OutstandingRow
        {
            public int BookingId { get; set; }
            public string Customer { get; set; } = string.Empty;
            public DateTime BookingDate { get; set; }
            public decimal PriceSnapshot { get; set; }
            public decimal AmountPaid { get; set; }
            public decimal AmountDue { get; set; }
        }

        private sealed class EquipmentUsageRow
        {
            public string Status { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        private sealed class LowStockRow
        {
            public string ItemName { get; set; } = string.Empty;
            public decimal QuantityOnHand { get; set; }
            public decimal ReorderLevel { get; set; }
            public string Supplier { get; set; } = string.Empty;
        }
    }
}
