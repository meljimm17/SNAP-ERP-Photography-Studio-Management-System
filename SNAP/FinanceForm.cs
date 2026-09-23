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

        public FinanceForm(AccessLevel accessLevel = AccessLevel.Full)
        {
            Dock = DockStyle.Fill;
            BackColor = AppTheme.BackgroundColor;

            var canEdit = accessLevel is AccessLevel.Manage or AccessLevel.Full;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            AppTheme.StyleTabControl(tabs);
            tabs.TabPages.Add(BuildExpenseTab(canEdit));
            tabs.TabPages.Add(BuildSalaryTab(canEdit));

            var header = AppTheme.CreatePageHeader("Finance", "Manage business expenses and salary payments.");
            Controls.Add(AppTheme.BuildPage(header, null, AppTheme.WrapContent(tabs)));

            LoadExpenses();
            LoadSalaryPayments();
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

            _addSalary.Text = "+ Add Payment";
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
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new ExpenseRow
                    {
                        ExpenseId = x.ExpenseId,
                        Category = x.Category.ToString(),
                        Description = x.Description ?? string.Empty,
                        Amount = x.Amount,
                        ExpenseDate = x.ExpenseDate,
                        Supplier = x.Supplier != null ? x.Supplier.SupplierName : string.Empty,
                        Equipment = x.Equipment != null ? x.Equipment.EquipmentName : string.Empty
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
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new SalaryPaymentRow
                    {
                        SalaryPaymentId = x.SalaryPaymentId,
                        Employee = x.TenantUser != null ? x.TenantUser.FullName : $"User #{x.TenantUserId}",
                        Amount = x.Amount,
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

            TenantSyncService.SaveAndSync(db);
            LoadExpenses();
        }

        private void DeleteExpense()
        {
            if (_expenseGrid.SelectedRows.Count == 0) return;

            var selected = (ExpenseRow)_expenseGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            if (MessageBox.Show(
                    $"Delete this expense of ₱{selected.Amount:N2}? This cannot be undone.",
                    "Delete Expense", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            using var db = CreateDbContext();
            var entity = db.Expenses.Find(selected.ExpenseId);
            if (entity is null) return;

            db.Expenses.Remove(entity);
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
                Amount = dialog.Amount,
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
            entity.Amount = dialog.Amount;
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
                    $"Delete this salary payment of ₱{selected.Amount:N2} for {selected.Employee}? This cannot be undone.",
                    "Delete Salary Payment", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            using var db = CreateDbContext();
            var entity = db.SalaryPayments.Find(selected.SalaryPaymentId);
            if (entity is null) return;

            db.SalaryPayments.Remove(entity);
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
        }

        private sealed class SalaryPaymentRow
        {
            public int SalaryPaymentId { get; set; }
            public string Employee { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public DateTime PayPeriodStart { get; set; }
            public DateTime PayPeriodEnd { get; set; }
            public DateTime PaymentDate { get; set; }
            public string Notes { get; set; } = string.Empty;
        }
    }
}
