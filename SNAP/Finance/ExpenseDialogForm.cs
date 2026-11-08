using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;

namespace SNAP
{
    /// <summary>
    /// Add/Edit dialog for a Finance Expense, optionally tied to a Supplier and/or Equipment.
    /// </summary>
    public class ExpenseDialogForm : Form
    {
        private readonly RoundedComboBox _categoryCombo = new();
        private readonly RoundedTextBox _descriptionBox = new() { PlaceholderText = "Description (text, optional)" };
        private readonly RoundedTextBox _amountBox = new() { PlaceholderText = "Amount (decimal number, e.g. 1500.00)" };
        private readonly RoundedDatePicker _expenseDatePicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedComboBox _supplierCombo = new();
        private readonly RoundedComboBox _equipmentCombo = new();
        private readonly CheckBox _isPaidCheckbox = new() { Text = "Paid", ForeColor = AppTheme.TextPrimaryColor, Font = AppTheme.BodyFont, AutoSize = false };
        private readonly RoundedDatePicker _paidDatePicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private Control _paidDateGroup = null!;

        private readonly RoundedButton _saveButton = new() { Text = "Save Expense" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public ExpenseCategory Category { get; private set; }
        public string? Description { get; private set; }
        public decimal Amount { get; private set; }
        public DateTime ExpenseDate { get; private set; }
        public int? SupplierId { get; private set; }
        public int? EquipmentId { get; private set; }
        public bool IsPaid { get; private set; }
        public DateTime? PaidDate { get; private set; }

        public ExpenseDialogForm(Expense? existing, List<Supplier> suppliers, List<Equipment> equipment)
        {
            Text = existing is null ? "Add Expense" : "Edit Expense";
            Width = 510;
            Height = 700;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 400;
            _categoryCombo.Width = width;
            _descriptionBox.Width = width;
            _amountBox.Width = width;
            _expenseDatePicker.Width = width;
            _supplierCombo.Width = width;
            _equipmentCombo.Width = width;
            _isPaidCheckbox.Width = width;
            _isPaidCheckbox.Height = 24;
            _isPaidCheckbox.Margin = new Padding(0, 4, 0, 14);
            _paidDatePicker.Width = width;
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            foreach (var category in Enum.GetValues<ExpenseCategory>()) _categoryCombo.Items.Add(category);

            _supplierCombo.Items.Add(new ComboItem(0, "None"));
            foreach (var supplier in suppliers) _supplierCombo.Items.Add(new ComboItem(supplier.SupplierId, supplier.SupplierName));

            _equipmentCombo.Items.Add(new ComboItem(0, "None"));
            foreach (var item in equipment) _equipmentCombo.Items.Add(new ComboItem(item.EquipmentId, item.EquipmentName));

            _paidDateGroup = AppTheme.BuildFieldGroup("Paid Date", _paidDatePicker);
            _isPaidCheckbox.CheckedChanged += (_, _) => _paidDateGroup.Visible = _isPaidCheckbox.Checked;

            Controls.Add(AppTheme.BuildDialogLayout(_saveButton, _cancelButton,
                AppTheme.BuildFieldGroup("Category", _categoryCombo),
                AppTheme.BuildFieldGroup("Description", _descriptionBox),
                AppTheme.BuildFieldGroup("Amount (₱)", _amountBox),
                AppTheme.BuildFieldGroup("Expense Date", _expenseDatePicker),
                AppTheme.BuildFieldGroup("Supplier (optional)", _supplierCombo),
                AppTheme.BuildFieldGroup("Equipment (optional)", _equipmentCombo),
                _isPaidCheckbox,
                _paidDateGroup));

            if (existing is not null)
            {
                _categoryCombo.SelectedItem = existing.Category;
                _descriptionBox.Text = existing.Description ?? string.Empty;
                _amountBox.Text = existing.Amount.ToString("0.##");
                _expenseDatePicker.Value = existing.ExpenseDate;
                SelectComboValue(_supplierCombo, existing.SupplierId ?? 0);
                SelectComboValue(_equipmentCombo, existing.EquipmentId ?? 0);
                _isPaidCheckbox.Checked = existing.IsPaid;
                _paidDatePicker.Value = existing.PaidDate ?? DateTime.Today;
                _paidDateGroup.Visible = existing.IsPaid;
            }
            else
            {
                _categoryCombo.SelectedIndex = 0;
                _expenseDatePicker.Value = DateTime.Today;
                _supplierCombo.SelectedIndex = 0;
                _equipmentCombo.SelectedIndex = 0;
                _isPaidCheckbox.Checked = true;
                _paidDatePicker.Value = DateTime.Today;
                _paidDateGroup.Visible = true;
            }

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private static void SelectComboValue(RoundedComboBox combo, int id)
        {
            for (var i = 0; i < combo.Items.Count; i++)
            {
                if (((ComboItem)combo.Items[i]!).Id == id)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        private void Save()
        {
            if (_categoryCombo.SelectedItem is not ExpenseCategory category)
            {
                MessageBox.Show("Select a category.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(_amountBox.Text, out var amount) || amount <= 0)
            {
                MessageBox.Show("Enter a valid Amount greater than zero.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Category = category;
            Description = string.IsNullOrWhiteSpace(_descriptionBox.Text) ? null : _descriptionBox.Text.Trim();
            Amount = amount;
            ExpenseDate = _expenseDatePicker.Value.Date;
            SupplierId = _supplierCombo.SelectedItem is ComboItem s && s.Id > 0 ? s.Id : null;
            EquipmentId = _equipmentCombo.SelectedItem is ComboItem eq && eq.Id > 0 ? eq.Id : null;
            IsPaid = _isPaidCheckbox.Checked;
            PaidDate = _isPaidCheckbox.Checked ? _paidDatePicker.Value.Date : null;

            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed record ComboItem(int Id, string Display)
        {
            public override string ToString() => Display;
        }
    }
}
