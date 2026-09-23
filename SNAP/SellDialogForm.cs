using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;

namespace SNAP
{
    /// <summary>
    /// Walk-in sale of one IsForSale Inventory item, with no booking involved - the standalone
    /// counterpart to the booking Add-ons flow (see AddOnsService). Optionally links a Customer
    /// and/or Booking for record-keeping, but neither is required.
    /// </summary>
    public class SellDialogForm : Form
    {
        private readonly Inventory _item;

        private readonly Label _itemLabel = new() { ForeColor = AppTheme.TextSecondaryColor, Font = AppTheme.SecondaryFont, AutoSize = false };
        private readonly RoundedTextBox _quantityBox = new() { PlaceholderText = "Quantity (decimal number, e.g. 1)" };
        private readonly RoundedComboBox _customerCombo = new();
        private readonly RoundedComboBox _bookingCombo = new();
        private readonly RoundedComboBox _methodCombo = new();
        private readonly Label _totalLabel = new() { ForeColor = AppTheme.TextPrimaryColor, Font = new Font("Segoe UI", 10, FontStyle.Bold), AutoSize = false };

        private readonly RoundedButton _saveButton = new() { Text = "Record Sale" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public decimal Quantity { get; private set; }
        public int? CustomerId { get; private set; }
        public int? BookingId { get; private set; }
        public PaymentMethod PaymentMethod { get; private set; }

        public SellDialogForm(Inventory item, List<Customer> customers, List<Booking> bookings)
        {
            _item = item;

            Text = "Sell Item";
            Width = 480;
            Height = 620;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 400;
            _itemLabel.Width = width;
            _itemLabel.Height = 40;
            _itemLabel.Margin = new Padding(0, 0, 0, 14);
            _itemLabel.Text = $"{item.ItemName}  |  ₱{item.SellingPrice ?? 0:N2} each  |  {item.QuantityOnHand:0.##} on hand";
            _quantityBox.Width = width;
            _customerCombo.Width = width;
            _bookingCombo.Width = width;
            _methodCombo.Width = width;
            _totalLabel.Width = width;
            _totalLabel.Height = 30;
            _totalLabel.Margin = new Padding(0, 0, 0, 14);
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            _customerCombo.Items.Add(new ComboItem(0, "None (walk-in)"));
            foreach (var customer in customers) _customerCombo.Items.Add(new ComboItem(customer.CustomerId, customer.FullName));
            _customerCombo.SelectedIndex = 0;

            _bookingCombo.Items.Add(new ComboItem(0, "None"));
            foreach (var booking in bookings) _bookingCombo.Items.Add(new ComboItem(booking.BookingId, $"#{booking.BookingId} — {booking.PackageNameSnapshot} ({booking.SessionDate:MMM d, yyyy})"));
            _bookingCombo.SelectedIndex = 0;

            foreach (var method in Enum.GetValues<PaymentMethod>()) _methodCombo.Items.Add(method);
            _methodCombo.SelectedIndex = 0;

            _quantityBox.Text = "1";
            _quantityBox.TextChanged += (_, _) => UpdateTotal();
            UpdateTotal();

            Controls.Add(AppTheme.BuildDialogBody(
                _itemLabel,
                AppTheme.BuildFieldGroup("Quantity", _quantityBox),
                AppTheme.BuildFieldGroup("Customer (optional)", _customerCombo),
                AppTheme.BuildFieldGroup("Booking (optional)", _bookingCombo),
                AppTheme.BuildFieldGroup("Payment Method", _methodCombo),
                _totalLabel,
                AppTheme.BuildDialogButtonRow(_saveButton, _cancelButton)));

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void UpdateTotal()
        {
            var quantity = decimal.TryParse(_quantityBox.Text, out var q) ? q : 0m;
            _totalLabel.Text = $"Total: ₱{quantity * (_item.SellingPrice ?? 0):N2}";
        }

        private void Save()
        {
            if (!decimal.TryParse(_quantityBox.Text, out var quantity) || quantity <= 0)
            {
                MessageBox.Show("Enter a valid Quantity greater than zero.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (quantity > _item.QuantityOnHand)
            {
                MessageBox.Show($"Only {_item.QuantityOnHand:0.##} unit(s) on hand.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_methodCombo.SelectedItem is not PaymentMethod method)
            {
                MessageBox.Show("Select a payment method.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Quantity = quantity;
            CustomerId = _customerCombo.SelectedItem is ComboItem c && c.Id > 0 ? c.Id : null;
            BookingId = _bookingCombo.SelectedItem is ComboItem b && b.Id > 0 ? b.Id : null;
            PaymentMethod = method;

            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed record ComboItem(int Id, string Display)
        {
            public override string ToString() => Display;
        }
    }
}
