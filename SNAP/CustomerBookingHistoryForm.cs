using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.domain.entities;
using SNAP.infrastructure.Data;

namespace SNAP
{
    /// <summary>
    /// Read-only booking history for a single customer, including payment status per booking.
    /// </summary>
    public class CustomerBookingHistoryForm : Form
    {
        private readonly DataGridView _grid = new();

        public CustomerBookingHistoryForm(TenantErpDbContext db, Customer customer)
        {
            Text = $"Booking History - {customer.FullName}";
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

            var header = AppTheme.CreatePageHeader("Booking History", $"All bookings for {customer.FullName}.");
            Controls.Add(AppTheme.BuildPage(header, null, AppTheme.WrapContent(_grid)));

            var bookings = db.Bookings
                .Where(b => b.CustomerId == customer.CustomerId)
                .OrderByDescending(b => b.SessionDate)
                .ToList();

            var bookingIds = bookings.Select(b => b.BookingId).ToList();
            var totalsByBooking = db.Payments
                .Where(p => bookingIds.Contains(p.BookingId))
                .GroupBy(p => p.BookingId)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.AmountPaid));

            _grid.DataSource = bookings.Select(b => new BookingHistoryRow
            {
                BookingId = b.BookingId,
                Package = b.PackageNameSnapshot,
                SessionDate = b.SessionDate,
                Time = $"{DateTime.Today.Add(b.SessionStartTime):h:mm tt} - {DateTime.Today.Add(b.SessionEndTime):h:mm tt}",
                Location = b.Location,
                Status = b.Status.ToString(),
                Price = b.PriceSnapshot,
                TotalPaid = totalsByBooking.GetValueOrDefault(b.BookingId),
                RemainingBalance = b.PriceSnapshot - totalsByBooking.GetValueOrDefault(b.BookingId)
            }).ToList();
        }

        private sealed class BookingHistoryRow
        {
            public int BookingId { get; set; }
            public string Package { get; set; } = string.Empty;
            public DateTime SessionDate { get; set; }
            public string Time { get; set; } = string.Empty;
            public string Location { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public decimal TotalPaid { get; set; }
            public decimal RemainingBalance { get; set; }
        }
    }
}
