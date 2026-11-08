using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP
{
    /// <summary>
    /// Finance Management: Expenses and Salary Payments on two tabs of one screen. Neither entity
    /// carries an IsActive/IsDeleted flag, so Delete here is a real row removal (unlike the rest of
    /// the app's soft-delete convention).
    /// </summary>
    public class FinanceForm : UserControl
    {
        private const string PayColumnName = "PayAction";

        private bool _canEdit;

        // Branch picker is on the Expenses tab only - Salary and Owner Transactions are
        // company-wide aggregates that are not split by branch.
        private readonly RoundedComboBox _expenseBranchPicker = new();

        private readonly DataGridView _expenseGrid = new();
        private readonly RoundedButton _addExpense = new();
        private readonly RoundedButton _editExpense = new() { IsPrimary = false };
        private readonly RoundedButton _deleteExpense = new() { IsPrimary = false };

        private readonly DataGridView _salaryGrid = new();
        private readonly RoundedButton _addSalary = new();
        private readonly RoundedButton _editSalary = new() { IsPrimary = false };
        private readonly RoundedButton _deleteSalary = new() { IsPrimary = false };

        private readonly DataGridView _ownerTransactionGrid = new();
        private readonly RoundedButton _addOwnerTransaction = new();
        private readonly RoundedButton _editOwnerTransaction = new() { IsPrimary = false };
        private readonly RoundedButton _deleteOwnerTransaction = new() { IsPrimary = false };

        public FinanceForm(AccessLevel accessLevel = AccessLevel.Full)
        {
            Dock = DockStyle.Fill;
            BackColor = AppTheme.BackgroundColor;

            var canEdit = accessLevel is AccessLevel.Manage or AccessLevel.Full;
            _canEdit = canEdit;

            // A BranchManager's Finance access is Expenses only. Payroll and owner capital are
            // company-wide concerns that sit above a single branch, so those two tabs are never
            // built for them - not merely disabled, since a disabled control is still a control
            // whose data was already loaded.
            var canSeeCompanyWideFinance = AppSession.CurrentRole != SystemRole.BranchManager;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            AppTheme.StyleTabControl(tabs);
            tabs.TabPages.Add(BuildExpenseTab(canEdit));
            if (canSeeCompanyWideFinance)
            {
                tabs.TabPages.Add(BuildSalaryTab(canEdit));
                tabs.TabPages.Add(BuildOwnerTransactionTab(canEdit));
            }

            var description = canSeeCompanyWideFinance
                ? "Manage business expenses, salary payments, and owner transactions."
                : "Manage your branch's operating expenses.";
            var header = AppTheme.CreatePageHeader("Finance", description);
            Controls.Add(AppTheme.BuildPage(header, null, AppTheme.WrapContent(tabs)));

            LoadExpenses();
            if (canSeeCompanyWideFinance)
            {
                LoadSalaryPayments();
                LoadOwnerTransactions();
            }
        }

        private TabPage BuildExpenseTab(bool canEdit)
        {
            var page = new TabPage("Expenses") { BackColor = AppTheme.WhiteColor };

            AppTheme.StyleGrid(_expenseGrid);
            _expenseGrid.Dock = DockStyle.Fill;
            _expenseGrid.ReadOnly = true;
            _expenseGrid.AutoGenerateColumns = true;
            _expenseGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _expenseGrid.MultiSelect = false;
            AppTheme.StyleStatusColumn(_expenseGrid, "PaidStatus", value =>
                value is true ? ("Paid", StatusKind.Success) : ("Unpaid (Accounts Payable)", StatusKind.Warning));

            _expenseGrid.CellFormatting += ExpenseGrid_CellFormatting;
            _expenseGrid.CellContentClick += ExpenseGrid_CellContentClick;

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.WhiteColor
            };

            _addExpense.Text = "+ Add Expense";
            _editExpense.Text = "Edit";
            _deleteExpense.Text = "Delete";
            _addExpense.Width = 130;
            _editExpense.Width = 90;
            _deleteExpense.Width = 90;
            _addExpense.Margin = new Padding(0, 0, 8, 0);
            _editExpense.Margin = new Padding(0, 0, 8, 0);

            _addExpense.Click += (_, _) => AddExpense();
            _editExpense.Click += (_, _) => EditExpense();
            _deleteExpense.Click += (_, _) => DeleteExpense();

            BranchFilter.Populate(_expenseBranchPicker);
            _expenseBranchPicker.Width = 170;
            _expenseBranchPicker.Margin = new Padding(8, 0, 0, 0);
            _expenseBranchPicker.SelectedIndexChanged += (_, _) => LoadExpenses();

            toolbar.Controls.Add(_addExpense);
            toolbar.Controls.Add(_editExpense);
            toolbar.Controls.Add(_deleteExpense);
            toolbar.Controls.Add(_expenseBranchPicker);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, AppTheme.ToolbarRowHeight));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(_expenseGrid, 0, 1);
            page.Controls.Add(layout);

            _addExpense.Enabled = canEdit;
            _editExpense.Enabled = canEdit;
            _deleteExpense.Enabled = canEdit;

            return page;
        }

        private TabPage BuildSalaryTab(bool canEdit)
        {
            var page = new TabPage("Salary Payments") { BackColor = AppTheme.WhiteColor };

            AppTheme.StyleGrid(_salaryGrid);
            _salaryGrid.Dock = DockStyle.Fill;
            _salaryGrid.ReadOnly = true;
            // Explicit columns, unlike the other Finance grids: auto-generating one per property
            // pushed Net Pay and the dates off the right edge behind the deduction breakdown. The
            // full breakdown is still a click away via Edit.
            _salaryGrid.AutoGenerateColumns = false;
            _salaryGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _salaryGrid.MultiSelect = false;

            _salaryGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Employee", DataPropertyName = "Employee", HeaderText = "Employee", FillWeight = 140 });
            _salaryGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "GrossAmount",
                DataPropertyName = "GrossAmount",
                HeaderText = "Gross",
                FillWeight = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            _salaryGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "NetAmount",
                DataPropertyName = "NetAmount",
                HeaderText = "Net Pay",
                FillWeight = 95,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "N2", Alignment = DataGridViewContentAlignment.MiddleRight }
            });
            _salaryGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PayPeriod", DataPropertyName = "PayPeriod", HeaderText = "Pay Period", FillWeight = 130 });
            _salaryGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PaidStatus", DataPropertyName = "PaidStatus", HeaderText = "Status", FillWeight = 90 });
            _salaryGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "PaidOn", DataPropertyName = "PaidOn", HeaderText = "Date Paid", FillWeight = 105 });

            AppTheme.StyleStatusColumn(_salaryGrid, "PaidStatus", value =>
                value?.ToString() == "Paid" ? ("Paid", StatusKind.Success) : ("Unpaid", StatusKind.Warning));

            _salaryGrid.CellFormatting += SalaryGrid_CellFormatting;
            _salaryGrid.CellContentClick += SalaryGrid_CellContentClick;

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.WhiteColor
            };

            _addSalary.Text = "+ Run Payroll";
            _editSalary.Text = "Edit";
            _deleteSalary.Text = "Delete";
            _addSalary.Width = 130;
            _editSalary.Width = 90;
            _deleteSalary.Width = 90;
            _addSalary.Margin = new Padding(0, 0, 8, 0);
            _editSalary.Margin = new Padding(0, 0, 8, 0);

            _addSalary.Click += (_, _) => RunPayroll();
            _editSalary.Click += (_, _) => EditSalaryPayment();
            _deleteSalary.Click += (_, _) => DeleteSalaryPayment();

            toolbar.Controls.Add(_addSalary);
            toolbar.Controls.Add(_editSalary);
            toolbar.Controls.Add(_deleteSalary);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, AppTheme.ToolbarRowHeight));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(_salaryGrid, 0, 1);
            page.Controls.Add(layout);

            _addSalary.Enabled = canEdit;
            _editSalary.Enabled = canEdit;
            _deleteSalary.Enabled = canEdit;

            return page;
        }

        private TabPage BuildOwnerTransactionTab(bool canEdit)
        {
            var page = new TabPage("Owner Transactions") { BackColor = AppTheme.WhiteColor };

            AppTheme.StyleGrid(_ownerTransactionGrid);
            _ownerTransactionGrid.Dock = DockStyle.Fill;
            _ownerTransactionGrid.ReadOnly = true;
            _ownerTransactionGrid.AutoGenerateColumns = true;
            _ownerTransactionGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _ownerTransactionGrid.MultiSelect = false;
            AppTheme.StyleStatusColumn(_ownerTransactionGrid, "Type", value => (value?.ToString() ?? string.Empty) switch
            {
                "Contribution" => ("Contribution", StatusKind.Success),
                "Withdrawal" => ("Withdrawal", StatusKind.Warning),
                var other => (other, StatusKind.Neutral)
            });

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.WhiteColor
            };

            _addOwnerTransaction.Text = "+ Add Transaction";
            _editOwnerTransaction.Text = "Edit";
            _deleteOwnerTransaction.Text = "Delete";
            _addOwnerTransaction.Width = 150;
            _editOwnerTransaction.Width = 90;
            _deleteOwnerTransaction.Width = 90;
            _addOwnerTransaction.Margin = new Padding(0, 0, 8, 0);
            _editOwnerTransaction.Margin = new Padding(0, 0, 8, 0);

            _addOwnerTransaction.Click += (_, _) => AddOwnerTransaction();
            _editOwnerTransaction.Click += (_, _) => EditOwnerTransaction();
            _deleteOwnerTransaction.Click += (_, _) => DeleteOwnerTransaction();

            toolbar.Controls.Add(_addOwnerTransaction);
            toolbar.Controls.Add(_editOwnerTransaction);
            toolbar.Controls.Add(_deleteOwnerTransaction);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, AppTheme.ToolbarRowHeight));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(_ownerTransactionGrid, 0, 1);
            page.Controls.Add(layout);

            _addOwnerTransaction.Enabled = canEdit;
            _editOwnerTransaction.Enabled = canEdit;
            _deleteOwnerTransaction.Enabled = canEdit;

            return page;
        }

        private void LoadOwnerTransactions()
        {
            try
            {
                using var db = CreateDbContext();
                _ownerTransactionGrid.DataSource = db.OwnerTransactions
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.OwnerTransactionId)
                    .Select(x => new OwnerTransactionRow
                    {
                        OwnerTransactionId = x.OwnerTransactionId,
                        Type = x.Type.ToString(),
                        Amount = x.Amount,
                        TransactionDate = x.TransactionDate,
                        Notes = x.Notes ?? string.Empty
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AddOwnerTransaction()
        {
            try
            {
                AddOwnerTransactionCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not record this owner transaction:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddOwnerTransactionCore()
        {
            using var dialog = new OwnerTransactionDialogForm(existing: null);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            using var db = CreateDbContext();
            db.OwnerTransactions.Add(new OwnerTransaction
            {
                Type = dialog.Type,
                Amount = dialog.Amount,
                TransactionDate = dialog.TransactionDate,
                Notes = dialog.Notes,
                CreatedAt = DateTime.UtcNow
            });
            TenantSyncService.SaveAndSync(db);
            LoadOwnerTransactions();

            MessageBox.Show("Owner transaction recorded successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditOwnerTransaction()
        {
            try
            {
                EditOwnerTransactionCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not update this owner transaction:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EditOwnerTransactionCore()
        {
            if (_ownerTransactionGrid.SelectedRows.Count == 0) return;

            var selected = (OwnerTransactionRow)_ownerTransactionGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            using var db = CreateDbContext();
            var entity = db.OwnerTransactions.Find(selected.OwnerTransactionId);
            if (entity is null) return;

            using var dialog = new OwnerTransactionDialogForm(entity);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            entity.Type = dialog.Type;
            entity.Amount = dialog.Amount;
            entity.TransactionDate = dialog.TransactionDate;
            entity.Notes = dialog.Notes;

            TenantSyncService.SaveAndSync(db);
            LoadOwnerTransactions();
        }

        private void DeleteOwnerTransaction()
        {
            if (_ownerTransactionGrid.SelectedRows.Count == 0) return;

            var selected = (OwnerTransactionRow)_ownerTransactionGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            if (MessageBox.Show(
                    $"Delete this {selected.Type.ToLowerInvariant()} of ₱{selected.Amount:N2}? This cannot be undone.",
                    "Delete Owner Transaction", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                using var db = CreateDbContext();
                var entity = db.OwnerTransactions.Find(selected.OwnerTransactionId);
                if (entity is null) return;

                db.OwnerTransactions.Remove(entity);
                TenantSyncService.SaveAndSync(db);
                LoadOwnerTransactions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not delete this owner transaction:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

        private void LoadExpenses()
        {
            try
            {
                var branchId = BranchFilter.Resolve(BranchFilter.SelectedId(_expenseBranchPicker));
                using var db = CreateDbContext();
                _expenseGrid.DataSource = db.Expenses
                    .Include(x => x.Supplier)
                    .Include(x => x.Equipment)
                    .Where(x => !x.IsDeleted)
                    .Where(x => branchId == null || x.BranchId == branchId)
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.ExpenseId)
                    .Select(x => new ExpenseRow
                    {
                        ExpenseId = x.ExpenseId,
                        Category = x.Category.ToString(),
                        Description = x.Description ?? string.Empty,
                        Amount = x.Amount,
                        ExpenseDate = x.ExpenseDate,
                        Supplier = x.Supplier != null ? x.Supplier.SupplierName : string.Empty,
                        Equipment = x.Equipment != null ? x.Equipment.EquipmentName : string.Empty,
                        PaidStatus = x.IsPaid,
                        PurchaseOrderId = x.PurchaseOrderId
                    })
                    .ToList();

                AddPayColumn();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// Re-adds the Pay action column after every rebind. The grid auto-generates its columns,
        /// which drops and recreates them on each DataSource assignment; removing and re-adding
        /// this one keeps it reliably last instead of drifting left as the bound columns come back.
        /// </summary>
        private void AddPayColumn()
        {
            if (_expenseGrid.Columns.Contains(PayColumnName))
            {
                _expenseGrid.Columns.Remove(PayColumnName);
            }

            _expenseGrid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = PayColumnName,
                HeaderText = "",
                UseColumnTextForButtonValue = false,
                FlatStyle = FlatStyle.Flat,
                Text = "Pay"
            });
        }

        private void AddSalaryPayColumn()
        {
            if (_salaryGrid.Columns.Contains(PayColumnName))
            {
                _salaryGrid.Columns.Remove(PayColumnName);
            }

            _salaryGrid.Columns.Add(new DataGridViewButtonColumn
            {
                Name = PayColumnName,
                HeaderText = "",
                UseColumnTextForButtonValue = false,
                FlatStyle = FlatStyle.Flat,
                Text = "Pay",
                FillWeight = 70
            });
        }

        private void SalaryGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_salaryGrid.Columns[e.ColumnIndex].Name != PayColumnName) return;
            if (_salaryGrid.Rows[e.RowIndex].DataBoundItem is not SalaryPaymentRow row) return;

            var payable = _canEdit && !row.IsPaid;
            e.Value = payable ? "Pay" : string.Empty;

            var cell = _salaryGrid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            cell.ReadOnly = !payable;
            if (payable)
            {
                AppTheme.StyleActionButtonCell(cell, AppTheme.SuccessColor);
            }
            else
            {
                AppTheme.ClearActionButtonCell(cell);
            }
        }

        private void SalaryGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_salaryGrid.Columns[e.ColumnIndex].Name != PayColumnName) return;
            if (_salaryGrid.Rows[e.RowIndex].DataBoundItem is not SalaryPaymentRow row) return;
            if (!_canEdit || row.IsPaid) return;

            if (MessageBox.Show(
                    $"Mark {row.Employee} as paid ₱{row.NetAmount:N2} today?",
                    "Pay Employee", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                using var db = CreateDbContext();
                var entity = db.SalaryPayments.Find(row.SalaryPaymentId);
                if (entity is null) return;

                entity.IsPaid = true;
                entity.PaidDate = DateTime.Today;
                TenantSyncService.SaveAndSync(db);

                LoadSalaryPayments();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not mark the salary as paid:\n\n{ex.Message}", "Pay Employee", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ExpenseGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_expenseGrid.Columns[e.ColumnIndex].Name != PayColumnName) return;
            if (_expenseGrid.Rows[e.RowIndex].DataBoundItem is not ExpenseRow row) return;

            var payable = _canEdit && !row.PaidStatus;
            e.Value = payable ? "Pay" : string.Empty;

            var cell = _expenseGrid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            cell.ReadOnly = !payable;
            if (payable)
            {
                AppTheme.StyleActionButtonCell(cell, AppTheme.SuccessColor);
            }
            else
            {
                AppTheme.ClearActionButtonCell(cell);
            }
        }

        private void ExpenseGrid_CellContentClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (_expenseGrid.Columns[e.ColumnIndex].Name != PayColumnName) return;
            if (_expenseGrid.Rows[e.RowIndex].DataBoundItem is not ExpenseRow row) return;
            if (!_canEdit || row.PaidStatus) return;

            if (MessageBox.Show(
                    $"Mark this ₱{row.Amount:N2} expense as paid today?",
                    "Pay Expense", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                using var db = CreateDbContext();
                var expense = db.Expenses.Find(row.ExpenseId);
                if (expense is null) return;

                expense.IsPaid = true;
                expense.PaidDate = DateTime.Today;
                TenantSyncService.SaveAndSync(db);

                LoadExpenses();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not mark the expense as paid:\n\n{ex.Message}", "Pay Expense", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadSalaryPayments()
        {
            try
            {
                using var db = CreateDbContext();
                _salaryGrid.DataSource = db.SalaryPayments
                    .Include(x => x.TenantUser)
                    .Where(x => !x.IsDeleted)
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.SalaryPaymentId)
                    .Select(x => new SalaryPaymentRow
                    {
                        SalaryPaymentId = x.SalaryPaymentId,
                        Employee = x.TenantUser != null ? x.TenantUser.FullName : $"User #{x.TenantUserId}",
                        GrossAmount = x.GrossAmount,
                        SssDeduction = x.SssDeduction,
                        PhilHealthDeduction = x.PhilHealthDeduction,
                        PagIbigDeduction = x.PagIbigDeduction,
                        WithholdingTax = x.WithholdingTax,
                        NetAmount = x.NetAmount,
                        PayPeriodStart = x.PayPeriodStart,
                        PayPeriodEnd = x.PayPeriodEnd,
                        PaymentDate = x.PaymentDate,
                        IsPaid = x.IsPaid,
                        PaidDate = x.PaidDate,
                        Notes = x.Notes ?? string.Empty
                    })
                    .ToList();

                AddSalaryPayColumn();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AddExpense()
        {
            try
            {
                AddExpenseCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not add this expense:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddExpenseCore()
        {
            using var db = CreateDbContext();

            // Required column - resolved the same way every other still-direct-DB screen resolves
            // it (see BranchFilter.ResolveForWrite's remarks). A company-wide role viewing "All
            // Branches" gets null back and must pick a real one before an expense can be saved
            // against it; the previous "?? 0" fallback here saved a foreign key to a branch that
            // doesn't exist instead of asking, which failed the save anyway, just less clearly.
            var branchId = BranchFilter.ResolveForWrite(db, BranchFilter.SelectedId(_expenseBranchPicker));
            if (branchId is null)
            {
                MessageBox.Show("Select a branch before adding an expense.", "Select a Branch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var suppliers = db.Suppliers.Where(x => x.IsActive).OrderBy(x => x.SupplierName).ToList();
            var equipment = db.Equipment.Where(x => x.IsActive).OrderBy(x => x.EquipmentName).ToList();

            using var dialog = new ExpenseDialogForm(existing: null, suppliers, equipment);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            db.Expenses.Add(new Expense
            {
                Category = dialog.Category,
                Description = dialog.Description,
                Amount = dialog.Amount,
                ExpenseDate = dialog.ExpenseDate,
                SupplierId = dialog.SupplierId,
                EquipmentId = dialog.EquipmentId,
                IsPaid = dialog.IsPaid,
                PaidDate = dialog.PaidDate,
                BranchId = branchId.Value,
                CreatedAt = DateTime.UtcNow
            });
            TenantSyncService.SaveAndSync(db);
            LoadExpenses();

            MessageBox.Show("Expense added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditExpense()
        {
            try
            {
                EditExpenseCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not update this expense:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EditExpenseCore()
        {
            if (_expenseGrid.SelectedRows.Count == 0) return;

            var selected = (ExpenseRow)_expenseGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            using var db = CreateDbContext();
            var entity = db.Expenses.Find(selected.ExpenseId);
            if (entity is null) return;

            var suppliers = db.Suppliers.Where(x => x.IsActive).OrderBy(x => x.SupplierName).ToList();
            var equipment = db.Equipment.Where(x => x.IsActive).OrderBy(x => x.EquipmentName).ToList();

            using var dialog = new ExpenseDialogForm(entity, suppliers, equipment);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            entity.Category = dialog.Category;
            entity.Description = dialog.Description;
            entity.Amount = dialog.Amount;
            entity.ExpenseDate = dialog.ExpenseDate;
            entity.SupplierId = dialog.SupplierId;
            entity.EquipmentId = dialog.EquipmentId;
            entity.IsPaid = dialog.IsPaid;
            entity.PaidDate = dialog.PaidDate;

            TenantSyncService.SaveAndSync(db);
            LoadExpenses();
        }

        private void DeleteExpense()
        {
            if (_expenseGrid.SelectedRows.Count == 0) return;

            var selected = (ExpenseRow)_expenseGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            if (MessageBox.Show(
                    $"Delete this expense of ₱{selected.Amount:N2}? It will no longer appear in this list or count toward reports.",
                    "Delete Expense", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                using var db = CreateDbContext();
                var entity = db.Expenses.Find(selected.ExpenseId);
                if (entity is null) return;

                entity.IsDeleted = true;
                TenantSyncService.SaveAndSync(db);
                LoadExpenses();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not delete this expense:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunPayroll()
        {
            try
            {
                RunPayrollCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not run payroll:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunPayrollCore()
        {
            using var db = CreateDbContext();
            var users = db.TenantUsers.Where(x => x.Status == UserStatus.Active).OrderBy(x => x.FullName).ToList();
            if (users.Count == 0)
            {
                MessageBox.Show("Add a tenant user first.", "Run Payroll", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var existingPayments = db.SalaryPayments.Where(x => !x.IsDeleted).ToList();

            using var dialog = new PayrollRunDialogForm(users, existingPayments);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            foreach (var entry in dialog.Entries)
            {
                db.SalaryPayments.Add(new SalaryPayment
                {
                    TenantUserId = entry.TenantUserId,
                    GrossAmount = entry.GrossAmount,
                    SssDeduction = entry.SssDeduction,
                    PhilHealthDeduction = entry.PhilHealthDeduction,
                    PagIbigDeduction = entry.PagIbigDeduction,
                    WithholdingTax = entry.WithholdingTax,
                    NetAmount = entry.NetAmount,
                    PayPeriodStart = dialog.PayPeriodStart,
                    PayPeriodEnd = dialog.PayPeriodEnd,
                    PaymentDate = DateTime.Today,
                    IsPaid = false,
                    Notes = dialog.Notes,
                    CreatedAt = DateTime.UtcNow
                });
            }

            TenantSyncService.SaveAndSync(db);
            LoadSalaryPayments();

            var total = dialog.Entries.Sum(x => x.NetAmount);
            MessageBox.Show(
                $"{dialog.Entries.Count} payslip(s) created, totalling ₱{total:N2}.\n\n" +
                "They are listed as Unpaid. Use the Pay button on each row once the employee has actually been paid.",
                "Payslips Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditSalaryPayment()
        {
            try
            {
                EditSalaryPaymentCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not update this salary payment:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EditSalaryPaymentCore()
        {
            if (_salaryGrid.SelectedRows.Count == 0) return;

            var selected = (SalaryPaymentRow)_salaryGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            using var db = CreateDbContext();
            var entity = db.SalaryPayments.Find(selected.SalaryPaymentId);
            if (entity is null) return;

            var users = db.TenantUsers.Where(x => x.Status == UserStatus.Active).OrderBy(x => x.FullName).ToList();

            using var dialog = new SalaryPaymentDialogForm(entity, users);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            entity.TenantUserId = dialog.TenantUserId;
            entity.GrossAmount = dialog.GrossAmount;
            entity.SssDeduction = dialog.SssDeduction;
            entity.PhilHealthDeduction = dialog.PhilHealthDeduction;
            entity.PagIbigDeduction = dialog.PagIbigDeduction;
            entity.WithholdingTax = dialog.WithholdingTax;
            entity.NetAmount = dialog.NetAmount;
            entity.PayPeriodStart = dialog.PayPeriodStart;
            entity.PayPeriodEnd = dialog.PayPeriodEnd;
            entity.PaymentDate = dialog.PaymentDate;
            entity.Notes = dialog.Notes;

            TenantSyncService.SaveAndSync(db);
            LoadSalaryPayments();
        }

        private void DeleteSalaryPayment()
        {
            if (_salaryGrid.SelectedRows.Count == 0) return;

            var selected = (SalaryPaymentRow)_salaryGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            if (MessageBox.Show(
                    $"Delete this salary payment of ₱{selected.NetAmount:N2} for {selected.Employee}? It will no longer appear in this list or count toward reports.",
                    "Delete Salary Payment", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                using var db = CreateDbContext();
                var entity = db.SalaryPayments.Find(selected.SalaryPaymentId);
                if (entity is null) return;

                entity.IsDeleted = true;
                TenantSyncService.SaveAndSync(db);
                LoadSalaryPayments();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not delete this salary payment:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private sealed class ExpenseRow
        {
            public int ExpenseId { get; set; }
            public string Category { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public DateTime ExpenseDate { get; set; }
            public string Supplier { get; set; } = string.Empty;
            public string Equipment { get; set; } = string.Empty;
            public bool PaidStatus { get; set; }
            public int? PurchaseOrderId { get; set; }
        }

        private sealed class SalaryPaymentRow
        {
            public int SalaryPaymentId { get; set; }
            public string Employee { get; set; } = string.Empty;
            public decimal GrossAmount { get; set; }
            public decimal SssDeduction { get; set; }
            public decimal PhilHealthDeduction { get; set; }
            public decimal PagIbigDeduction { get; set; }
            public decimal WithholdingTax { get; set; }
            public decimal NetAmount { get; set; }
            public DateTime PayPeriodStart { get; set; }
            public DateTime PayPeriodEnd { get; set; }
            public DateTime PaymentDate { get; set; }
            public bool IsPaid { get; set; }
            public DateTime? PaidDate { get; set; }
            public string Notes { get; set; } = string.Empty;

            public string PayPeriod => $"{PayPeriodStart:MMM d} - {PayPeriodEnd:MMM d, yyyy}";
            public string PaidStatus => IsPaid ? "Paid" : "Unpaid";
            public string PaidOn => PaidDate is null ? string.Empty : PaidDate.Value.ToString("MMM d, yyyy");
        }

        private sealed class OwnerTransactionRow
        {
            public int OwnerTransactionId { get; set; }
            public string Type { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public DateTime TransactionDate { get; set; }
            public string Notes { get; set; } = string.Empty;
        }
    }
}
