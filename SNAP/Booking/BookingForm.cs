using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;

namespace SNAP
{
    public class BookingForm : UserControl
    {
        private readonly DataGridView _grid = new();
        private readonly RoundedButton _add = new();
        private readonly RoundedButton _edit = new() { IsPrimary = false };
        private readonly RoundedButton _delete = new() { IsPrimary = false };

        // Only shown on Medium tier to a company-wide role; a branch-bound user has no choice to
        // make and the API filters their rows for them regardless (BranchScope).
        private readonly RoundedComboBox _branchFilter = new();

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

            BranchFilter.Populate(_branchFilter);
            _branchFilter.Width = 170;
            _branchFilter.Margin = new Padding(12, 0, 0, 0);
            _branchFilter.SelectedIndexChanged += (_, _) => LoadData();

            toolbar.Controls.Add(_edit);
            toolbar.Controls.Add(_delete);
            toolbar.Controls.Add(_branchFilter);

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
                // The branch is a query hint, not a guarantee - BranchScope on the server ignores
                // it for a branch-bound user and substitutes their own.
                var branchId = SelectedBranchId;
                var route = branchId is null ? "/api/bookings" : $"/api/bookings?branchId={branchId}";
                var bookings = ApiClient.Get<List<BookingRow>>(route);

                if (bookings != null)
                {
                    // With several branches combined in one grid, the rows need a Branch column to
                    // stay tellable apart; below Medium there's nothing to distinguish.
                    var showBranch = AppSession.IsBranchAware && branchId is null;

                    var displayBookings = bookings.Select(b => new
                    {
                        b.BookingId,
                        Customer = b.CustomerName,
                        Package = b.Package,
                        Price = b.Price,
                        b.SessionDate,
                        b.SessionStartTime,
                        b.SessionEndTime,
                        b.Location,
                        Photographer = b.Photographer,
                        Branch = b.Branch,
                        b.Status
                    }).ToList();

                    _grid.DataSource = displayBookings;

                    if (_grid.Columns["Branch"] is { } branchColumn)
                    {
                        branchColumn.Visible = showBranch;
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show($"Error loading bookings:\n\n{ex.Message}", "Booking Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private sealed record BookingRow(int BookingId, string CustomerName, string Package, DateTime SessionDate,
            TimeSpan SessionStartTime, TimeSpan SessionEndTime, string Photographer, string Location, string Status, decimal Price,
            string Branch);

        /// <summary>The branch the grid is showing: the user's own if they're pinned to one,
        /// otherwise the picker's choice, where null means every branch.</summary>
        private int? SelectedBranchId => BranchFilter.Resolve(BranchFilter.SelectedId(_branchFilter));

        // BookingDialogForm hasn't been converted to call the API yet - its guided
        // customer/package/photographer/slot flow still reads and writes TenantErpDbContext
        // directly, so it still needs a live db context rather than an id.
        private static TenantErpDbContext CreateDbContext()
        {
            var connectionString = AppSession.CurrentTenantConnectionString
                ?? AppDbConfig.TenantErpConnectionString;

            var options = new DbContextOptionsBuilder<TenantErpDbContext>()
                .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3))
                .Options;

            return new TenantErpDbContext(options);
        }

        private void AddBooking()
        {
            using var db = CreateDbContext();
            // Pre-select whichever branch the grid is filtered to, so creating a booking while
            // looking at one branch doesn't silently default to another. On "All Branches" this
            // is null and the dialog's own picker makes the user choose.
            using var dialog = new BookingDialogForm(db, existingBooking: null, initialBranchId: SelectedBranchId);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                LoadData();
            }
        }

        private void EditBooking()
        {
            if (_grid.SelectedRows.Count == 0) return;

            var selected = _grid.SelectedRows[0].DataBoundItem as dynamic;
            if (selected is null) return;

            int bookingId = selected.BookingId;

            using var db = CreateDbContext();
            var entity = db.Bookings.Find(bookingId);
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

            var selected = _grid.SelectedRows[0].DataBoundItem as dynamic;
            if (selected is null) return;

            string package = selected.Package;
            int bookingId = selected.BookingId;

            if (MessageBox.Show(
                    $"Delete this booking for '{package}'? Its history is preserved (marked Cancelled), not erased.",
                    "Delete Booking", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                ApiClient.Delete($"/api/bookings/{bookingId}");
                LoadData();
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show($"Error deleting booking:\n\n{ex.Message}", "Delete Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
