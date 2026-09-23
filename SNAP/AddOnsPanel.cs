using System.Drawing;
using System.Windows.Forms;
using SNAP.domain.entities;

namespace SNAP
{
    /// <summary>
    /// The shared Add-ons checklist: one row per IsForSale Inventory item (name, price, quantity
    /// stepper) with a live running total - built once, embedded directly in the Booking Dialog
    /// (inline) and hosted inside AddOnsDialogForm for the Payment Management entry point, so both
    /// places show the exact same checklist, prices, and behavior.
    /// </summary>
    public class AddOnsPanel : Panel
    {
        private readonly List<Inventory> _items;
        private readonly Dictionary<int, CheckBox> _checks = new();
        private readonly Dictionary<int, NumericUpDown> _quantities = new();
        private readonly Label _totalLabel = new();

        public AddOnsPanel(List<Inventory> forSaleItems, int width)
        {
            _items = forSaleItems;
            Width = width;

            var y = 0;
            foreach (var item in _items)
            {
                var check = new CheckBox
                {
                    Text = $"{item.ItemName} — ₱{item.SellingPrice ?? 0:N2}",
                    AutoSize = false,
                    Width = width - 90,
                    Height = 28,
                    Location = new Point(0, y),
                    Font = AppTheme.BodyFont,
                    ForeColor = AppTheme.TextPrimaryColor,
                    UseMnemonic = false
                };
                var qty = new NumericUpDown
                {
                    Minimum = 0,
                    Maximum = 999,
                    Value = 0,
                    Width = 70,
                    Height = 28,
                    Location = new Point(width - 80, y),
                    Enabled = false
                };

                check.CheckedChanged += (_, _) =>
                {
                    qty.Enabled = check.Checked;
                    if (check.Checked && qty.Value == 0) qty.Value = 1;
                    if (!check.Checked) qty.Value = 0;
                    UpdateTotal();
                };
                qty.ValueChanged += (_, _) => UpdateTotal();

                Controls.Add(check);
                Controls.Add(qty);
                _checks[item.InventoryId] = check;
                _quantities[item.InventoryId] = qty;

                y += 34;
            }

            if (_items.Count == 0)
            {
                var emptyLabel = new Label
                {
                    Text = "No sellable inventory items yet. Add one from Inventory > Inventory Items first.",
                    ForeColor = AppTheme.TextSecondaryColor,
                    Font = AppTheme.SecondaryFont,
                    AutoSize = false,
                    Width = width,
                    Height = 40,
                    Location = new Point(0, y)
                };
                Controls.Add(emptyLabel);
                y += 44;
            }

            _totalLabel.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            _totalLabel.ForeColor = AppTheme.TextPrimaryColor;
            _totalLabel.AutoSize = false;
            _totalLabel.Width = width;
            _totalLabel.Height = 26;
            _totalLabel.Location = new Point(0, y + 6);
            Controls.Add(_totalLabel);

            Height = y + 6 + 26;
            UpdateTotal();
        }

        /// <summary>Pre-checks and sets quantities for items already sold against this booking, so
        /// reopening the dialog shows what's already there instead of a blank checklist.</summary>
        public void LoadExisting(IEnumerable<ProductSale> existing)
        {
            foreach (var sale in existing)
            {
                if (!_checks.TryGetValue(sale.InventoryId, out var check)) continue;
                check.Checked = true;
                if (_quantities.TryGetValue(sale.InventoryId, out var qty))
                {
                    qty.Value = Math.Max(qty.Minimum, Math.Min(qty.Maximum, sale.Quantity));
                }
            }
            UpdateTotal();
        }

        public List<AddOnsService.AddOnSelection> GetSelections()
        {
            var result = new List<AddOnsService.AddOnSelection>();
            foreach (var item in _items)
            {
                if (_checks[item.InventoryId].Checked && _quantities[item.InventoryId].Value > 0)
                {
                    result.Add(new AddOnsService.AddOnSelection(item.InventoryId, _quantities[item.InventoryId].Value, item.SellingPrice ?? 0m));
                }
            }
            return result;
        }

        public decimal GetTotal() => GetSelections().Sum(s => s.Quantity * s.UnitPrice);

        private void UpdateTotal()
        {
            _totalLabel.Text = $"Add-ons Total: ₱{GetTotal():N2}";
        }
    }
}
