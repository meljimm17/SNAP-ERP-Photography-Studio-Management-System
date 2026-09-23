using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;

namespace SNAP
{
    /// <summary>
    /// Records stock actually arriving against an Ordered/PartiallyReceived Purchase Order, one
    /// line at a time - defaults each line's "Receive Now" to its full remaining quantity, but a
    /// partial receipt (less than what's still owed) is fully supported.
    /// </summary>
    public class ReceivePurchaseOrderDialogForm : Form
    {
        private readonly PurchaseOrder _po;
        private readonly DataGridView _grid = new();

        private readonly RoundedButton _saveButton = new() { Text = "Receive Stock" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        /// <summary>One entry per line that received a non-zero quantity in this action.</summary>
        public List<(int PurchaseOrderLineId, int InventoryId, decimal QuantityReceivedNow, decimal UnitCost)> Receipts { get; private set; } = new();

        public ReceivePurchaseOrderDialogForm(PurchaseOrder po)
        {
            _po = po;

            Text = $"Receive Purchase Order #{po.PurchaseOrderId}";
            Width = 620;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 540;
            _saveButton.Width = 220;
            _saveButton.Height = 44;
            _cancelButton.Width = 220;
            _cancelButton.Height = 44;

            BuildGrid(width);

            Controls.Add(AppTheme.BuildDialogBody(
                _grid,
                AppTheme.BuildDialogButtonRow(_saveButton, _cancelButton)));

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void BuildGrid(int width)
        {
            AppTheme.StyleGrid(_grid);
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            _grid.Width = width;
            _grid.Height = 340;
            _grid.Margin = new Padding(0, 0, 0, 14);

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Item", HeaderText = "Item", FillWeight = 160, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Ordered", HeaderText = "Ordered", FillWeight = 90, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "AlreadyReceived", HeaderText = "Already Received", FillWeight = 110, ReadOnly = true });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ReceiveNow", HeaderText = "Receive Now", FillWeight = 100 });

            foreach (var line in _po.Lines)
            {
                var remaining = line.QuantityOrdered - line.QuantityReceived;
                var rowIndex = _grid.Rows.Add();
                var row = _grid.Rows[rowIndex];
                row.Tag = line;
                row.Cells["Item"].Value = line.Inventory?.ItemName ?? $"Item #{line.InventoryId}";
                row.Cells["Ordered"].Value = line.QuantityOrdered.ToString("0.##");
                row.Cells["AlreadyReceived"].Value = line.QuantityReceived.ToString("0.##");
                row.Cells["ReceiveNow"].Value = remaining > 0 ? remaining.ToString("0.##") : "0";
            }
        }

        private void Save()
        {
            var receipts = new List<(int, int, decimal, decimal)>();

            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.Tag is not PurchaseOrderLine line) continue;

                var remaining = line.QuantityOrdered - line.QuantityReceived;
                var text = row.Cells["ReceiveNow"].Value?.ToString();

                if (!decimal.TryParse(text, out var receiveNow) || receiveNow < 0)
                {
                    MessageBox.Show($"Enter a valid, non-negative Receive Now quantity for '{line.Inventory?.ItemName}'.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (receiveNow > remaining)
                {
                    MessageBox.Show($"'{line.Inventory?.ItemName}' only has {remaining:0.##} remaining to receive.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (receiveNow > 0)
                {
                    receipts.Add((line.PurchaseOrderLineId, line.InventoryId, receiveNow, line.UnitCost));
                }
            }

            if (receipts.Count == 0)
            {
                MessageBox.Show("Enter a Receive Now quantity greater than zero for at least one line.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Receipts = receipts;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
