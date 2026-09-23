using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.Controllers;
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
    /// </summary>
    public class ReportsForm : UserControl
    {
        private readonly InventoryController _inventoryController = new();

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
        /// Outstanding Payments, Equipment Usage, Low Stock Alerts, Income Statement, Balance
        /// Sheet, Cash Flow Statement, and Audit Log - the full 7-tab Reports module - on one card
        /// rather than stacking separate grids vertically.
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
                tabs.TabPages.Add(BuildLowStockTab());
                tabs.TabPages.Add(BuildStatementTab("Income Statement", _incomeStatementHost));
                tabs.TabPages.Add(BuildStatementTab("Balance Sheet", _balanceSheetHost));
                tabs.TabPages.Add(BuildStatementTab("Cash Flow Statement", _cashFlowHost));
                tabs.TabPages.Add(BuildAuditLogTab());
            }

            card.Controls.Add(tabs);
            return card;
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
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
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
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
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

                // Revenue Summary: income actually collected in the window (by PaymentDate) versus
                // bookings scheduled in the window (by SessionDate) - the gap between the two
                // counts is how missing/partial payments show up at a glance.
                var paymentsInRange = db.Payments
                    .Where(p => !p.IsDeleted && p.PaymentDate >= from && p.PaymentDate < toExclusive)
                    .ToList();

                var productSalesInRange = db.ProductSales
                    .Where(p => !p.IsDeleted && p.SaleDate >= from && p.SaleDate < toExclusive)
                    .ToList();

                var bookingsInRange = db.Bookings
                    .Include(b => b.Customer)
                    .Where(b => b.Status != BookingStatus.Cancelled && b.SessionDate >= from && b.SessionDate < toExclusive)
                    .OrderBy(b => b.SessionDate)
                    .ToList();

                // Total Income = Payments + Product Sales, so it never silently undercounts real
                // revenue now that Product Sales exist as a second revenue stream.
                _totalIncomeValue.Text = $"₱{paymentsInRange.Sum(p => p.AmountPaid) + productSalesInRange.Sum(p => p.TotalAmount):N2}";
                _bookingsValue.Text = bookingsInRange.Count.ToString();
                _paymentsValue.Text = paymentsInRange.Count.ToString();

                if (!_isMicroTier)
                {
                    var salaryPaidInRange = db.SalaryPayments
                        .Where(s => !s.IsDeleted && s.PaymentDate >= from && s.PaymentDate < toExclusive)
                        .Sum(s => (decimal?)s.NetAmount) ?? 0m;

                    var expensesInRange = db.Expenses
                        .Where(e => !e.IsDeleted && e.ExpenseDate >= from && e.ExpenseDate < toExclusive)
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
                            InventoryId = i.InventoryId,
                            ItemName = i.ItemName,
                            QuantityOnHand = i.QuantityOnHand,
                            ReorderLevel = i.ReorderLevel,
                            Supplier = i.Supplier != null ? i.Supplier.SupplierName : string.Empty
                        })
                        .ToList();

                    RenderIncomeStatement(db, from, _toDate.Value.Date);
                    RenderBalanceSheet(db, _toDate.Value.Date);
                    RenderCashFlowStatement(db, from, _toDate.Value.Date);
                    LoadAuditLog(db, from, toExclusive);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RenderIncomeStatement(TenantErpDbContext db, DateTime from, DateTime toInclusive)
        {
            var result = FinancialStatementService.ComputeIncomeStatement(db, from, toInclusive);

            var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            flow.Controls.Add(BuildStatementLine("Revenue (Payments + Product Sales)", result.Revenue));
            flow.Controls.Add(BuildStatementLine("Less: Operating Expenses", -result.OperatingExpenses, indentPx: 12));
            flow.Controls.Add(BuildStatementLine("Less: Salary & Wages", -result.SalaryWages, indentPx: 12));
            flow.Controls.Add(BuildStatementRule());
            flow.Controls.Add(BuildStatementLine("Net Income", result.NetIncome, bold: true));

            _incomeStatementHost.Controls.Clear();
            _incomeStatementHost.Controls.Add(flow);
        }

        private void RenderBalanceSheet(TenantErpDbContext db, DateTime asOfDate)
        {
            var result = FinancialStatementService.ComputeBalanceSheet(db, asOfDate);
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

        private void RenderCashFlowStatement(TenantErpDbContext db, DateTime from, DateTime toInclusive)
        {
            var result = FinancialStatementService.ComputeCashFlowStatement(db, from, toInclusive);

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

        private void LoadAuditLog(TenantErpDbContext db, DateTime from, DateTime toExclusive)
        {
            var query = db.AuditLogs.Where(a => a.Timestamp >= from && a.Timestamp < toExclusive);

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
            public int InventoryId { get; set; }
            public string ItemName { get; set; } = string.Empty;
            public decimal QuantityOnHand { get; set; }
            public decimal ReorderLevel { get; set; }
            public string Supplier { get; set; } = string.Empty;
        }

        private sealed class AuditLogRow
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
