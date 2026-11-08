using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;

namespace SNAP
{
    /// <summary>
    /// Quick, informal stock top-up for one Inventory item: pick a supplier, how much arrived, and
    /// optionally a new unit cost. Updates QuantityOnHand and UnitCost directly - for a full
    /// order/receive cycle with its own paper trail, use Purchase Orders instead.
    /// </summary>
    public class RestockDialogForm : Form
    {
        private readonly Inventory _item;

        private readonly Label _itemLabel = new() { ForeColor = AppTheme.TextSecondaryColor, Font = AppTheme.SecondaryFont, AutoSize = false };
        private readonly RoundedComboBox _supplierCombo = new();
        private readonly RoundedTextBox _quantityBox = new() { PlaceholderText = "Quantity Received (decimal number, e.g. 20)" };
        private readonly RoundedTextBox _unitCostBox = new() { PlaceholderText = "Unit Cost (decimal number, optional)" };

        private readonly RoundedButton _saveButton = new() { Text = "Restock" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public int? SupplierId { get; private set; }
        public decimal QuantityReceived { get; private set; }
        public decimal? UnitCost { get; private set; }

        public RestockDialogForm(Inventory item, List<Supplier> suppliers)
        {
            _item = item;

            Text = "Restock Item";
            Width = 510;
            Height = 500;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 400;
            _itemLabel.Width = width;
            _itemLabel.Height = 40;
            _itemLabel.Margin = new Padding(0, 0, 0, 14);
            _itemLabel.Text = $"{item.ItemName}  |  Currently on hand: {item.QuantityOnHand:0.##}";
            _supplierCombo.Width = width;
            _quantityBox.Width = width;
            _unitCostBox.Width = width;
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            _supplierCombo.Items.Add(new ComboItem(0, "None"));
            foreach (var supplier in suppliers) _supplierCombo.Items.Add(new ComboItem(supplier.SupplierId, supplier.SupplierName));

            var currentSupplierId = item.SupplierId ?? 0;
            for (var i = 0; i < _supplierCombo.Items.Count; i++)
            {
                if (((ComboItem)_supplierCombo.Items[i]!).Id == currentSupplierId)
                {
                    _supplierCombo.SelectedIndex = i;
                    break;
                }
            }

            if (item.UnitCost is not null)
            {
                _unitCostBox.Text = item.UnitCost.Value.ToString("0.##");
            }

            Controls.Add(AppTheme.BuildDialogLayout(_saveButton, _cancelButton,
                _itemLabel,
                AppTheme.BuildFieldGroup("Supplier", _supplierCombo),
                AppTheme.BuildFieldGroup("Quantity Received", _quantityBox),
                AppTheme.BuildFieldGroup("Unit Cost (optional - leave blank to keep the current cost)", _unitCostBox)));

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void Save()
        {
            if (!decimal.TryParse(_quantityBox.Text, out var quantity) || quantity <= 0)
            {
                MessageBox.Show("Enter a valid Quantity Received greater than zero.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_unitCostBox.Text) && (!decimal.TryParse(_unitCostBox.Text, out var cost) || cost < 0))
            {
                MessageBox.Show("Enter a valid, non-negative Unit Cost, or leave it blank.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SupplierId = _supplierCombo.SelectedItem is ComboItem s && s.Id > 0 ? s.Id : null;
            QuantityReceived = quantity;
            UnitCost = string.IsNullOrWhiteSpace(_unitCostBox.Text) ? _item.UnitCost : decimal.Parse(_unitCostBox.Text);

            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed record ComboItem(int Id, string Display)
        {
            public override string ToString() => Display;
        }
    }
}
