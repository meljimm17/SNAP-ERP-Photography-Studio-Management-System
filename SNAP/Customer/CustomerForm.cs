using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.enums;

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
                var filter = _search.Text.Trim();

                // Call API to get customers
                var customers = ApiClient.Get<List<CustomerRow>>("/api/customers");

                if (customers != null && !string.IsNullOrWhiteSpace(filter))
                {
                    // Filter on client side if search is provided
                    customers = customers
                        .Where(c =>
                            c.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            c.ContactNumber.Contains(filter) ||
                            (c.Email != null && c.Email.Contains(filter)))
                        .ToList();
                }

                _grid.DataSource = customers ?? new List<CustomerRow>();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show($"Error loading customers:\n\n{ex.Message}", "Customer Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private sealed record CustomerRow(int CustomerId, string FullName, string ContactNumber, string? Email, string? Address, DateTime CreatedAt);

        private void ViewEditCustomer()
        {
            if (_grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Select a customer first.", "View / Edit Customer", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = _grid.SelectedRows[0].DataBoundItem as dynamic;
            if (selected is null) return;

            int customerId = selected.CustomerId;

            using var dialog = new CustomerDialogForm(customerId);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                // Update via API
                var updateRequest = new
                {
                    fullName = dialog.FullName,
                    contactNumber = dialog.ContactNumber,
                    emailAddress = dialog.EmailAddress,
                    address = dialog.Address
                };
                ApiClient.Put($"/api/customers/{customerId}", updateRequest);
                LoadData();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show($"Error updating customer:\n\n{ex.Message}", "Update Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ShowBookingHistory()
        {
            if (_grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Select a customer first.", "Booking History", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selected = _grid.SelectedRows[0].DataBoundItem as dynamic;
            if (selected is null) return;

            int customerId = selected.CustomerId;
            using var history = new CustomerBookingHistoryForm(customerId);
            history.ShowDialog(this);
        }

        private void DeleteCustomer()
        {
            if (_grid.SelectedRows.Count == 0) return;

            var selected = _grid.SelectedRows[0].DataBoundItem as dynamic;
            if (selected is null) return;

            string customerName = selected.FullName;
            int customerId = selected.CustomerId;

            if (MessageBox.Show(
                    $"Delete '{customerName}'? Their booking and payment history is preserved.",
                    "Delete Customer", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                ApiClient.Delete($"/api/customers/{customerId}");
                LoadData();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show($"Error deleting customer:\n\n{ex.Message}", "Delete Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
