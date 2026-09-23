using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;

namespace SNAP
{
    /// <summary>
    /// Add/Edit dialog for a SalaryPayment against one TenantUser for a given pay period.
    /// </summary>
    public class SalaryPaymentDialogForm : Form
    {
        private readonly RoundedComboBox _userCombo = new();
        private readonly RoundedTextBox _amountBox = new() { PlaceholderText = "Amount (decimal number, e.g. 15000.00)" };
        private readonly RoundedDatePicker _periodStartPicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedDatePicker _periodEndPicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedDatePicker _paymentDatePicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedTextBox _notesBox = new() { PlaceholderText = "Notes (text, optional)" };

        private readonly RoundedButton _saveButton = new() { Text = "Save Salary Payment" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public int TenantUserId { get; private set; }
        public decimal Amount { get; private set; }
        public DateTime PayPeriodStart { get; private set; }
        public DateTime PayPeriodEnd { get; private set; }
        public DateTime PaymentDate { get; private set; }
        public string? Notes { get; private set; }

        public SalaryPaymentDialogForm(SalaryPayment? existing, List<TenantUser> users)
        {
            Text = existing is null ? "Add Salary Payment" : "Edit Salary Payment";
            Width = 480;
            Height = 640;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 400;
            _userCombo.Width = width;
            _amountBox.Width = width;
            _periodStartPicker.Width = width;
            _periodEndPicker.Width = width;
            _paymentDatePicker.Width = width;
            _notesBox.Width = width;
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            foreach (var user in users) _userCombo.Items.Add(new ComboItem(user.UserId, user.FullName));

            Controls.Add(AppTheme.BuildDialogBody(
                AppTheme.BuildFieldGroup("Employee", _userCombo),
                AppTheme.BuildFieldGroup("Amount (₱)", _amountBox),
                AppTheme.BuildFieldGroup("Pay Period Start", _periodStartPicker),
                AppTheme.BuildFieldGroup("Pay Period End", _periodEndPicker),
                AppTheme.BuildFieldGroup("Payment Date", _paymentDatePicker),
                AppTheme.BuildFieldGroup("Notes", _notesBox),
                AppTheme.BuildDialogButtonRow(_saveButton, _cancelButton)));

            if (existing is not null)
            {
                for (var i = 0; i < _userCombo.Items.Count; i++)
                {
                    if (((ComboItem)_userCombo.Items[i]!).Id == existing.TenantUserId)
                    {
                        _userCombo.SelectedIndex = i;
                        break;
                    }
                }
                _amountBox.Text = existing.Amount.ToString("0.##");
                _periodStartPicker.Value = existing.PayPeriodStart;
                _periodEndPicker.Value = existing.PayPeriodEnd;
                _paymentDatePicker.Value = existing.PaymentDate;
                _notesBox.Text = existing.Notes ?? string.Empty;
            }
            else
            {
                if (_userCombo.Items.Count > 0) _userCombo.SelectedIndex = 0;
                _periodStartPicker.Value = DateTime.Today;
                _periodEndPicker.Value = DateTime.Today;
                _paymentDatePicker.Value = DateTime.Today;
            }

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void Save()
        {
            if (_userCombo.SelectedItem is not ComboItem employee)
            {
                MessageBox.Show("Select an employee.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(_amountBox.Text, out var amount) || amount <= 0)
            {
                MessageBox.Show("Enter a valid Amount greater than zero.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_periodEndPicker.Value.Date < _periodStartPicker.Value.Date)
            {
                MessageBox.Show("Pay Period End cannot be before Pay Period Start.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            TenantUserId = employee.Id;
            Amount = amount;
            PayPeriodStart = _periodStartPicker.Value.Date;
            PayPeriodEnd = _periodEndPicker.Value.Date;
            PaymentDate = _paymentDatePicker.Value.Date;
            Notes = string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim();

            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed record ComboItem(int Id, string Display)
        {
            public override string ToString() => Display;
        }
    }
}
