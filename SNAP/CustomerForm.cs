using System.Drawing;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP
{
    public class CustomerForm : UserControl
    {
        private readonly RoundedTextBox _search = new() { PlaceholderText = "Search by name, contact number, or email..." };
        private readonly DataGridView _grid = new();
        private readonly RoundedButton _viewEdit = new() { IsPrimary = false };
        private readonly RoundedButton _bookingHistory = new() { IsPrimary = false };
        private readonly RoundedButton _delete = new() { IsPrimary = false };

        public CustomerForm(AccessLevel accessLevel = AccessLevel.Full)
        {
            Dock = DockStyle.Fill;
            BackColor = AppTheme.BackgroundColor;

            AppTheme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AutoGenerateColumns = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.BackgroundColor
            };
            _search.Width = 300;
            _search.Margin = new Padding(0, 0, 16, 0);

            _viewEdit.Text = "Edit";
            _bookingHistory.Text = "Booking History";
            _delete.Text = "Delete";
            _viewEdit.Width = 90;
            _bookingHistory.Width = 130;
            _delete.Width = 90;
            _viewEdit.Margin = new Padding(0, 0, 8, 0);
            _bookingHistory.Margin = new Padding(0, 0, 8, 0);

            _viewEdit.Click += (_, _) => ViewEditCustomer();
            _bookingHistory.Click += (_, _) => ShowBookingHistory();
            _delete.Click += (_, _) => DeleteCustomer();

            toolbar.Controls.Add(_search);
            toolbar.Controls.Add(_viewEdit);
            toolbar.Controls.Add(_bookingHistory);
            toolbar.Controls.Add(_delete);

            var header = AppTheme.CreatePageHeader("Customers", "Manage customer information and records.");
            Controls.Add(AppTheme.BuildPage(header, toolbar, AppTheme.WrapContent(_grid)));

            var canEdit = accessLevel is AccessLevel.Manage or AccessLevel.Full;
            _viewEdit.Enabled = canEdit;
            _delete.Enabled = canEdit;

            _search.TextChanged += (_, _) => LoadData();

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using var db = CreateDbContext();
                var filter = _search.Text.Trim();

                var query = db.Customers.Where(x => x.IsActive);
                if (!string.IsNullOrWhiteSpace(filter))
                {
                    query = query.Where(c =>
                        c.FullName.Contains(filter) ||
                        c.ContactNumber.Contains(filter) ||
                        (c.EmailAddress != null && c.EmailAddress.Contains(filter)));
                }

                _grid.DataSource = query.OrderByDescending(x => x.CreatedAt).ToList();
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

        private void ViewEditCustomer()
        {
            if (_grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Select a customer first.", "View / Edit Customer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = (Customer)_grid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            using var db = CreateDbContext();
            var entity = db.Customers.Find(selected.CustomerId);
            if (entity is null) return;

            using var dialog = new CustomerDialogForm(entity);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            entity.FullName = dialog.FullName;
            entity.ContactNumber = dialog.ContactNumber;
            entity.EmailAddress = dialog.EmailAddress;
            entity.Address = dialog.Address;

            TenantSyncService.SaveAndSync(db);
            LoadData();
        }

        private void ShowBookingHistory()
        {
            if (_grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Select a customer first.", "Booking History", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = (Customer)_grid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            using var db = CreateDbContext();
            using var history = new CustomerBookingHistoryForm(db, selected);
            history.ShowDialog(this);
        }

        private void DeleteCustomer()
        {
            if (_grid.SelectedRows.Count == 0) return;

            var selected = (Customer)_grid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            if (MessageBox.Show(
                    $"Delete '{selected.FullName}'? Their booking and payment history is preserved.",
                    "Delete Customer", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            using var db = CreateDbContext();
            var entity = db.Customers.Find(selected.CustomerId);
            if (entity is null) return;

            entity.IsActive = false;
            TenantSyncService.SaveAndSync(db);
            LoadData();
        }
    }
}
