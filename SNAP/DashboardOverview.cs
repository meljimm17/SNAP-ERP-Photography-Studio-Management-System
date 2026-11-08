using System.Drawing;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.Controls;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;

namespace SNAP
{
    /// <summary>
    /// The home Dashboard's KPI cards and charts: the same figures as Reports &amp; Analytics
    /// (both read ReportMetrics), fixed to the current month, where every card and chart opens
    /// the module its numbers are stored in - Low Stock opens Inventory, Bookings opens Bookings
    /// &amp; Scheduling, and so on.
    ///
    /// What appears is limited two ways, so no tenant or role sees a number it couldn't otherwise
    /// reach. The tier limits the data: Micro only computes Bookings/Payments figures (see
    /// ReportMetrics), and Branch Comparison exists only at Medium. And a card or chart is shown
    /// only when canOpen says its target module is in this user's sidebar - which already folds
    /// in both the tier's module set and the role's permissions - so the click is never a dead end.
    /// </summary>
    internal sealed class DashboardOverview : UserControl
    {
        private readonly Func<SystemModule, bool> _canOpen;
        private readonly Action<SystemModule> _open;
        private readonly bool _isMicroTier = AppSession.CurrentPlanTier == PlanTier.Micro;
        private readonly Panel _scrollHost = new() { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppTheme.BackgroundColor };

        public DashboardOverview(Func<SystemModule, bool> canOpen, Action<SystemModule> open)
        {
            _canOpen = canOpen;
            _open = open;
            Dock = DockStyle.Fill;
            BackColor = AppTheme.BackgroundColor;
            Controls.Add(_scrollHost);
        }

        /// <summary>Current month, start inclusive / end exclusive - shown in the page header too.</summary>
        public static (DateTime From, DateTime ToExclusive) CurrentMonth()
        {
            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            return (monthStart, monthStart.AddMonths(1));
        }

