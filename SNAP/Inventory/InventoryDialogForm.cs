using System.Drawing;
using System.Linq;
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
        private const string NoCategoryLabel = "(None)";

        private readonly RoundedTextBox _itemNameBox = new() { PlaceholderText = "Item Name (text, e.g. Photo album (leather))" };
        private readonly RoundedComboBox _categoryCombo = new();
        private readonly RoundedButton _addCategoryButton = new() { Text = "+", IsPrimary = false };
        private readonly RoundedTextBox _quantityBox = new() { PlaceholderText = "Quantity On Hand (decimal number)" };
        private readonly RoundedTextBox _reorderLevelBox = new() { PlaceholderText = "Reorder Level (decimal number)" };
        private readonly RoundedComboBox _supplierCombo = new();
        private readonly CheckBox _isForSaleCheckbox = new() { Text = "Customer-purchasable (e.g. polaroids, prints)", ForeColor = AppTheme.TextPrimaryColor, Font = AppTheme.BodyFont, AutoSize = false, UseMnemonic = false };
        private readonly RoundedTextBox _sellingPriceBox = new() { PlaceholderText = "Selling Price (decimal number, e.g. 50.00)" };
        private Control _sellingPriceGroup = null!;

        private readonly RoundedButton _saveButton = new() { Text = "Save Item" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public string ItemName { get; private set; } = string.Empty;
        public string? Category { get; private set; }
        public decimal QuantityOnHand { get; private set; }
        public decimal ReorderLevel { get; private set; }
        public int? SupplierId { get; private set; }
        public bool IsForSale { get; private set; }
        public decimal? SellingPrice { get; private set; }

        public InventoryDialogForm(Inventory? existing, List<Supplier> suppliers, List<string> categories)
        {
            Text = existing is null ? "Add Inventory Item" : "Edit Inventory Item";
            Width = 510;
            Height = 680;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 400;
            const int addButtonWidth = 44;
            _itemNameBox.Width = width;
            _categoryCombo.Width = width - addButtonWidth - 8;
            _addCategoryButton.Width = addButtonWidth;
            _addCategoryButton.Height = 45;
            _quantityBox.Width = width;
            _reorderLevelBox.Width = width;
            _supplierCombo.Width = width;
            _isForSaleCheckbox.Width = width;
            _isForSaleCheckbox.Height = 24;
            _isForSaleCheckbox.Margin = new Padding(0, 4, 0, 14);
            _sellingPriceBox.Width = width;
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            _supplierCombo.Items.Add(new ComboItem(0, "None"));
            foreach (var supplier in suppliers) _supplierCombo.Items.Add(new ComboItem(supplier.SupplierId, supplier.SupplierName));

            // "(None)" first, then every category already in use - picking from what exists avoids
            // "Printing Supplies" vs "Printing Suplies" silently splitting into two categories that
            // look identical in a report. The "+" button is the escape hatch for a genuinely new one.
            _categoryCombo.Items.Add(NoCategoryLabel);
            foreach (var category in categories.OrderBy(c => c, StringComparer.OrdinalIgnoreCase))
            {
                _categoryCombo.Items.Add(category);
            }
            // An existing item's own category might not be in the fetched list yet (e.g. it was
            // the only item using it and got retired) - add it so editing never silently drops it.
            if (existing?.Category is { Length: > 0 } existingCategory
                && !categories.Any(c => string.Equals(c, existingCategory, StringComparison.OrdinalIgnoreCase)))
            {
                _categoryCombo.Items.Add(existingCategory);
            }

            _addCategoryButton.Click += (_, _) => AddNewCategory();

            var categoryRow = new TableLayoutPanel { Width = width, Height = 45, ColumnCount = 2, RowCount = 1 };
            categoryRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            categoryRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, addButtonWidth));
            categoryRow.Controls.Add(_categoryCombo, 0, 0);
            categoryRow.Controls.Add(_addCategoryButton, 1, 0);

            _sellingPriceGroup = AppTheme.BuildFieldGroup("Selling Price (₱)", _sellingPriceBox);
            _isForSaleCheckbox.CheckedChanged += (_, _) => _sellingPriceGroup.Visible = _isForSaleCheckbox.Checked;

            Controls.Add(AppTheme.BuildDialogLayout(_saveButton, _cancelButton,
                AppTheme.BuildFieldGroup("Item Name", _itemNameBox),
                AppTheme.BuildFieldGroup("Category", categoryRow),
                AppTheme.BuildFieldGroup("Quantity On Hand", _quantityBox),
                AppTheme.BuildFieldGroup("Reorder Level", _reorderLevelBox),
                AppTheme.BuildFieldGroup("Supplier", _supplierCombo),
                _isForSaleCheckbox,
                _sellingPriceGroup));

            if (existing is not null)
            {
                _itemNameBox.Text = existing.ItemName;
                _categoryCombo.SelectedItem = existing.Category is { Length: > 0 } cat ? cat : NoCategoryLabel;
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

                _isForSaleCheckbox.Checked = existing.IsForSale;
                _sellingPriceBox.Text = (existing.SellingPrice ?? 0).ToString("0.##");
                _sellingPriceGroup.Visible = existing.IsForSale;
            }
            else
            {
                _supplierCombo.SelectedIndex = 0;
                _categoryCombo.SelectedItem = NoCategoryLabel;
                _sellingPriceGroup.Visible = false;
            }

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        /// <summary>Prompts for a brand-new category name, adds it to the dropdown (skipping a
        /// case-insensitive duplicate of one already there) and selects it.</summary>
        private void AddNewCategory()
        {
            using var dialog = new SimpleInputDialog("Add Category", "Category Name", "e.g. Printing Supplies", "Add Category");
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var existingIndex = -1;
            for (var i = 0; i < _categoryCombo.Items.Count; i++)
            {
                if (string.Equals(_categoryCombo.Items[i]?.ToString(), dialog.Value, StringComparison.OrdinalIgnoreCase))
                {
                    existingIndex = i;
                    break;
                }
            }

            if (existingIndex >= 0)
            {
                _categoryCombo.SelectedIndex = existingIndex;
                return;
            }

            _categoryCombo.Items.Add(dialog.Value);
            _categoryCombo.SelectedItem = dialog.Value;
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

            if (_isForSaleCheckbox.Checked && (!decimal.TryParse(_sellingPriceBox.Text, out var sellingPrice) || sellingPrice <= 0))
            {
                MessageBox.Show("Enter a valid Selling Price greater than zero for a customer-purchasable item.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ItemName = _itemNameBox.Text.Trim();
            var selectedCategory = _categoryCombo.SelectedItem?.ToString();
            Category = string.IsNullOrWhiteSpace(selectedCategory) || selectedCategory == NoCategoryLabel ? null : selectedCategory;
            QuantityOnHand = quantity;
            ReorderLevel = reorderLevel;
            SupplierId = _supplierCombo.SelectedItem is ComboItem item && item.Id > 0 ? item.Id : null;
            IsForSale = _isForSaleCheckbox.Checked;
            SellingPrice = _isForSaleCheckbox.Checked && decimal.TryParse(_sellingPriceBox.Text, out var price) ? price : null;

            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed record ComboItem(int Id, string Display)
        {
            public override string ToString() => Display;
        }
    }
}
