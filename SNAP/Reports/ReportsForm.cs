using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SNAP.Controls;
using SNAP.domain.dtos;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP
{
    /// <summary>
    /// Admin/SuperAdmin-only view of how the system generates income: revenue collected in a
    /// date range plus any bookings in that range that are still short of their PriceSnapshot
    /// once all of their (possibly multiple, partial) payments are summed. Also surfaces
    /// Equipment usage, low-stock Inventory alerts, and Finance totals for the same date range.
    ///
    /// Laid out as one continuous page, not one box: Header -> Date Filters -> KPI Summary ->
    /// Charts -> Detailed Reports. The dashboard (KPIs + charts) sits permanently above the
    /// Detailed Reports tab strip (Outstanding Payments, Equipment Usage, Low Stock, the three
    /// financial statements, Audit Log) rather than being squeezed alongside those tabs as one
    /// more of them - every KPI card and chart is still clickable and jumps straight to its
    /// matching tab, the same place clicking that tab's own header would land.
    /// </summary>
    public class ReportsForm : UserControl
    {
        private readonly InventoryPresenter _inventoryController = new();

        private readonly RoundedDatePicker _fromDate = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedDatePicker _toDate = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedButton _apply = new() { Text = "Apply" };
        private readonly RoundedButton _generateReport = new() { Text = "Generate Report" };

        private FinancialStatementService.IncomeStatementResult? _incomeStatementResult;
        private FinancialStatementService.BalanceSheetResult? _balanceSheetResult;
        private FinancialStatementService.CashFlowStatementResult? _cashFlowStatementResult;

        private readonly Label _totalIncomeValue = new();
        private readonly Label _bookingsValue = new();
        private readonly Label _paymentsValue = new();
        private readonly Label _totalSalaryValue = new();
        private readonly Label _totalExpensesValue = new();

        private readonly DataGridView _outstandingGrid = new();
        private readonly DataGridView _equipmentUsageGrid = new();
        private readonly DataGridView _lowStockGrid = new();
        private readonly RoundedButton _reorderButton = new() { Text = "Reorder Selected", IsPrimary = false };

        private readonly Panel _incomeStatementHost = new() { Dock = DockStyle.Fill, BackColor = AppTheme.WhiteColor, AutoScroll = true };
        private readonly Panel _balanceSheetHost = new() { Dock = DockStyle.Fill, BackColor = AppTheme.WhiteColor, AutoScroll = true };
        private readonly Panel _cashFlowHost = new() { Dock = DockStyle.Fill, BackColor = AppTheme.WhiteColor, AutoScroll = true };

        private readonly DataGridView _auditLogGrid = new();
        private readonly RoundedComboBox _auditEntityFilter = new();
        private readonly RoundedComboBox _auditActorFilter = new();
        private readonly RoundedButton _auditApply = new() { Text = "Filter" };

        // Equipment/Inventory/Finance are Small-tier-and-up additions - Micro's Reports screen
        // stays exactly as it always was (Total Income/Bookings/Payments + Outstanding Payments).
        private readonly bool _isMicroTier = AppSession.CurrentPlanTier == PlanTier.Micro;

        // Chosen by the job each one's data does, not by habit - see the goals this screen was
        // redesigned against: Line for a trend over time, Bar for a same-unit comparison across a
        // few named things, Donut for a part-to-whole split, HorizontalBar for a ranked list whose
        // category names (item names here) are too long to fit under a vertical bar.
        private readonly ChartPanel _revenueTrendChart = new() { Title = "Revenue by Month", Kind = ChartKind.Line, ValueFormat = "N0" };
        // Bookings exist at every tier, so this is the one chart Micro also gets - see ApplyCharts.
        private readonly ChartPanel _bookingStatusChart = new() { Title = "Bookings by Status", Kind = ChartKind.Bar, UseCategoricalColors = true, ValueFormat = "N0" };
        private readonly ChartPanel _expenseBreakdownChart = new() { Title = "Expenses by Category", Kind = ChartKind.Donut, ValueFormat = "N0" };
        private readonly ChartPanel _equipmentStatusChart = new() { Title = "Equipment Status", Kind = ChartKind.Donut, ValueFormat = "N0" };
        private readonly ChartPanel _lowStockChart = new() { Title = "Low Stock Items (worst first)", Kind = ChartKind.HorizontalBar, UseCategoricalColors = true, ValueFormat = "N0" };
        private readonly ChartPanel _branchComparisonChart = new() { Title = "Net Income by Branch", Kind = ChartKind.Bar, ValueFormat = "N0" };

        // Branch picker for report-level filtering — only shown on Medium tier with a company-wide
        // role. Its SelectedIndexChanged triggers a full LoadData() refresh.
        private readonly RoundedComboBox _reportBranchPicker = new();

        // The single TabControl every Detailed Reports tab lives in, and a reference to each one
        // so the dashboard's KPI cards/charts can jump straight to it - the same place clicking
        // that tab's own header would land. Null for a tab that doesn't exist below Small tier.
        private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
        private TabPage? _outstandingTabPage;
        private TabPage? _equipmentUsageTabPage;
        private TabPage? _lowStockTabPage;
        private TabPage? _incomeStatementTabPage;
        private TabPage? _balanceSheetTabPage;
        private TabPage? _cashFlowTabPage;
        private TabPage? _auditLogTabPage;

        // Chrome-row heights for the dashboard section, following this codebase's established
        // convention (AppTheme.HeaderRowHeight/ToolbarRowHeight): fixed, deterministic pixel
        // heights for the rows that shouldn't stretch - not AutoSize propagation through nested
        // TableLayoutPanels, which this exact WinForms runtime's Dock/AutoSize interplay makes
        // unreliable (see AppTheme.BuildFieldGroup). DetailReportsHeight is generous rather than
        // Percent=100 of whatever's left, precisely so it keeps a real, usable size of its own
        // regardless of how tall the dashboard above it gets - the two stack inside one scrolling
        // canvas instead (see the constructor), so neither has to fight the other for space.
        internal const int KpiRowHeight = 110;
        internal const int ChartRowHeight = 300;
        private const int DetailReportsHeight = 640;

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

            // Detailed Reports is built first so the dashboard's KPI cards/charts can capture a
            // direct reference to each tab to jump to.
            var detailReports = BuildDetailReportsSection();
            var dashboard = BuildDashboardSection(out var dashboardHeight);

            // Header -> Date Filters (toolbar) -> KPI Summary + Charts (dashboard) -> Detailed
            // Reports. No single container wraps the whole page - the dashboard and the detailed-
            // reports tab strip are two independent, separately bordered sections, not one box
            // with everything crammed inside it.
            //
            // Both use a fixed, explicit height (see the class remarks), so their combined height
            // can genuinely exceed the visible window - Dock=Top rather than Fill lets this inner
            // panel be taller than its parent, and the outer scrollHost (AutoScroll=true) is what
            // turns that overflow into a normal vertical scrollbar instead of clipped, unreachable
            // content. Small/Medium tier's extra chart rows are exactly the case this exists for -
            // a dashboard tall enough to need it stays fully reachable, it just scrolls.
            var page = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = dashboardHeight + DetailReportsHeight,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = AppTheme.BackgroundColor
            };
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, dashboardHeight));
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, DetailReportsHeight));
            page.Controls.Add(dashboard, 0, 0);
            page.Controls.Add(detailReports, 0, 1);

            var scrollHost = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppTheme.BackgroundColor };
            scrollHost.Controls.Add(page);

            var header = AppTheme.CreatePageHeader("Reports & Analytics", "View business reports and summarized information.");
            Controls.Add(AppTheme.BuildPage(header, toolbar, scrollHost));

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
            _apply.Margin = new Padding(12, 2, 0, 0);

            _generateReport.Width = 150;
            _generateReport.Margin = new Padding(12, 2, 0, 0);
            _generateReport.Click += (_, _) => GenerateReportPdf();

            BranchFilter.Populate(_reportBranchPicker);
            _reportBranchPicker.Width = 170;
            _reportBranchPicker.Margin = new Padding(12, 2, 0, 0);
            _reportBranchPicker.SelectedIndexChanged += (_, _) => LoadData();

            toolbar.Controls.Add(fromLabel);
            toolbar.Controls.Add(_fromDate);
            toolbar.Controls.Add(toLabel);
            toolbar.Controls.Add(_toDate);
            toolbar.Controls.Add(_apply);
            toolbar.Controls.Add(_generateReport);
            toolbar.Controls.Add(_reportBranchPicker);
            return toolbar;
        }

        /// <summary>
        /// The dashboard: KPI Summary row, then Charts grid, in that order - both fixed-height
        /// (see the class remarks on why), so the caller can size a Percent=100 row for Detailed
        /// Reports beneath it without depending on AutoSize propagation. sectionHeight is handed
        /// back rather than recomputed by the caller, so the chart count (which varies by tier and
        /// by whether Branch Comparison applies) is counted in exactly one place - BuildChartsGrid.
        /// </summary>
        private Control BuildDashboardSection(out int sectionHeight)
        {
            var chartsGrid = BuildChartsGrid();
            var kpiRow = BuildKpiRow();

            sectionHeight = KpiRowHeight + (chartsGrid.RowCount * ChartRowHeight);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = AppTheme.BackgroundColor };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, KpiRowHeight));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, chartsGrid.RowCount * ChartRowHeight));
            layout.Controls.Add(kpiRow, 0, 0);
            layout.Controls.Add(chartsGrid, 0, 1);
            return layout;
        }

        /// <summary>
        /// Exactly the metrics worth a glance at a KPI card: Total Income, Bookings, Payments,
        /// Total Expenses. Everything else that used to be a card either became one of the charts
        /// below (Bookings by Status, Expenses by Category) or is a click away in Detailed Reports
        /// - a card is for the one or two numbers that matter most, not a duplicate of a chart or
        /// table that already says the same thing better. Total Expenses drops out entirely below
        /// Small tier, where Finance doesn't exist yet - three even columns rather than four with
        /// one permanently blank.
        /// </summary>
        private TableLayoutPanel BuildKpiRow()
        {
            void GoTo(TabPage? target)
            {
                if (target is not null) _tabs.SelectedTab = target;
            }

            var columnCount = _isMicroTier ? 3 : 4;
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columnCount, RowCount = 1, BackColor = AppTheme.BackgroundColor };
            for (var i = 0; i < columnCount; i++)
            {
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columnCount));
            }

            row.Controls.Add(AppTheme.CreateKpiCard("Total Income", _totalIncomeValue, () => GoTo(_incomeStatementTabPage ?? _outstandingTabPage)), 0, 0);
            row.Controls.Add(AppTheme.CreateKpiCard("Bookings in Range", _bookingsValue, () => GoTo(_outstandingTabPage)), 1, 0);
            row.Controls.Add(AppTheme.CreateKpiCard("Payments in Range", _paymentsValue, () => GoTo(_outstandingTabPage)), 2, 0);
            if (!_isMicroTier)
            {
                row.Controls.Add(AppTheme.CreateKpiCard("Total Expenses", _totalExpensesValue, () => GoTo(_incomeStatementTabPage)), 3, 0);
            }

            return row;
        }

        /// <summary>
        /// The visual centerpiece, arranged two-per-row so each chart stays wide enough to read
        /// clearly. Bookings by Status runs at every tier - Bookings is a core module everywhere;
        /// Expense Breakdown, Equipment Status, Low Stock, and Branch Comparison need Finance/
        /// Inventory/Equipment/Branch, which don't exist below Small/Medium, so Micro stops at the
        /// first two. Every chart still lives in the grid even when a later tier-check hides one
        /// (Branch Comparison, toggled by the branch picker at LoadData time) - it just leaves an
        /// empty cell rather than reflowing the rest, which is the simpler, more predictable
        /// behaviour of the two.
        /// </summary>
        private TableLayoutPanel BuildChartsGrid()
        {
            void GoTo(TabPage? target)
            {
                if (target is not null) _tabs.SelectedTab = target;
            }

            void WireChartClick(ChartPanel chart, TabPage? target)
            {
                if (target is null) return;
                AppTheme.MakeChartClickable(chart, () => GoTo(target));
            }

            var charts = new List<ChartPanel> { _revenueTrendChart, _bookingStatusChart };
            WireChartClick(_revenueTrendChart, _incomeStatementTabPage ?? _outstandingTabPage);
            WireChartClick(_bookingStatusChart, _outstandingTabPage);

            if (!_isMicroTier)
            {
                charts.Add(_expenseBreakdownChart);
                WireChartClick(_expenseBreakdownChart, _incomeStatementTabPage);
                charts.Add(_equipmentStatusChart);
                WireChartClick(_equipmentStatusChart, _equipmentUsageTabPage);
                charts.Add(_lowStockChart);
                WireChartClick(_lowStockChart, _lowStockTabPage);
                charts.Add(_branchComparisonChart);
                WireChartClick(_branchComparisonChart, _incomeStatementTabPage);
            }

            const int columns = 2;
            var rows = Math.Max(1, (int)Math.Ceiling(charts.Count / (double)columns));

            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = rows, BackColor = AppTheme.BackgroundColor };
            for (var c = 0; c < columns; c++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            }
            for (var r = 0; r < rows; r++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, ChartRowHeight));
            }

            for (var i = 0; i < charts.Count; i++)
            {
                // WrapContent gives every chart its own subtle-bordered card, same treatment as
                // every grid elsewhere in the app - not one big box behind all of them.
                var card = AppTheme.WrapContent(charts[i]);
                card.Dock = DockStyle.Fill;
                card.Margin = new Padding(8);
                grid.Controls.Add(card, i % columns, i / columns);
            }

            return grid;
        }

        /// <summary>
        /// Outstanding Payments, Equipment Usage, Low Stock Alerts, Income Statement, Balance
        /// Sheet, Cash Flow Statement, and Audit Log - each its own independent tab/panel, wrapped
        /// once (not nested further) for the same subtle-card look every other module uses.
        /// </summary>
        private Control BuildDetailReportsSection()
        {
            AppTheme.StyleTabControl(_tabs);

            _outstandingTabPage = BuildGridTab("Outstanding Payments", _outstandingGrid);
            _tabs.TabPages.Add(_outstandingTabPage);

            if (!_isMicroTier)
            {
                _equipmentUsageTabPage = BuildGridTab("Equipment Usage", _equipmentUsageGrid);
                _lowStockTabPage = BuildLowStockTab();
                _incomeStatementTabPage = BuildStatementTab("Income Statement", _incomeStatementHost);
                _balanceSheetTabPage = BuildStatementTab("Balance Sheet", _balanceSheetHost);
                _cashFlowTabPage = BuildStatementTab("Cash Flow Statement", _cashFlowHost);
                _auditLogTabPage = BuildAuditLogTab();

                _tabs.TabPages.Add(_equipmentUsageTabPage);
                _tabs.TabPages.Add(_lowStockTabPage);
                _tabs.TabPages.Add(_incomeStatementTabPage);
                _tabs.TabPages.Add(_balanceSheetTabPage);
                _tabs.TabPages.Add(_cashFlowTabPage);
                _tabs.TabPages.Add(_auditLogTabPage);
            }

            return AppTheme.WrapContent(_tabs);
        }

        private static TabPage BuildStatementTab(string title, Panel host)
        {
            var page = new TabPage(title) { BackColor = AppTheme.WhiteColor };

            var disclaimer = new Label
            {
                Text = "Prepared for internal management use, cash/modified-cash basis.",
                ForeColor = AppTheme.TextSecondaryColor,
                Font = AppTheme.SecondaryFont,
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 24,
                Padding = new Padding(0, 4, 0, 0)
            };

            host.Padding = new Padding(8, 4, 8, 8);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(disclaimer, 0, 0);
            layout.Controls.Add(host, 0, 1);
            page.Controls.Add(layout);

            return page;
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

        private TabPage BuildLowStockTab()
        {
            var page = new TabPage("Low Stock Alerts") { BackColor = AppTheme.WhiteColor };

            AppTheme.StyleGrid(_lowStockGrid);
            _lowStockGrid.Dock = DockStyle.Fill;
            _lowStockGrid.ReadOnly = true;
            _lowStockGrid.AutoGenerateColumns = true;
            _lowStockGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _lowStockGrid.MultiSelect = false;

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.WhiteColor
            };
            _reorderButton.Width = 150;
            _reorderButton.Click += (_, _) => ReorderSelected();
            toolbar.Controls.Add(_reorderButton);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, AppTheme.ToolbarRowHeight));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(_lowStockGrid, 0, 1);
            page.Controls.Add(layout);

            return page;
        }

        private void ReorderSelected()
        {
            if (_lowStockGrid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Select a low-stock item first.", "Reorder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = (LowStockRow)_lowStockGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            Inventory item;
            try
            {
                item = _inventoryController.GetInventoryItem(selected.InventoryId);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var suppliers = _inventoryController.GetActiveSuppliers();
            var items = _inventoryController.GetActiveInventoryItems();

            if (suppliers.Count == 0)
            {
                MessageBox.Show("Add at least one active Supplier first.", "Reorder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var shortfall = System.Math.Max(item.ReorderLevel - item.QuantityOnHand, 1);
            var prefill = new PurchaseOrder
            {
                SupplierId = item.SupplierId ?? suppliers.First().SupplierId,
                Status = PurchaseOrderStatus.Draft,
                OrderDate = DateTime.Today,
                Lines = new List<PurchaseOrderLine>
                {
                    new() { InventoryId = item.InventoryId, QuantityOrdered = shortfall, UnitCost = item.UnitCost ?? 0m, Inventory = item }
                }
            };

            using var dialog = new PurchaseOrderDialogForm(suppliers, items, prefill);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var lines = dialog.Lines.Select(l => new PurchaseOrderLineInput(l.InventoryId, l.QuantityOrdered, l.UnitCost)).ToList();
                _inventoryController.AddPurchaseOrder(dialog.SupplierId, dialog.Status, dialog.OrderDate, dialog.ExpectedDate, dialog.Notes, lines);
                MessageBox.Show("Purchase order created from reorder.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Fills every dashboard chart from the same ReportMetrics the KPI cards above them were
        /// read from, so the two can never disagree. Micro only gets the two charts it has
        /// (Revenue Trend, Bookings by Status) - the rest aren't in its grid at all.
        /// </summary>
        private void ApplyCharts(ReportMetrics metrics)
        {
            _revenueTrendChart.SetData(metrics.RevenueByMonth);
            _bookingStatusChart.SetData(metrics.BookingsByStatus);

            if (_isMicroTier) return;

            _expenseBreakdownChart.SetData(metrics.ExpensesByCategory);
            _equipmentStatusChart.SetData(metrics.EquipmentStatus);
            _lowStockChart.SetData(metrics.LowStockWorstFirst);

            _showBranchComparison = metrics.BranchNetIncome is not null;
            _branchComparisonChart.Visible = _showBranchComparison;
            if (metrics.BranchNetIncome is not null) _branchComparisonChart.SetData(metrics.BranchNetIncome);
        }

        // Tracked separately from _branchComparisonChart.Visible, which also reads false whenever
        // the chart's tab simply isn't on screen - the PDF must follow the data, not the tab.
        private bool _showBranchComparison;

        /// <summary>Logical size each chart is laid out at for the PDF - half an A4 content width
        /// at screen DPI, so a pair sits side by side with room for donut legends.</summary>
        private const int PdfChartWidth = 520;
        private const int PdfChartHeight = 280;

        /// <summary>The same charts this screen shows, in the same order, as images for the PDF.</summary>
        private List<byte[]> RenderChartsForPdf()
        {
            var charts = new List<ChartPanel> { _revenueTrendChart, _bookingStatusChart };
            if (!_isMicroTier)
            {
                charts.Add(_expenseBreakdownChart);
                charts.Add(_equipmentStatusChart);
                charts.Add(_lowStockChart);
                if (_showBranchComparison) charts.Add(_branchComparisonChart);
            }

            return charts.Select(c => c.RenderToPng(PdfChartWidth, PdfChartHeight)).ToList();
        }

        /// <summary>
        /// Read-only audit trail: newest first, filterable by entity, actor, and (reusing the
        /// page's own From/To date range) date - no edit/delete here, since an audit trail that
        /// could be altered from its own viewer isn't a trail.
        /// </summary>
        private TabPage BuildAuditLogTab()
        {
            var page = new TabPage("Audit Log") { BackColor = AppTheme.WhiteColor };

            AppTheme.StyleGrid(_auditLogGrid);
            _auditLogGrid.Dock = DockStyle.Fill;
            _auditLogGrid.ReadOnly = true;
            _auditLogGrid.AutoGenerateColumns = true;
            _auditLogGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _auditLogGrid.MultiSelect = false;
            AppTheme.StyleStatusColumn(_auditLogGrid, "Action", value => (value?.ToString() ?? string.Empty) switch
            {
                "Create" => ("Create", StatusKind.Success),
                "Update" => ("Update", StatusKind.Info),
                "Delete" => ("Delete", StatusKind.Danger),
                var other => (other, StatusKind.Neutral)
            });

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.WhiteColor
            };

            _auditEntityFilter.Width = 160;
            _auditEntityFilter.Margin = new Padding(0, 0, 8, 0);
            _auditEntityFilter.Items.Add("All Modules");
            foreach (var entity in AuditableEntityNames) _auditEntityFilter.Items.Add(entity);
            _auditEntityFilter.SelectedIndex = 0;

            _auditActorFilter.Width = 180;
            _auditActorFilter.Margin = new Padding(0, 0, 8, 0);
            _auditActorFilter.Items.Add("All Actors");
            _auditActorFilter.SelectedIndex = 0;

            _auditApply.Width = 90;
            _auditApply.Click += (_, _) => LoadData();

            toolbar.Controls.Add(_auditEntityFilter);
            toolbar.Controls.Add(_auditActorFilter);
            toolbar.Controls.Add(_auditApply);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, AppTheme.ToolbarRowHeight));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(_auditLogGrid, 0, 1);
            page.Controls.Add(layout);

            return page;
        }

        private static readonly string[] AuditableEntityNames =
        {
            "Equipment", "Inventory", "Supplier", "Payment", "Expense", "SalaryPayment",
            "TenantUser", "Company", "ProductSale", "PurchaseOrder", "OwnerTransaction", "Booking", "Customer"
        };

        private void LoadData()
        {
            try
            {
                using var db = CreateDbContext();

                var from = _fromDate.Value.Date;
                var toExclusive = _toDate.Value.Date.AddDays(1);

                // Apply branch filter for report-level scoping. Branch-bound users
                // always get their own branch; company-wide roles use the picker.
                var branchId = BranchFilter.Resolve(BranchFilter.SelectedId(_reportBranchPicker));

                // Every KPI and chart figure comes from ReportMetrics - the same computation the
                // home Dashboard uses, so the two screens can never show different numbers.
                var metrics = ReportMetrics.Compute(db, from, toExclusive, branchId, includeExtended: !_isMicroTier);
                var bookingsInRange = metrics.BookingRows;

                _totalIncomeValue.Text = $"₱{metrics.TotalIncome:N2}";
                _bookingsValue.Text = metrics.BookingsInRange.ToString();
                _paymentsValue.Text = metrics.PaymentsInRange.ToString();
                if (!_isMicroTier)
                {
                    _totalSalaryValue.Text = $"₱{metrics.TotalSalaryPaid:N2}";
                    _totalExpensesValue.Text = $"₱{metrics.TotalExpenses:N2}";
                }

                ApplyCharts(metrics);

                // Outstanding Payments: sum ALL payments ever made against each booking in range
                // (not just ones dated inside the window), since a booking's balance depends on its
                // full payment history - a booking can have several partial payments recorded on
                // different dates, only some of which may fall in the selected range.
                var bookingIds = bookingsInRange.Select(b => b.BookingId).ToList();
                var paidTotalsByBooking = db.Payments
                    .Where(p => !p.IsDeleted && bookingIds.Contains(p.BookingId))
                    .GroupBy(p => p.BookingId)
                    .ToDictionary(g => g.Key, g => g.Sum(p => p.AmountPaid));
                var addOnsByBooking = AddOnsService.GetAddOnsTotals(db, bookingIds);

                var outstandingRows = bookingsInRange
                    .Select(b => new OutstandingRow
                    {
                        BookingId = b.BookingId,
                        Customer = b.Customer?.FullName ?? $"Customer #{b.CustomerId}",
                        BookingDate = b.SessionDate,
                        PriceSnapshot = b.PriceSnapshot + addOnsByBooking.GetValueOrDefault(b.BookingId),
                        AmountPaid = paidTotalsByBooking.GetValueOrDefault(b.BookingId),
                        AmountDue = b.PriceSnapshot + addOnsByBooking.GetValueOrDefault(b.BookingId) - paidTotalsByBooking.GetValueOrDefault(b.BookingId)
                    })
                    .Where(r => r.AmountDue > 0)
                    .OrderByDescending(r => r.AmountDue)
                    .ToList();
                _outstandingGrid.DataSource = outstandingRows;

                if (!_isMicroTier)
                {
                    _equipmentUsageGrid.DataSource = metrics.EquipmentUsageRows;
                    _lowStockGrid.DataSource = metrics.LowStockRows;

                    RenderIncomeStatement(db, from, _toDate.Value.Date, branchId);
                    RenderBalanceSheet(db, _toDate.Value.Date, branchId);
                    RenderCashFlowStatement(db, from, _toDate.Value.Date, branchId);
                    LoadAuditLog(db, from, toExclusive, branchId);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RenderIncomeStatement(TenantErpDbContext db, DateTime from, DateTime toInclusive, int? branchId)
        {
            var result = FinancialStatementService.ComputeIncomeStatement(db, from, toInclusive, branchId);
            _incomeStatementResult = result;

            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            flow.Controls.Add(BuildStatementLine("Revenue (Payments + Product Sales)", result.Revenue));
            flow.Controls.Add(BuildStatementLine("Less: Operating Expenses", -result.OperatingExpenses, indentPx: 12));
            flow.Controls.Add(BuildStatementLine("Less: Salary & Wages", -result.SalaryWages, indentPx: 12));
            flow.Controls.Add(BuildStatementRule());
            flow.Controls.Add(BuildStatementLine("Net Income", result.NetIncome, bold: true));

            _incomeStatementHost.Controls.Clear();
            _incomeStatementHost.Controls.Add(flow);
        }

        private void RenderBalanceSheet(TenantErpDbContext db, DateTime asOfDate, int? branchId)
        {
            var result = FinancialStatementService.ComputeBalanceSheet(db, asOfDate, branchId);
            _balanceSheetResult = result;
            const int colWidth = 300;

            var assetsFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 12, 0) };
            assetsFlow.Controls.Add(BuildStatementLine("ASSETS", null, bold: true, width: colWidth));
            assetsFlow.Controls.Add(BuildStatementLine("Cash", result.Cash, indentPx: 12, width: colWidth));
            assetsFlow.Controls.Add(BuildStatementLine("Accounts Receivable", result.AccountsReceivable, indentPx: 12, width: colWidth));
            assetsFlow.Controls.Add(BuildStatementLine("Inventory (at cost)", result.Inventory, indentPx: 12, width: colWidth));
            assetsFlow.Controls.Add(BuildStatementLine("Equipment (at cost)", result.Equipment, indentPx: 12, width: colWidth));
            assetsFlow.Controls.Add(BuildStatementRule(colWidth));
            assetsFlow.Controls.Add(BuildStatementLine("Total Assets", result.TotalAssets, bold: true, width: colWidth));

            var liabFlow = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            liabFlow.Controls.Add(BuildStatementLine("LIABILITIES", null, bold: true, width: colWidth));
            liabFlow.Controls.Add(BuildStatementLine("Accounts Payable", result.AccountsPayable, indentPx: 12, width: colWidth));
            liabFlow.Controls.Add(BuildStatementLine(string.Empty, null, width: colWidth));
            liabFlow.Controls.Add(BuildStatementLine("EQUITY", null, bold: true, width: colWidth));
            liabFlow.Controls.Add(BuildStatementLine("Owner's Capital", result.OwnersCapital, indentPx: 12, width: colWidth));
            liabFlow.Controls.Add(BuildStatementLine("Retained Earnings", result.RetainedEarnings, indentPx: 12, width: colWidth));
            liabFlow.Controls.Add(BuildStatementLine("Total Equity", result.TotalEquity, bold: true, indentPx: 12, width: colWidth));
            liabFlow.Controls.Add(BuildStatementRule(colWidth));
            liabFlow.Controls.Add(BuildStatementLine("Total Liabilities + Equity", result.TotalLiabilitiesAndEquity, bold: true, width: colWidth));

            var isBalanced = result.TotalAssets == result.TotalLiabilitiesAndEquity;
            var balancedNotice = new Label
            {
                Text = isBalanced ? "Balanced (Assets = Liabilities + Equity)" : $"Out of balance by ₱{Math.Abs(result.TotalAssets - result.TotalLiabilitiesAndEquity):N2}",
                ForeColor = isBalanced ? AppTheme.SuccessColor : AppTheme.DangerColor,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                AutoSize = true,
                Dock = DockStyle.Bottom,
                Margin = new Padding(0, 8, 0, 0)
            };

            var columns = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, AutoSize = true };
            columns.Controls.Add(assetsFlow, 0, 0);
            columns.Controls.Add(liabFlow, 1, 0);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.Controls.Add(columns, 0, 0);
            layout.Controls.Add(balancedNotice, 0, 1);

            _balanceSheetHost.Controls.Clear();
            _balanceSheetHost.Controls.Add(layout);
        }

        private void RenderCashFlowStatement(TenantErpDbContext db, DateTime from, DateTime toInclusive, int? branchId)
        {
            var result = FinancialStatementService.ComputeCashFlowStatement(db, from, toInclusive, branchId);
            _cashFlowStatementResult = result;

            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            flow.Controls.Add(BuildStatementLine("Net Income", result.NetIncome, bold: true));
            flow.Controls.Add(BuildStatementLine("+/- Change in Accounts Receivable", result.AccountsReceivableChange, indentPx: 12));
            flow.Controls.Add(BuildStatementLine("+/- Change in Accounts Payable", result.AccountsPayableChange, indentPx: 12));
            flow.Controls.Add(BuildStatementLine("+/- Change in Inventory", result.InventoryChange, indentPx: 12));
            flow.Controls.Add(BuildStatementRule());
            flow.Controls.Add(BuildStatementLine("Net Cash from Operating Activities", result.NetCashFromOperations, bold: true));
            flow.Controls.Add(BuildStatementLine("Equipment purchased (Investing)", -result.EquipmentPurchased, indentPx: 12));
            flow.Controls.Add(BuildStatementLine("Owner Contributions/Withdrawals (Financing)", result.OwnerFinancingNet, indentPx: 12));
            flow.Controls.Add(BuildStatementRule());
            flow.Controls.Add(BuildStatementLine("Net Change in Cash", result.NetChangeInCash, bold: true));
            flow.Controls.Add(BuildStatementLine("Beginning Cash", result.BeginningCash));
            flow.Controls.Add(BuildStatementLine("Ending Cash", result.EndingCash, bold: true));

            _cashFlowHost.Controls.Clear();
            _cashFlowHost.Controls.Add(flow);
        }

        private static Control BuildStatementLine(string label, decimal? value, bool bold = false, int indentPx = 0, int width = 520)
        {
            var row = new TableLayoutPanel { Width = width, Height = 26, ColumnCount = 2, RowCount = 1, Margin = new Padding(indentPx, 0, 0, 0) };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

            var font = bold ? new Font("Segoe UI", 9.5f, FontStyle.Bold) : AppTheme.BodyFont;
            row.Controls.Add(new Label { Text = label, Font = font, ForeColor = AppTheme.TextPrimaryColor, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true }, 0, 0);
            row.Controls.Add(new Label
            {
                Text = value is null ? string.Empty : value < 0 ? $"(₱{-value:N2})" : $"₱{value:N2}",
                Font = font,
                ForeColor = AppTheme.TextPrimaryColor,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight
            }, 1, 0);
            return row;
        }

        private static Control BuildStatementRule(int width = 520) =>
            new Panel { Width = width, Height = 1, BackColor = AppTheme.BorderColor, Margin = new Padding(0, 4, 0, 4) };

        private void LoadAuditLog(TenantErpDbContext db, DateTime from, DateTime toExclusive, int? branchId)
        {
            // AuditLog inherits its branch from the entity it records, so a branch view shows
            // only that branch's activity plus the company-wide entries that carry no branch.
            var query = db.AuditLogs.Where(a => a.Timestamp >= from && a.Timestamp < toExclusive
                                                && (branchId == null || a.BranchId == null || a.BranchId == branchId));

            if (_auditEntityFilter.SelectedItem is string entityName && entityName != "All Modules")
            {
                query = query.Where(a => a.EntityName == entityName);
            }

            if (_auditActorFilter.SelectedItem is string actorName && actorName != "All Actors")
            {
                query = query.Where(a => a.ActorName == actorName);
            }

            _auditLogGrid.DataSource = query
                .OrderByDescending(a => a.Timestamp)
                .Select(a => new AuditLogRow
                {
                    Timestamp = a.Timestamp,
                    Actor = a.ActorName,
                    Entity = a.EntityName,
                    EntityId = a.EntityId,
                    Action = a.Action.ToString(),
                    Summary = a.Summary ?? string.Empty
                })
                .ToList();

            // Refresh the actor filter's options from what's actually in the log, preserving the
            // current selection so re-filtering doesn't silently reset it back to "All Actors".
            var previousActor = _auditActorFilter.SelectedItem as string;
            var actors = db.AuditLogs.Select(a => a.ActorName).Distinct().OrderBy(a => a).ToList();
            _auditActorFilter.Items.Clear();
            _auditActorFilter.Items.Add("All Actors");
            foreach (var actor in actors) _auditActorFilter.Items.Add(actor);
            _auditActorFilter.SelectedItem = previousActor is not null && _auditActorFilter.Items.Contains(previousActor) ? previousActor : "All Actors";
        }

        private void GenerateReportPdf()
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "PDF Document (*.pdf)|*.pdf",
                FileName = $"Reports_{_fromDate.Value:yyyyMMdd}-{_toDate.Value:yyyyMMdd}.pdf"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                BuildReportDocument().GeneratePdf(dialog.FileName);

                var open = MessageBox.Show("Report saved. Open it now?", "Generate Report", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (open == DialogResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(dialog.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not generate the report.\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Gathers the data this View already has loaded (bound grid rows, stored statement
        /// results, summary label text) into the plain shape ReportDocumentBuilder expects, then
        /// hands off to it - this method decides *what* goes in the report, never *how* a PDF page
        /// is laid out.
        /// </summary>
        private IDocument BuildReportDocument()
        {
            var summaryStats = new List<ReportDocumentBuilder.SummaryStat>
            {
                new("Total Income", _totalIncomeValue.Text),
                new("Bookings in Range", _bookingsValue.Text),
                new("Payments in Range", _paymentsValue.Text)
            };
            if (!_isMicroTier)
            {
                summaryStats.Add(new("Total Salary Paid", _totalSalaryValue.Text));
                summaryStats.Add(new("Total Expenses", _totalExpensesValue.Text));
            }

            return ReportDocumentBuilder.Build(
                _fromDate.Value.Date,
                _toDate.Value.Date,
                AppSession.CurrentCompanyCode,
                AppSession.CurrentDisplayName ?? AppSession.CurrentUsername,
                includeExtendedSections: !_isMicroTier,
                BranchLabelForReport(),
                summaryStats,
                RenderChartsForPdf(),
                _outstandingGrid.DataSource as List<OutstandingRow> ?? new(),
                _equipmentUsageGrid.DataSource as List<EquipmentUsageRow> ?? new(),
                _lowStockGrid.DataSource as List<LowStockRow> ?? new(),
                _auditLogGrid.DataSource as List<AuditLogRow> ?? new(),
                _incomeStatementResult,
                _balanceSheetResult,
                _cashFlowStatementResult);
        }

        /// <summary>
        /// How the PDF names the branch it covers, or null below Medium tier where there is no
        /// branch concept to name.
        /// </summary>
        private string? BranchLabelForReport()
        {
            if (!AppSession.IsBranchAware) return null;

            var branchId = BranchFilter.Resolve(BranchFilter.SelectedId(_reportBranchPicker));
            if (branchId is null) return BranchFilter.AllBranchesLabel;

            var name = new BranchPresenter().GetActiveBranches()
                .FirstOrDefault(b => b.BranchId == branchId.Value)?.BranchName;
            return name ?? $"Branch #{branchId.Value}";
        }

        private static TenantErpDbContext CreateDbContext()
        {
            var connectionString = AppSession.CurrentTenantConnectionString
                ?? AppDbConfig.TenantErpConnectionString;

            var options = new DbContextOptionsBuilder<TenantErpDbContext>()
                .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3))
                .Options;

            return new TenantErpDbContext(options);
        }

        // Internal (not private) so ReportDocumentBuilder - the Controller/Service that lays these
        // out as PDF content - can accept them as parameters without this View leaking further
        // than its own project.
        internal sealed class OutstandingRow
        {
            public int BookingId { get; set; }
            public string Customer { get; set; } = string.Empty;
            public DateTime BookingDate { get; set; }
            public decimal PriceSnapshot { get; set; }
            public decimal AmountPaid { get; set; }
            public decimal AmountDue { get; set; }
        }

        internal sealed class EquipmentUsageRow
        {
            public string Status { get; set; } = string.Empty;
            public int Count { get; set; }
        }

        internal sealed class LowStockRow
        {
            public int InventoryId { get; set; }
            public string ItemName { get; set; } = string.Empty;
            public decimal QuantityOnHand { get; set; }
            public decimal ReorderLevel { get; set; }
            public string Supplier { get; set; } = string.Empty;
        }

        internal sealed class AuditLogRow
        {
            public DateTime Timestamp { get; set; }
            public string Actor { get; set; } = string.Empty;
            public string Entity { get; set; } = string.Empty;
            public int EntityId { get; set; }
            public string Action { get; set; } = string.Empty;
            public string Summary { get; set; } = string.Empty;
        }
    }
}
