using System.Windows.Forms;

namespace SNAP
{
    /// <summary>
    /// Read-only booking history for a single customer, including payment status per booking.
    /// </summary>
    public class CustomerBookingHistoryForm : Form
    {
        private readonly DataGridView _grid = new();

        public CustomerBookingHistoryForm(int customerId)
        {
            Text = "Booking History";
            Width = 900;
            Height = 500;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.BackgroundColor;

            AppTheme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AutoGenerateColumns = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            AppTheme.StyleStatusColumn(_grid, "Status", value => (value?.ToString() ?? string.Empty) switch
            {
                "Pending" => ("Pending", StatusKind.Warning),
                "Confirmed" => ("Confirmed", StatusKind.Info),
                "Completed" => ("Completed", StatusKind.Success),
                "Cancelled" => ("Cancelled", StatusKind.Danger),
                var other => (other, StatusKind.Neutral)
            });

            var header = AppTheme.CreatePageHeader("Booking History", "All bookings for this customer.");
            Controls.Add(AppTheme.BuildPage(header, null, AppTheme.WrapContent(_grid)));

            LoadBookingHistory(customerId);
        }

        private void LoadBookingHistory(int customerId)
        {
            try
            {
                // Call API to get customer booking history
                var history = ApiClient.Get<List<BookingHistoryRow>>($"/api/customers/{customerId}/bookings");
                _grid.DataSource = history ?? new List<BookingHistoryRow>();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show($"Error loading booking history:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private sealed record BookingHistoryRow(
            int BookingId,
            DateTime SessionDate,
            TimeSpan SessionStartTime,
            TimeSpan SessionEndTime,
            string Package,
            decimal Price,
            string Status,
            decimal AmountPaid);
    }
}
