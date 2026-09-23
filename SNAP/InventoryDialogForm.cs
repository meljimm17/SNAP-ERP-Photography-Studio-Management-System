using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;

namespace SNAP
{
    /// <summary>
    /// Add/Edit dialog for an Inventory item, with an optional Supplier to reorder it from.
    /// </summary>
    public class InventoryDialogForm : Form
    {
        private readonly RoundedTextBox _itemNameBox = new() { PlaceholderText = "Item Name (text, e.g. Photo album (leather))" };
        private readonly RoundedTextBox _categoryBox = new() { PlaceholderText = "Category (text, optional)" };
        private readonly RoundedTextBox _quantityBox = new() { PlaceholderText = "Quantity On Hand (decimal number)" };
        private readonly RoundedTextBox _reorderLevelBox = new() { PlaceholderText = "Reorder Level (decimal number)" };
        private readonly RoundedComboBox _supplierCombo = new();

        private readonly RoundedButton _saveButton = new() { Text = "Save Item" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public string ItemName { get; private set; } = string.Empty;
        public string? Category { get; private set; }
        public decimal QuantityOnHand { get; private set; }
        public decimal ReorderLevel { get; private set; }
        public int? SupplierId { get; private set; }

        public InventoryDialogForm(Inventory? existing, List<Supplier> suppliers)
        {
            Text = existing is null ? "Add Inventory Item" : "Edit Inventory Item";
            Width = 480;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 400;
            _itemNameBox.Width = width;
            _categoryBox.Width = width;
            _quantityBox.Width = width;
            _reorderLevelBox.Width = width;
            _supplierCombo.Width = width;
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            _supplierCombo.Items.Add(new ComboItem(0, "None"));
            foreach (var supplier in suppliers) _supplierCombo.Items.Add(new ComboItem(supplier.SupplierId, supplier.SupplierName));

            Controls.Add(AppTheme.BuildDialogBody(
                AppTheme.BuildFieldGroup("Item Name", _itemNameBox),
                AppTheme.BuildFieldGroup("Category", _categoryBox),
                AppTheme.BuildFieldGroup("Quantity On Hand", _quantityBox),
                AppTheme.BuildFieldGroup("Reorder Level", _reorderLevelBox),
                AppTheme.BuildFieldGroup("Supplier", _supplierCombo),
                AppTheme.BuildDialogButtonRow(_saveButton, _cancelButton)));

            if (existing is not null)
            {
                _itemNameBox.Text = existing.ItemName;
                _categoryBox.Text = existing.Category ?? string.Empty;
                _quantityBox.Text = existing.QuantityOnHand.ToString("0.##");
                _reorderLevelBox.Text = existing.ReorderLevel.ToString("0.##");

                var supplierId = existing.SupplierId ?? 0;
                for (var i = 0; i < _supplierCombo.Items.Count; i++)
                {
                    if (((ComboItem)_supplierCombo.Items[i]!).Id == supplierId)
                    {
                        _supplierCombo.SelectedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                _supplierCombo.SelectedIndex = 0;
            }

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_itemNameBox.Text))
            {
                MessageBox.Show("Item Name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(_quantityBox.Text, out var quantity) || quantity < 0)
            {
                MessageBox.Show("Enter a valid, non-negative Quantity On Hand.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(_reorderLevelBox.Text, out var reorderLevel) || reorderLevel < 0)
            {
                MessageBox.Show("Enter a valid, non-negative Reorder Level.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ItemName = _itemNameBox.Text.Trim();
            Category = string.IsNullOrWhiteSpace(_categoryBox.Text) ? null : _categoryBox.Text.Trim();
            QuantityOnHand = quantity;
            ReorderLevel = reorderLevel;
            SupplierId = _supplierCombo.SelectedItem is ComboItem item && item.Id > 0 ? item.Id : null;

            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed record ComboItem(int Id, string Display)
        {
            public override string ToString() => Display;
        }
    }
}