        public void Render(int? branchId)
        {
            foreach (Control old in _scrollHost.Controls.Cast<Control>().ToList())
            {
                old.Dispose();
            }
            _scrollHost.Controls.Clear();

            ReportMetrics metrics;
            try
            {
                var (from, toExclusive) = CurrentMonth();
                using var db = CreateDbContext();
                metrics = ReportMetrics.Compute(db, from, toExclusive, branchId, includeExtended: !_isMicroTier);
            }
            catch (Exception ex)
            {
                _scrollHost.Controls.Add(new Label
                {
                    Text = $"Couldn't load dashboard figures.\n\n{ex.Message}",
                    ForeColor = AppTheme.TextSecondaryColor,
                    Font = AppTheme.BodyFont,
                    Dock = DockStyle.Fill,
                    Padding = new Padding(8)
                });
                return;
            }

            var kpiRow = BuildKpiRow(metrics);
            var chartsGrid = BuildChartsGrid(metrics);

            var kpiHeight = kpiRow is null ? 0 : ReportsForm.KpiRowHeight;
            var chartsHeight = chartsGrid is null ? 0 : chartsGrid.RowCount * ReportsForm.ChartRowHeight;

            if (kpiRow is null && chartsGrid is null)
            {
                _scrollHost.Controls.Add(new Label
                {
                    Text = "No dashboard figures are available for your role.",
                    ForeColor = AppTheme.TextSecondaryColor,
                    Font = AppTheme.BodyFont,
                    Dock = DockStyle.Fill,
                    Padding = new Padding(8)
                });
                return;
            }

            // Same fixed-height, Dock=Top-inside-AutoScroll arrangement ReportsForm uses, so a
            // Small/Medium dashboard taller than the window scrolls instead of being clipped.
            var page = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = kpiHeight + chartsHeight,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = AppTheme.BackgroundColor
            };
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, kpiHeight));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, chartsHeight));
            if (kpiRow is not null) page.Controls.Add(kpiRow, 0, 0);
            if (chartsGrid is not null) page.Controls.Add(chartsGrid, 0, 1);

            _scrollHost.Controls.Add(page);
        }

        /// <summary>
        /// Reports' KPI row, card for card: Total Income, Bookings, Payments, plus Total Expenses
        /// above Micro. Columns are sized to however many cards this user actually gets.
        /// </summary>
        private TableLayoutPanel? BuildKpiRow(ReportMetrics metrics)
        {
            var cards = new List<(string Title, string Value, SystemModule Target)>
            {
                ("Total Income", $"₱{metrics.TotalIncome:N2}", SystemModule.PaymentManagement),
                ("Bookings This Month", metrics.BookingsInRange.ToString(), SystemModule.BookingScheduling),
                ("Payments This Month", metrics.PaymentsInRange.ToString(), SystemModule.PaymentManagement)
            };
            if (!_isMicroTier)
            {
                cards.Add(("Total Expenses", $"₱{metrics.TotalExpenses ?? 0m:N2}", SystemModule.FinanceManagement));
            }

            cards = cards.Where(c => _canOpen(c.Target)).ToList();
            if (cards.Count == 0) return null;

            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = cards.Count, RowCount = 1, BackColor = AppTheme.BackgroundColor };
            for (var i = 0; i < cards.Count; i++)
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cards.Count));
                var target = cards[i].Target;
                var valueLabel = new Label { Text = cards[i].Value };
                row.Controls.Add(AppTheme.CreateKpiCard(cards[i].Title, valueLabel, () => _open(target)), i, 0);
            }

            return row;
        }

        /// <summary>
        /// Reports' charts, two per row, each with the same chart type and settings as its Reports
        /// counterpart. Charts whose module this user can't open are left out entirely rather
        /// than leaving a blank cell, so the grid always reflows to what's actually shown.
        /// </summary>
        private TableLayoutPanel? BuildChartsGrid(ReportMetrics metrics)
        {
            var charts = new List<(ChartPanel Chart, SystemModule Target)>
            {
                (NewChart("Revenue by Month", ChartKind.Line, false, metrics.RevenueByMonth), SystemModule.PaymentManagement),
                (NewChart("Bookings by Status", ChartKind.Bar, true, metrics.BookingsByStatus), SystemModule.BookingScheduling)
            };

            if (!_isMicroTier)
            {
                charts.Add((NewChart("Expenses by Category", ChartKind.Donut, false, metrics.ExpensesByCategory), SystemModule.FinanceManagement));
                charts.Add((NewChart("Equipment Status", ChartKind.Donut, false, metrics.EquipmentStatus), SystemModule.EquipmentManagement));
                charts.Add((NewChart("Low Stock Items (worst first)", ChartKind.HorizontalBar, true, metrics.LowStockWorstFirst), SystemModule.InventoryManagement));

                // Medium tier, viewing all branches - no single module holds a per-branch net
                // income figure, so this one opens Reports, where the branch statements live.
                if (metrics.BranchNetIncome is not null)
                {
                    charts.Add((NewChart("Net Income by Branch", ChartKind.Bar, false, metrics.BranchNetIncome), SystemModule.ReportsManagement));
                }
            }

            var visible = charts.Where(c => _canOpen(c.Target)).ToList();
            foreach (var hidden in charts.Except(visible)) hidden.Chart.Dispose();
            if (visible.Count == 0) return null;

            const int columns = 2;
            var rows = (int)Math.Ceiling(visible.Count / (double)columns);

            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = rows, BackColor = AppTheme.BackgroundColor };
            for (var c = 0; c < columns; c++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            }
            for (var r = 0; r < rows; r++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, ReportsForm.ChartRowHeight));
            }

            for (var i = 0; i < visible.Count; i++)
            {
                var (chart, target) = visible[i];
                AppTheme.MakeChartClickable(chart, () => _open(target));

                var card = AppTheme.WrapContent(chart);
                card.Dock = DockStyle.Fill;
                card.Margin = new Padding(8);
                grid.Controls.Add(card, i % columns, i / columns);
            }

            return grid;
        }

        private static ChartPanel NewChart(string title, ChartKind kind, bool categorical, IReadOnlyList<ChartPoint> data)
        {
            var chart = new ChartPanel { Title = title, Kind = kind, UseCategoricalColors = categorical, ValueFormat = "N0" };
            chart.SetData(data);
            return chart;
        }

        private static TenantErpDbContext CreateDbContext()
        {
            var connectionString = AppSession.CurrentTenantConnectionString ?? AppDbConfig.TenantErpConnectionString;
            var options = new DbContextOptionsBuilder<TenantErpDbContext>()
                .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3))
                .Options;
            return new TenantErpDbContext(options);
        }
    }
}
