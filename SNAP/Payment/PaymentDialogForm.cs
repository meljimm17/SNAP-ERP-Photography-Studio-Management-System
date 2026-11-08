using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP
{
    /// <summary>
    /// Records one payment event toward a specific booking's remaining balance - used for both the
    /// first payment ("Pay", pre-filled with the full remaining balance) and a later top-up
    /// ("Update", also pre-filled with whatever remains). AmountPaid is editable down for a
    /// partial/deposit payment; entering more than what's owed only records the remaining balance,
    /// with the excess reported back as change, the same way a cash register would. Each save adds
    /// its own Payment row - a booking's total paid is the sum of all its (possibly several)
    /// payment rows, so this never edits a previous row.
    /// </summary>
    public class PaymentDialogForm : Form
    {
        private readonly TenantErpDbContext _db;
        private readonly Booking _booking;
        private readonly decimal _alreadyPaid;
        private readonly decimal _addOnsTotal;
        private readonly decimal _totalPrice;
        private readonly decimal _remainingBalance;

        private readonly Label _balanceLabel = new() { ForeColor = AppTheme.TextSecondaryColor, Font = new Font("Segoe UI", 9, FontStyle.Italic), AutoSize = false };
        private readonly RoundedTextBox _amountBox = new() { PlaceholderText = "Amount Paid (decimal number, e.g. 1500.00)" };
        private readonly RoundedComboBox _methodCombo = new();
        private readonly RoundedTextBox _referenceBox = new() { PlaceholderText = "Reference Number (auto-generated)", ReadOnly = true };
        private readonly RoundedDatePicker _paymentDatePicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };

        private readonly RoundedButton _confirmButton = new() { Text = "Save Payment" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public Payment? SavedPayment { get; private set; }
        public Booking? SavedBooking { get; private set; }
        public decimal SavedTotalPaid { get; private set; }
        public decimal SavedRemainingBalance { get; private set; }
        public decimal SavedChange { get; private set; }
        public decimal SavedAddOnsTotal => _addOnsTotal;

        public PaymentDialogForm(TenantErpDbContext db, Booking booking, decimal alreadyPaid)
        {
            _db = db;
            _booking = booking;
            _alreadyPaid = alreadyPaid;
            _addOnsTotal = AddOnsService.GetAddOnsTotal(db, booking.BookingId);
            _totalPrice = booking.PriceSnapshot + _addOnsTotal;
            _remainingBalance = _totalPrice - alreadyPaid;

            Text = alreadyPaid > 0 ? "Update Payment" : "Record Payment";
            Width = 560;
            Height = 500;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            BuildLayout();

            foreach (var method in Enum.GetValues<PaymentMethod>())
            {
                _methodCombo.Items.Add(method);
            }
            _methodCombo.SelectedIndex = 0;

            _referenceBox.Text = GenerateNextReferenceNumber();
            _paymentDatePicker.Value = DateTime.Today;
            _amountBox.Text = _remainingBalance.ToString("0.##");
            _amountBox.TextChanged += (_, _) => UpdateBalanceLabel();

            UpdateBalanceLabel();

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _confirmButton.Click += (_, _) => Save();
        }

        private void BuildLayout()
        {
            const int width = 460;
            _balanceLabel.Width = width;
            _balanceLabel.Height = 36;
            _balanceLabel.Margin = new Padding(0, 0, 0, 14);
            _amountBox.Width = width;
            _methodCombo.Width = width;
            _paymentDatePicker.Width = width;
            _referenceBox.Width = width;
            _confirmButton.Width = 220;
            _confirmButton.Height = 44;
            _cancelButton.Width = 220;
            _cancelButton.Height = 44;

            Controls.Add(AppTheme.BuildDialogLayout(_confirmButton, _cancelButton,
                _balanceLabel,
                AppTheme.BuildFieldGroup("Amount Paid", _amountBox),
                AppTheme.BuildFieldGroup("Payment Method", _methodCombo),
                AppTheme.BuildFieldGroup("Payment Date", _paymentDatePicker),
                AppTheme.BuildFieldGroup("Reference Number (auto-generated)", _referenceBox)));
        }

        /// <summary>
        /// Next sequential reference number, zero-padded to 4 digits starting at "0000". Based on
        /// the highest purely-numeric reference number seen so far, so older free-text references
        /// (e.g. "GCash Ref# 123") don't interfere with the sequence.
        /// </summary>
        private string GenerateNextReferenceNumber()
        {
            var maxNumeric = _db.Payments
                .Select(p => p.ReferenceNumber)
                .AsEnumerable()
                .Where(r => !string.IsNullOrWhiteSpace(r) && r!.All(char.IsDigit))
                .Select(r => long.Parse(r!))
                .DefaultIfEmpty(-1)
                .Max();

            return (maxNumeric + 1).ToString("D4");
        }

        private void UpdateBalanceLabel()
        {
            var typedAmount = decimal.TryParse(_amountBox.Text, out var parsed) ? parsed : 0m;

            string extra;
            if (typedAmount <= 0)
            {
                extra = string.Empty;
            }
            else if (typedAmount < _remainingBalance)
            {
                extra = $"  |  Partial payment - still short by ₱{_remainingBalance - typedAmount:N2}";
            }
            else if (typedAmount > _remainingBalance)
            {
                extra = $"  |  Change: ₱{typedAmount - _remainingBalance:N2}";
            }
            else
            {
                extra = "  |  Fully settles the balance";
            }

            var priceText = _addOnsTotal > 0
                ? $"Price: ₱{_booking.PriceSnapshot:N2} + ₱{_addOnsTotal:N2} add-ons = ₱{_totalPrice:N2}"
                : $"Price: ₱{_totalPrice:N2}";
            _balanceLabel.Text = $"{priceText}  |  Already Paid: ₱{_alreadyPaid:N2}  |  Remaining: ₱{_remainingBalance:N2}{extra}";
        }

        private void Save()
        {
            if (_remainingBalance <= 0)
            {
                MessageBox.Show(
                    "This booking is already fully paid. No additional payment is needed.",
                    "Already Fully Paid", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(_amountBox.Text, out var amount) || amount <= 0)
            {
                MessageBox.Show("Enter a valid payment amount greater than zero.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_methodCombo.SelectedItem is not PaymentMethod method)
            {
                MessageBox.Show("Select a payment method.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Partial/deposit payments are accepted as-is. Overpaying only ever records the exact
            // remaining balance - any excess is reported back as change, not stored as an overpayment.
            var change = amount > _remainingBalance ? amount - _remainingBalance : 0m;
            var amountToApply = amount > _remainingBalance ? _remainingBalance : amount;

            var entity = new Payment
            {
                BookingId = _booking.BookingId,
                AmountPaid = amountToApply,
                PaymentMethod = method,
                PaymentDate = _paymentDatePicker.Value.Date,
                ReferenceNumber = string.IsNullOrWhiteSpace(_referenceBox.Text) ? null : _referenceBox.Text.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _db.Payments.Add(entity);

            // This runs inside a modal dialog's own click handler, so a caller wrapping
            // ShowDialog() in a try/catch would not actually catch a failure here (it happens on a
            // nested message-pump iteration, not as part of the call that opened the dialog) -
            // this needs its own handling right here.
            try
            {
                TenantSyncService.SaveAndSync(_db);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save this payment:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            SavedPayment = entity;
            SavedBooking = _booking;
            SavedTotalPaid = _alreadyPaid + amountToApply;
            SavedRemainingBalance = _totalPrice - SavedTotalPaid;
            SavedChange = change;

            MessageBox.Show(
                "Payment recorded successfully.",
                "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
