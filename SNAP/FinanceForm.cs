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

            var tabs = new TabControl { Dock = DockStyle.Fill };
            AppTheme.StyleTabControl(tabs);
            tabs.TabPages.Add(BuildExpenseTab(canEdit));
            tabs.TabPages.Add(BuildSalaryTab(canEdit));
            tabs.TabPages.Add(BuildOwnerTransactionTab(canEdit));

            var header = AppTheme.CreatePageHeader("Finance", "Manage business expenses, salary payments, and owner transactions.");
            Controls.Add(AppTheme.BuildPage(header, null, AppTheme.WrapContent(tabs)));

            LoadExpenses();
            LoadSalaryPayments();
            LoadOwnerTransactions();
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

            toolbar.Controls.Add(_addExpense);
            toolbar.Controls.Add(_editExpense);
            toolbar.Controls.Add(_deleteExpense);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
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
            _salaryGrid.AutoGenerateColumns = true;
            _salaryGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _salaryGrid.MultiSelect = false;

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

            _addSalary.Click += (_, _) => AddSalaryPayment();
            _editSalary.Click += (_, _) => EditSalaryPayment();
            _deleteSalary.Click += (_, _) => DeleteSalaryPayment();

            toolbar.Controls.Add(_addSalary);
            toolbar.Controls.Add(_editSalary);
            toolbar.Controls.Add(_deleteSalary);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
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
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
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

            using var db = CreateDbContext();
            var entity = db.OwnerTransactions.Find(selected.OwnerTransactionId);
            if (entity is null) return;

            db.OwnerTransactions.Remove(entity);
            TenantSyncService.SaveAndSync(db);
            LoadOwnerTransactions();
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

        private void LoadExpenses()
        {
            try
            {
                using var db = CreateDbContext();
                _expenseGrid.DataSource = db.Expenses
                    .Include(x => x.Supplier)
                    .Include(x => x.Equipment)
                    .Where(x => !x.IsDeleted)
                    .OrderByDescending(x => x.CreatedAt)
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                        Notes = x.Notes ?? string.Empty
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AddExpense()
        {
            using var db = CreateDbContext();
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
                CreatedAt = DateTime.UtcNow
            });
            TenantSyncService.SaveAndSync(db);
            LoadExpenses();

            MessageBox.Show("Expense added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditExpense()
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

            using var db = CreateDbContext();
            var entity = db.Expenses.Find(selected.ExpenseId);
            if (entity is null) return;

            entity.IsDeleted = true;
            TenantSyncService.SaveAndSync(db);
            LoadExpenses();
        }

        private void AddSalaryPayment()
        {
            using var db = CreateDbContext();
            var users = db.TenantUsers.Where(x => x.Status == UserStatus.Active).OrderBy(x => x.FullName).ToList();
            if (users.Count == 0)
            {
                MessageBox.Show("Add a tenant user first.", "Add Salary Payment", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new SalaryPaymentDialogForm(existing: null, users);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            db.SalaryPayments.Add(new SalaryPayment
            {
                TenantUserId = dialog.TenantUserId,
                GrossAmount = dialog.GrossAmount,
                SssDeduction = dialog.SssDeduction,
                PhilHealthDeduction = dialog.PhilHealthDeduction,
                PagIbigDeduction = dialog.PagIbigDeduction,
                WithholdingTax = dialog.WithholdingTax,
                NetAmount = dialog.NetAmount,
                PayPeriodStart = dialog.PayPeriodStart,
                PayPeriodEnd = dialog.PayPeriodEnd,
                PaymentDate = dialog.PaymentDate,
                Notes = dialog.Notes,
                CreatedAt = DateTime.UtcNow
            });
            TenantSyncService.SaveAndSync(db);
            LoadSalaryPayments();

            MessageBox.Show("Salary payment recorded successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditSalaryPayment()
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

            using var db = CreateDbContext();
            var entity = db.SalaryPayments.Find(selected.SalaryPaymentId);
            if (entity is null) return;

            entity.IsDeleted = true;
            TenantSyncService.SaveAndSync(db);
            LoadSalaryPayments();
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
            public string Notes { get; set; } = string.Empty;
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
