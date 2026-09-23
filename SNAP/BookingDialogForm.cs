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
    /// <summary>
    /// Guided Booking &amp; Scheduling flow: search-or-register customer, pick a package,
    /// pick a date, pick a photographer, then pick from a time-slot list that already excludes
    /// slots overlapping that photographer's existing bookings that day. Used for both creating
    /// a new booking and rescheduling an existing one (Confirm re-runs the same checks either way).
    /// </summary>
    public class BookingDialogForm : Form
    {
        private static readonly TimeSpan StudioOpen = TimeSpan.FromHours(8);
        private static readonly TimeSpan StudioClose = TimeSpan.FromHours(18);
        private static readonly TimeSpan SlotStep = TimeSpan.FromMinutes(30);

        private readonly TenantErpDbContext _db;
        private readonly Booking? _existingBooking;
        private readonly List<Customer> _allCustomers;
        private readonly List<PhotographyPackage> _allPackages;
        private readonly List<TenantUser> _allPhotographers;

        private readonly RoundedTextBox _customerSearch = new() { PlaceholderText = "Search customers by name or contact number..." };
        private readonly RoundedComboBox _customerCombo = new();
        private readonly RoundedButton _newCustomerToggle = new() { Text = "+ New Customer" };
        private readonly RoundedPanel _newCustomerPanel = new() { DrawShadow = false, BackColor = AppTheme.BackgroundColor };
        private readonly RoundedTextBox _newCustomerName = new() { PlaceholderText = "Full Name (text, e.g. Juan Dela Cruz)" };
        private readonly RoundedTextBox _newCustomerContact = new() { PlaceholderText = "Contact Number (numbers, e.g. 09171234567)" };
        private readonly RoundedTextBox _newCustomerEmail = new() { PlaceholderText = "Email (text, optional, e.g. name@example.com)" };
        private readonly RoundedTextBox _newCustomerAddress = new() { PlaceholderText = "Address (text, optional)" };
        private bool _registeringNewCustomer;

        private readonly RoundedComboBox _packageCombo = new();
        private readonly RoundedDatePicker _datePicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy", MinDate = DateTime.Today };
        private readonly RoundedComboBox _photographerCombo = new();
        private readonly RoundedComboBox _slotCombo = new();
        private readonly RoundedTextBox _locationBox = new() { PlaceholderText = "Location (text, e.g. Studio A)" };
        private readonly RoundedComboBox _statusCombo = new();
        private Control _statusGroup = null!;

        private readonly RoundedButton _confirmButton = new() { Text = "Confirm Booking" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public BookingDialogForm(TenantErpDbContext db, Booking? existingBooking)
        {
            _db = db;
            _existingBooking = existingBooking;

            _allCustomers = _db.Customers.Where(x => x.IsActive).OrderBy(x => x.FullName).ToList();
            _allPackages = _db.PhotographyPackages.Where(x => x.IsActive).OrderBy(x => x.PackageName).ToList();
            _allPhotographers = _db.TenantUsers.Where(x => x.Status == UserStatus.Active && x.Role == SystemRole.Photographer).OrderBy(x => x.FullName).ToList();

            Text = existingBooking is null ? "New Booking" : "Reschedule / Update Booking";
            Width = 580;
            // Fixed at a screen-friendly height; the "+ New Customer" panel and other content that
            // don't fit are reachable via the scrollable dialog body rather than growing the
            // dialog off-screen.
            Height = 700;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildLayout();
            WireEvents();

            if (existingBooking is null)
            {
                PopulateCustomerCombo(null, autoSelectFirst: false);
                PopulatePackageCombo();
                _statusGroup.Visible = false;
            }
            else
            {
                PopulateForEdit(existingBooking);
            }

            RebuildTimeSlots();
        }

        private void BuildLayout()
        {
            const int fieldHeight = 45;
            const int width = 500;

            _customerSearch.Width = width;
            _customerCombo.Width = width;
            _customerCombo.Margin = new Padding(0, 0, 0, 14);

            _newCustomerToggle.Width = 180;
            _newCustomerToggle.Height = 32;
            _newCustomerToggle.IsPrimary = false;
            _newCustomerToggle.Margin = new Padding(0, 0, 0, 14);

            _newCustomerPanel.Width = width;
            _newCustomerPanel.Height = (fieldHeight + 8) * 4;
            _newCustomerPanel.Visible = false;
            _newCustomerPanel.Margin = new Padding(0, 0, 0, 14);
            var ny = 4;
            foreach (var box in new[] { _newCustomerName, _newCustomerContact, _newCustomerEmail, _newCustomerAddress })
            {
                box.Location = new Point(0, ny);
                box.Width = width;
                _newCustomerPanel.Controls.Add(box);
                ny += fieldHeight + 8;
            }

            _packageCombo.Width = width;
            _datePicker.Width = width;
            _photographerCombo.Width = width;
            _slotCombo.Width = width;
            _locationBox.Width = width;
            _statusCombo.Width = width;
            foreach (var status in Enum.GetValues<BookingStatus>())
            {
                _statusCombo.Items.Add(status);
            }
            _statusGroup = AppTheme.BuildFieldGroup("Status", _statusCombo);

            _confirmButton.Width = 240;
            _confirmButton.Height = 44;
            _cancelButton.Width = 240;
            _cancelButton.Height = 44;

            // FlowLayoutPanel (via BuildDialogBody) skips invisible children and closes their gap
            // automatically, so toggling _newCustomerPanel/_customerSearch/_newCustomerToggle or
            // _statusGroup's Visible flag reflows everything below with no manual Top-shifting -
            // unlike the old absolute-position layout this replaces.
            Controls.Add(AppTheme.BuildDialogBody(
                AppTheme.BuildFieldGroup("Customer", _customerSearch),
                _customerCombo,
                _newCustomerToggle,
                _newCustomerPanel,
                AppTheme.BuildFieldGroup("Photography Package", _packageCombo),
                AppTheme.BuildFieldGroup("Session Date", _datePicker),
                AppTheme.BuildFieldGroup("Photographer", _photographerCombo),
                AppTheme.BuildFieldGroup("Time Slot (unavailable times are hidden)", _slotCombo),
                AppTheme.BuildFieldGroup("Location", _locationBox),
                _statusGroup,
                AppTheme.BuildDialogButtonRow(_confirmButton, _cancelButton)));
        }

        private void WireEvents()
        {
            _customerSearch.TextChanged += (_, _) => PopulateCustomerCombo(_customerSearch.Text);
            _newCustomerToggle.Click += (_, _) => ToggleNewCustomerPanel();

            _packageCombo.SelectedIndexChanged += (_, _) => RebuildTimeSlots();
            _datePicker.ValueChanged += (_, _) => RebuildTimeSlots();
            _photographerCombo.SelectedIndexChanged += (_, _) => RebuildTimeSlots();

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _confirmButton.Click += (_, _) => ConfirmBooking();
        }

        private void ToggleNewCustomerPanel()
        {
            _registeringNewCustomer = !_registeringNewCustomer;
            _newCustomerPanel.Visible = _registeringNewCustomer;
            _customerCombo.Enabled = !_registeringNewCustomer;
            _customerSearch.Enabled = !_registeringNewCustomer;
            _newCustomerToggle.Text = _registeringNewCustomer ? "Cancel New Customer" : "+ New Customer";
        }

        /// <summary>
        /// Editing an existing booking doesn't need the search-or-register flow (the customer is
        /// already fixed) - hides the search field-group and "+ New Customer" toggle entirely
        /// rather than just disabling them (a disabled RoundedButton still paints as if it were
        /// clickable). The dialog body's FlowLayoutPanel closes the gap they leave automatically.
        /// </summary>
        private void ApplyEditCustomerLayout()
        {
            _customerSearch.Parent!.Visible = false;
            _newCustomerToggle.Visible = false;
        }

        private void PopulateCustomerCombo(string? filter, bool autoSelectFirst = true)
        {
            _customerCombo.Items.Clear();
            var matches = string.IsNullOrWhiteSpace(filter)
                ? _allCustomers
                : _allCustomers.Where(c =>
                    c.FullName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    c.ContactNumber.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var customer in matches)
            {
                _customerCombo.Items.Add(new ComboItem(customer.CustomerId, $"{customer.FullName} ({customer.ContactNumber})"));
            }

            // On the initial "New Booking" load, leave the combo blank rather than silently
            // defaulting to whichever customer happens to sort first - that looked like a real
            // selection had already been made. Once the user actually searches, picking the top
            // match for them is still helpful, so auto-select stays on for that case.
            if (autoSelectFirst && _customerCombo.Items.Count > 0)
            {
                _customerCombo.SelectedIndex = 0;
            }
        }

        private void PopulatePackageCombo()
        {
            _packageCombo.Items.Clear();
            foreach (var package in _allPackages)
            {
                _packageCombo.Items.Add(new ComboItem(package.PackageId, $"{package.PackageName} — ₱{package.Price:N0} — {package.DurationHours}h"));
            }

            if (_packageCombo.Items.Count > 0)
            {
                _packageCombo.SelectedIndex = 0;
            }

            PopulatePhotographerCombo();
        }

        private void PopulatePhotographerCombo()
        {
            _photographerCombo.Items.Clear();
            _photographerCombo.Items.Add(new ComboItem(0, "Unassigned"));
            foreach (var photographer in _allPhotographers)
            {
                _photographerCombo.Items.Add(new ComboItem(photographer.UserId, photographer.FullName));
            }

            _photographerCombo.SelectedIndex = 0;
        }

        private void PopulateForEdit(Booking booking)
        {
            ApplyEditCustomerLayout();
            _customerCombo.Enabled = false;
            _customerCombo.Items.Clear();
            var customer = _allCustomers.FirstOrDefault(c => c.CustomerId == booking.CustomerId);
            _customerCombo.Items.Add(new ComboItem(booking.CustomerId, customer?.FullName ?? booking.PackageNameSnapshot));
            _customerCombo.SelectedIndex = 0;

            _packageCombo.Enabled = false;
            _packageCombo.Items.Clear();
            _packageCombo.Items.Add(new ComboItem(booking.PackageId, $"{booking.PackageNameSnapshot} — ₱{booking.PriceSnapshot:N0} — {booking.DurationSnapshot}h"));
            _packageCombo.SelectedIndex = 0;

            PopulatePhotographerCombo();
            var photographerId = booking.AssignedPhotographerUserId ?? 0;
            for (var i = 0; i < _photographerCombo.Items.Count; i++)
            {
                if (((ComboItem)_photographerCombo.Items[i]!).Id == photographerId)
                {
                    _photographerCombo.SelectedIndex = i;
                    break;
                }
            }

            _datePicker.Value = booking.SessionDate.Date < DateTime.Today ? DateTime.Today : booking.SessionDate.Date;
            _locationBox.Text = booking.Location;

            _statusGroup.Visible = true;
            _statusCombo.SelectedItem = booking.Status;
        }

        private decimal GetSelectedPackageDurationHours()
        {
            if (_existingBooking is not null)
            {
                return _existingBooking.DurationSnapshot;
            }

            if (_packageCombo.SelectedItem is ComboItem item)
            {
                var package = _allPackages.FirstOrDefault(p => p.PackageId == item.Id);
                if (package is not null) return package.DurationHours;
            }

            return 1m;
        }

        private void RebuildTimeSlots()
        {
            if (_photographerCombo.Items.Count == 0) return;

            var duration = TimeSpan.FromHours((double)GetSelectedPackageDurationHours());
            var sessionDate = _datePicker.Value.Date;
            var photographerId = _photographerCombo.SelectedItem is ComboItem p && p.Id > 0 ? p.Id : (int?)null;

            var overlapService = new BookingOverlapService();
            var existingBookingsThatDay = _db.Bookings
                .Where(x => x.SessionDate.Date == sessionDate && x.Status != BookingStatus.Cancelled)
                .ToList();

            var previouslySelected = _slotCombo.SelectedItem as SlotOption;
            _slotCombo.Items.Clear();

            for (var start = StudioOpen; start + duration <= StudioClose; start += SlotStep)
            {
                var candidate = new Booking
                {
                    BookingId = _existingBooking?.BookingId ?? 0,
                    SessionDate = sessionDate,
                    SessionStartTime = start,
                    SessionEndTime = start + duration,
                    AssignedPhotographerUserId = photographerId,
                    Status = BookingStatus.Pending
                };

                if (!overlapService.HasOverlap(candidate, existingBookingsThatDay))
                {
                    _slotCombo.Items.Add(new SlotOption(start, start + duration));
                }
            }

            if (previouslySelected is not null)
            {
                foreach (var item in _slotCombo.Items)
                {
                    if (item is SlotOption slot && slot.Start == previouslySelected.Start)
                    {
                        _slotCombo.SelectedItem = item;
                        break;
                    }
                }
            }

            if (_slotCombo.SelectedIndex < 0 && _existingBooking is not null)
            {
                // Keep the booking's current slot visible even if it's technically "taken" by itself.
                var current = new SlotOption(_existingBooking.SessionStartTime, _existingBooking.SessionEndTime);
                _slotCombo.Items.Insert(0, current);
                _slotCombo.SelectedIndex = 0;
            }
            else if (_slotCombo.SelectedIndex < 0 && _slotCombo.Items.Count > 0)
            {
                _slotCombo.SelectedIndex = 0;
            }
        }

        private void ConfirmBooking()
        {
            if (_slotCombo.SelectedItem is not SlotOption slot)
            {
                MessageBox.Show("No time slots are available for this date/photographer/package combination. Choose a different date or photographer.", "No Slots Available", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_locationBox.Text))
            {
                MessageBox.Show("Location is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var photographerId = _photographerCombo.SelectedItem is ComboItem p && p.Id > 0 ? p.Id : (int?)null;
            var sessionDate = _datePicker.Value.Date;

            Booking entity;
            if (_existingBooking is null)
            {
                int customerId;
                if (_registeringNewCustomer)
                {
                    if (string.IsNullOrWhiteSpace(_newCustomerName.Text) || string.IsNullOrWhiteSpace(_newCustomerContact.Text))
                    {
                        MessageBox.Show("Full Name and Contact Number are required to register a new customer.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (!Validation.IsValidPhMobileNumber(_newCustomerContact.Text))
                    {
                        MessageBox.Show("Enter a valid PH mobile number, e.g. 09171234567 or +639171234567.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(_newCustomerEmail.Text) && !Validation.IsValidEmail(_newCustomerEmail.Text))
                    {
                        MessageBox.Show("Enter a valid email address, e.g. name@example.com.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    var newCustomer = new Customer
                    {
                        FullName = _newCustomerName.Text.Trim(),
                        ContactNumber = _newCustomerContact.Text.Trim(),
                        EmailAddress = string.IsNullOrWhiteSpace(_newCustomerEmail.Text) ? null : _newCustomerEmail.Text.Trim(),
                        Address = string.IsNullOrWhiteSpace(_newCustomerAddress.Text) ? null : _newCustomerAddress.Text.Trim(),
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.Customers.Add(newCustomer);
                    TenantSyncService.SaveAndSync(_db);
                    customerId = newCustomer.CustomerId;
                }
                else
                {
                    if (_customerCombo.SelectedItem is not ComboItem customerItem)
                    {
                        MessageBox.Show("Select an existing customer or register a new one.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    customerId = customerItem.Id;
                }

                if (_packageCombo.SelectedItem is not ComboItem packageItem)
                {
                    MessageBox.Show("Select a photography package.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var package = _allPackages.First(p => p.PackageId == packageItem.Id);

                entity = new Booking
                {
                    CustomerId = customerId,
                    PackageId = package.PackageId,
                    AssignedPhotographerUserId = photographerId,
                    PackageNameSnapshot = package.PackageName,
                    PriceSnapshot = package.Price,
                    DurationSnapshot = package.DurationHours,
                    SessionDate = sessionDate,
                    SessionStartTime = slot.Start,
                    SessionEndTime = slot.End,
                    Location = _locationBox.Text.Trim(),
                    Status = BookingStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
            }
            else
            {
                entity = _db.Bookings.Find(_existingBooking.BookingId)!;
                entity.AssignedPhotographerUserId = photographerId;
                entity.SessionDate = sessionDate;
                entity.SessionStartTime = slot.Start;
                entity.SessionEndTime = slot.End;
                entity.Location = _locationBox.Text.Trim();
                entity.Status = _statusCombo.SelectedItem is BookingStatus status ? status : entity.Status;
            }

            // Defense-in-depth: re-validate immediately before saving, since availability could
            // have changed since the dialog opened (e.g. another user just booked the same slot).
            var overlapService = new BookingOverlapService();
            var existingBookings = _db.Bookings
                .AsNoTracking()
                .Where(x => x.Status != BookingStatus.Cancelled)
                .ToList();

            if (overlapService.HasOverlap(entity, existingBookings))
            {
                MessageBox.Show("This slot was just booked for that photographer. Please choose a different time.", "Booking Conflict", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RebuildTimeSlots();
                return;
            }

            var isNewBooking = _existingBooking is null;
            if (isNewBooking)
            {
                _db.Bookings.Add(entity);
            }

            TenantSyncService.SaveAndSync(_db);

            MessageBox.Show(
                isNewBooking ? "Booking created successfully." : "Booking updated successfully.",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed record ComboItem(int Id, string Display)
        {
            public override string ToString() => Display;
        }

        private sealed record SlotOption(TimeSpan Start, TimeSpan End)
        {
            public override string ToString() => $"{FormatTime(Start)} - {FormatTime(End)}";

            private static string FormatTime(TimeSpan time) => DateTime.Today.Add(time).ToString("h:mm tt");
        }
    }
}
