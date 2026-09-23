using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;

namespace SNAP
{
    /// <summary>
    /// Add/Edit dialog for an OwnerTransaction: money the owner puts into or takes out of the
    /// business outside of their own salary.
    /// </summary>
    public class OwnerTransactionDialogForm : Form
    {
        private readonly RoundedComboBox _typeCombo = new();
        private readonly RoundedTextBox _amountBox = new() { PlaceholderText = "Amount (decimal number, e.g. 50000.00)" };
        private readonly RoundedDatePicker _transactionDatePicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedTextBox _notesBox = new() { PlaceholderText = "Notes (text, optional)" };

        private readonly RoundedButton _saveButton = new() { Text = "Save Transaction" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public OwnerTransactionType Type { get; private set; }
        public decimal Amount { get; private set; }
        public DateTime TransactionDate { get; private set; }
        public string? Notes { get; private set; }

        public OwnerTransactionDialogForm(OwnerTransaction? existing)
        {
            Text = existing is null ? "Add Owner Transaction" : "Edit Owner Transaction";
            Width = 480;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 400;
            _typeCombo.Width = width;
            _amountBox.Width = width;
            _transactionDatePicker.Width = width;
            _notesBox.Width = width;
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            foreach (var type in Enum.GetValues<OwnerTransactionType>()) _typeCombo.Items.Add(type);

            Controls.Add(AppTheme.BuildDialogBody(
                AppTheme.BuildFieldGroup("Type", _typeCombo),
                AppTheme.BuildFieldGroup("Amount (₱)", _amountBox),
                AppTheme.BuildFieldGroup("Transaction Date", _transactionDatePicker),
                AppTheme.BuildFieldGroup("Notes", _notesBox),
                AppTheme.BuildDialogButtonRow(_saveButton, _cancelButton)));

            if (existing is not null)
            {
                _typeCombo.SelectedItem = existing.Type;
                _amountBox.Text = existing.Amount.ToString("0.##");
                _transactionDatePicker.Value = existing.TransactionDate;
                _notesBox.Text = existing.Notes ?? string.Empty;
            }
            else
            {
                _typeCombo.SelectedIndex = 0;
                _transactionDatePicker.Value = DateTime.Today;
            }

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void Save()
        {
            if (_typeCombo.SelectedItem is not OwnerTransactionType type)
            {
                MessageBox.Show("Select a type.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(_amountBox.Text, out var amount) || amount <= 0)
            {
                MessageBox.Show("Enter a valid Amount greater than zero.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Type = type;
            Amount = amount;
            TransactionDate = _transactionDatePicker.Value.Date;
            Notes = string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
