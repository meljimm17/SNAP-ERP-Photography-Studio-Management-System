using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP
{
    public class BookingForm : UserControl
    {
        private readonly DataGridView _grid = new();
        private readonly RoundedButton _add = new();
        private readonly RoundedButton _edit = new() { IsPrimary = false };
        private readonly RoundedButton _delete = new() { IsPrimary = false };

        public BookingForm(AccessLevel accessLevel = AccessLevel.Full)
        {
            Dock = DockStyle.Fill;
            BackColor = AppTheme.BackgroundColor;

            AppTheme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = true;
            _grid.ReadOnly = true;
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

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.BackgroundColor
            };

            _add.Text = "+ New Booking";
            _edit.Text = "Edit";
            _delete.Text = "Delete";
            _add.Width = 150;
            _edit.Width = 90;
            _delete.Width = 90;
            _edit.Margin = new Padding(0, 0, 8, 0);

            _add.Click += (_, _) => AddBooking();
            _edit.Click += (_, _) => EditBooking();
            _delete.Click += (_, _) => DeleteBooking();

            toolbar.Controls.Add(_edit);
            toolbar.Controls.Add(_delete);

            var header = AppTheme.CreatePageHeader("Bookings & Scheduling", "Manage customer bookings and photography schedules.", _add);
            Controls.Add(AppTheme.BuildPage(header, toolbar, AppTheme.WrapContent(_grid)));

            var canEdit = accessLevel is AccessLevel.Manage or AccessLevel.Full;
            _add.Enabled = canEdit;
            _edit.Enabled = canEdit;
            _delete.Enabled = canEdit;

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using var db = CreateDbContext();
                _grid.DataSource = db.Bookings
                    .Include(x => x.Customer)
                    .Include(x => x.AssignedPhotographer)
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new BookingRow
                    {
                        BookingId = x.BookingId,
                        Customer = x.Customer != null ? x.Customer.FullName : $"Customer #{x.CustomerId}",
                        Package = x.PackageNameSnapshot,
                        Price = x.PriceSnapshot,
                        SessionDate = x.SessionDate,
                        SessionStartTime = x.SessionStartTime,
                        SessionEndTime = x.SessionEndTime,
                        Location = x.Location,
                        AssignedPhotographer = x.AssignedPhotographer != null ? x.AssignedPhotographer.FullName : "Unassigned",
                        Status = x.Status.ToString()
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static TenantErpDbContext CreateDbContext()
        {
            // Use the tenant connection string from AppSession if available
            var connectionString = AppSession.CurrentTenantConnectionString 
                ?? AppDbConfig.TenantErpConnectionString;

            var options = new DbContextOptionsBuilder<TenantErpDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new TenantErpDbContext(options);
        }

        private void AddBooking()
        {
            using var db = CreateDbContext();
            using var dialog = new BookingDialogForm(db, existingBooking: null);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                LoadData();
            }
        }

        private void EditBooking()
        {
            if (_grid.SelectedRows.Count == 0) return;

            var selected = (BookingRow)_grid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            using var db = CreateDbContext();
            var entity = db.Bookings.Find(selected.BookingId);
            if (entity is null) return;

            using var dialog = new BookingDialogForm(db, entity);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                LoadData();
            }
        }

        private void DeleteBooking()
        {
            if (_grid.SelectedRows.Count == 0) return;

            var selected = (BookingRow)_grid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            if (MessageBox.Show(
                    $"Delete this booking for '{selected.Package}'? Its history is preserved (marked Cancelled), not erased.",
                    "Delete Booking", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            using var db = CreateDbContext();
            var entity = db.Bookings.Find(selected.BookingId);
            if (entity is null) return;

            entity.Status = BookingStatus.Cancelled;
            AddOnsService.ReverseAllAddOns(db, entity.BookingId);
            TenantSyncService.SaveAndSync(db);
            LoadData();
        }

        private sealed class BookingRow
        {
            public int BookingId { get; set; }
            public string Customer { get; set; } = string.Empty;
            public string Package { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public DateTime SessionDate { get; set; }
            public TimeSpan SessionStartTime { get; set; }
            public TimeSpan SessionEndTime { get; set; }
            public string Location { get; set; } = string.Empty;
            public string AssignedPhotographer { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }
    }
}
