using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;

namespace SNAP
{
    /// <summary>
    /// Creates (or edits, while still Draft/Ordered) a Purchase Order: a Supplier plus one or more
    /// lines (Inventory item, quantity ordered, unit cost). Receiving stock against the order
    /// happens separately, via ReceivePurchaseOrderDialogForm.
    /// </summary>
    public class PurchaseOrderDialogForm : Form
    {
        private readonly List<Inventory> _allItems;
        private readonly DataGridView _linesGrid = new();

        private readonly RoundedComboBox _supplierCombo = new();
        private readonly RoundedComboBox _statusCombo = new();
        private readonly RoundedDatePicker _orderDatePicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedDatePicker _expectedDatePicker = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "MMMM d, yyyy" };
        private readonly RoundedTextBox _notesBox = new() { PlaceholderText = "Notes (text, optional)" };

        private readonly RoundedButton _addLineButton = new() { Text = "+ Add Line", IsPrimary = false };
        private readonly RoundedButton _removeLineButton = new() { Text = "Remove Line", IsPrimary = false };
        private readonly RoundedButton _saveButton = new() { Text = "Save Purchase Order" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public int SupplierId { get; private set; }
        public PurchaseOrderStatus Status { get; private set; }
        public DateTime OrderDate { get; private set; }
        public DateTime? ExpectedDate { get; private set; }
        public string? Notes { get; private set; }
        public List<(int InventoryId, decimal QuantityOrdered, decimal UnitCost)> Lines { get; private set; } = new();

        public PurchaseOrderDialogForm(List<Supplier> suppliers, List<Inventory> items, PurchaseOrder? existing = null)
        {
            _allItems = items;

            Text = existing is null ? "New Purchase Order" : "Edit Purchase Order";
            Width = 700;
            Height = 680;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 620;
            _supplierCombo.Width = width;
            _statusCombo.Width = width;
            _orderDatePicker.Width = width;
            _expectedDatePicker.Width = width;
            _notesBox.Width = width;
            _addLineButton.Width = 140;
            _addLineButton.Height = 34;
            _removeLineButton.Width = 140;
            _removeLineButton.Height = 34;
            _saveButton.Width = 220;
            _saveButton.Height = 44;
            _cancelButton.Width = 220;
            _cancelButton.Height = 44;

            foreach (var supplier in suppliers) _supplierCombo.Items.Add(new ComboItem(supplier.SupplierId, supplier.SupplierName));
            foreach (var status in new[] { PurchaseOrderStatus.Draft, PurchaseOrderStatus.Ordered }) _statusCombo.Items.Add(status);

            BuildLinesGrid(width);

            var lineButtons = new FlowLayoutPanel { Width = width, Height = 40, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 6, 0, 14) };
            _addLineButton.Margin = new Padding(0, 0, 8, 0);
            lineButtons.Controls.Add(_addLineButton);
            lineButtons.Controls.Add(_removeLineButton);

            var linesGroup = new Panel { Width = width, Height = 240, Margin = new Padding(0, 0, 0, 14) };
            var linesLabel = AppTheme.CreateSectionLabel("Order Lines");
            linesLabel.Location = new System.Drawing.Point(0, 0);
            _linesGrid.Location = new System.Drawing.Point(0, 22);
            _linesGrid.Width = width;
            _linesGrid.Height = 180;
            linesGroup.Controls.Add(linesLabel);
            linesGroup.Controls.Add(_linesGrid);

            Controls.Add(AppTheme.BuildDialogBody(
                AppTheme.BuildFieldGroup("Supplier", _supplierCombo),
                AppTheme.BuildFieldGroup("Status", _statusCombo),
                AppTheme.BuildFieldGroup("Order Date", _orderDatePicker),
                AppTheme.BuildFieldGroup("Expected Date (optional)", _expectedDatePicker),
                linesGroup,
                lineButtons,
                AppTheme.BuildFieldGroup("Notes", _notesBox),
                AppTheme.BuildDialogButtonRow(_saveButton, _cancelButton)));

            if (existing is not null)
            {
                SelectComboValue(_supplierCombo, existing.SupplierId);
                _statusCombo.SelectedItem = existing.Status == PurchaseOrderStatus.Ordered ? PurchaseOrderStatus.Ordered : PurchaseOrderStatus.Draft;
                _orderDatePicker.Value = existing.OrderDate;
                _expectedDatePicker.Value = existing.ExpectedDate ?? DateTime.Today;
                _notesBox.Text = existing.Notes ?? string.Empty;

                foreach (var line in existing.Lines)
                {
                    AddLineRow(line.InventoryId, line.QuantityOrdered, line.UnitCost);
                }
            }
            else
            {
                if (_supplierCombo.Items.Count > 0) _supplierCombo.SelectedIndex = 0;
                _statusCombo.SelectedIndex = 0;
                _orderDatePicker.Value = DateTime.Today;
                _expectedDatePicker.Value = DateTime.Today.AddDays(7);
                AddLineRow(null, 0, 0);
            }

            _addLineButton.Click += (_, _) => AddLineRow(null, 0, 0);
            _removeLineButton.Click += (_, _) => RemoveSelectedLine();
            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void BuildLinesGrid(int width)
        {
            AppTheme.StyleGrid(_linesGrid);
            _linesGrid.AllowUserToAddRows = false;
            _linesGrid.AllowUserToDeleteRows = false;
            _linesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _linesGrid.MultiSelect = false;
            _linesGrid.AutoGenerateColumns = false;
            _linesGrid.RowHeadersVisible = false;
            _linesGrid.Width = width;
            _linesGrid.Height = 180;

            var itemColumn = new DataGridViewComboBoxColumn
            {
                Name = "Item",
                HeaderText = "Inventory Item",
                FillWeight = 200,
                DataSource = _allItems.Select(i => new ComboItem(i.InventoryId, i.ItemName)).ToList(),
                DisplayMember = nameof(ComboItem.Display),
                ValueMember = nameof(ComboItem.Id),
                DataPropertyName = "Item"
            };
            _linesGrid.Columns.Add(itemColumn);
            _linesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "Qty Ordered", FillWeight = 100 });
            _linesGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "UnitCost", HeaderText = "Unit Cost (₱)", FillWeight = 100 });
        }

        private void AddLineRow(int? inventoryId, decimal quantity, decimal unitCost)
        {
            var rowIndex = _linesGrid.Rows.Add();
            var row = _linesGrid.Rows[rowIndex];
            if (inventoryId is not null) row.Cells["Item"].Value = inventoryId.Value;
            row.Cells["Quantity"].Value = quantity > 0 ? quantity.ToString("0.##") : string.Empty;
            row.Cells["UnitCost"].Value = unitCost > 0 ? unitCost.ToString("0.##") : string.Empty;
        }

        private void RemoveSelectedLine()
        {
            if (_linesGrid.SelectedRows.Count == 0) return;
            _linesGrid.Rows.Remove(_linesGrid.SelectedRows[0]);
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
            if (_supplierCombo.SelectedItem is not ComboItem supplier)
            {
                MessageBox.Show("Select a supplier.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_statusCombo.SelectedItem is not PurchaseOrderStatus status)
            {
                MessageBox.Show("Select a status.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var lines = new List<(int InventoryId, decimal QuantityOrdered, decimal UnitCost)>();
            foreach (DataGridViewRow row in _linesGrid.Rows)
            {
                if (row.Cells["Item"].Value is not int inventoryId) continue;

                if (!decimal.TryParse(row.Cells["Quantity"].Value?.ToString(), out var quantity) || quantity <= 0)
                {
                    MessageBox.Show("Every order line needs a valid Quantity Ordered greater than zero.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!decimal.TryParse(row.Cells["UnitCost"].Value?.ToString(), out var unitCost) || unitCost < 0)
                {
                    MessageBox.Show("Every order line needs a valid, non-negative Unit Cost.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                lines.Add((inventoryId, quantity, unitCost));
            }

            if (lines.Count == 0)
            {
                MessageBox.Show("Add at least one order line.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SupplierId = supplier.Id;
            Status = status;
            OrderDate = _orderDatePicker.Value.Date;
            ExpectedDate = _expectedDatePicker.Value.Date;
            Notes = string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim();
            Lines = lines;

            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed record ComboItem(int Id, string Display)
        {
            public override string ToString() => Display;
        }
    }
}
