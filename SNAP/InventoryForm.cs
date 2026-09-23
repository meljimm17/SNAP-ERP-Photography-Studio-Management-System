using System.Windows.Forms;
using SNAP.Controllers;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;

namespace SNAP
{
    /// <summary>
    /// Inventory Management: Inventory Items, Suppliers (with a spend-history panel), Purchase
    /// Orders, and Product Sales history, on four tabs of one screen.
    ///
    /// This is the View half of an MVP split: it only builds/renders controls and forwards user
    /// actions to InventoryController (the Presenter/Controller), which owns every database query,
    /// save, and business rule. It never talks to TenantErpDbContext directly, and a rejected
    /// action (a database error or a business-rule violation, e.g. "only Draft/Ordered POs can be
    /// edited") always arrives here as an exception to display, the same way for both.
    /// </summary>
    public class InventoryForm : UserControl
    {
        private readonly InventoryController _controller = new();

        private readonly DataGridView _inventoryGrid = new();
        private readonly RoundedButton _addItem = new();
        private readonly RoundedButton _editItem = new() { IsPrimary = false };
        private readonly RoundedButton _deleteItem = new() { IsPrimary = false };
        private readonly RoundedButton _restockItem = new() { IsPrimary = false };
        private readonly RoundedButton _sellItem = new() { IsPrimary = false };

        private readonly DataGridView _supplierGrid = new();
        private readonly DataGridView _supplierSpendGrid = new();
        private readonly RoundedButton _addSupplier = new();
        private readonly RoundedButton _editSupplier = new() { IsPrimary = false };
        private readonly RoundedButton _deleteSupplier = new() { IsPrimary = false };

        private readonly DataGridView _poGrid = new();
        private readonly RoundedButton _addPo = new();
        private readonly RoundedButton _editPo = new() { IsPrimary = false };
        private readonly RoundedButton _receivePo = new() { IsPrimary = false };

        private readonly DataGridView _productSalesGrid = new();

        private readonly bool _canEdit;

        public InventoryForm(AccessLevel accessLevel = AccessLevel.Full)
        {
            Dock = DockStyle.Fill;
            BackColor = AppTheme.BackgroundColor;

            _canEdit = accessLevel is AccessLevel.Manage or AccessLevel.Full;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            AppTheme.StyleTabControl(tabs);
            tabs.TabPages.Add(BuildInventoryTab());
            tabs.TabPages.Add(BuildSupplierTab());
            tabs.TabPages.Add(BuildPurchaseOrdersTab());
            tabs.TabPages.Add(BuildProductSalesTab());

            var header = AppTheme.CreatePageHeader("Inventory", "Monitor studio supplies, stock levels, and procurement.");
            Controls.Add(AppTheme.BuildPage(header, null, AppTheme.WrapContent(tabs)));

            LoadInventory();
            LoadSuppliers();
            LoadPurchaseOrders();
            LoadProductSales();
        }

        private static void ShowError(Exception ex) =>
            MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        // ----- Inventory Items tab -----

        private TabPage BuildInventoryTab()
        {
            var page = new TabPage("Inventory Items") { BackColor = AppTheme.WhiteColor };

            AppTheme.StyleGrid(_inventoryGrid);
            _inventoryGrid.Dock = DockStyle.Fill;
            _inventoryGrid.ReadOnly = true;
            _inventoryGrid.AutoGenerateColumns = true;
            _inventoryGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _inventoryGrid.MultiSelect = false;
            AppTheme.StyleStatusColumn(_inventoryGrid, "LowStock", value =>
                value is true ? ("Low Stock", StatusKind.Warning) : ("In Stock", StatusKind.Success));

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.WhiteColor
            };

            _addItem.Text = "+ Add Item";
            _editItem.Text = "Edit";
            _deleteItem.Text = "Delete";
            _restockItem.Text = "Restock";
            _sellItem.Text = "Sell";
            _addItem.Width = 120;
            _editItem.Width = 90;
            _deleteItem.Width = 90;
            _restockItem.Width = 90;
            _sellItem.Width = 80;
            _addItem.Margin = new Padding(0, 0, 8, 0);
            _editItem.Margin = new Padding(0, 0, 8, 0);
            _deleteItem.Margin = new Padding(0, 0, 8, 0);
            _restockItem.Margin = new Padding(0, 0, 8, 0);

            _addItem.Click += (_, _) => AddInventoryItem();
            _editItem.Click += (_, _) => EditInventoryItem();
            _deleteItem.Click += (_, _) => DeleteInventoryItem();
            _restockItem.Click += (_, _) => RestockInventoryItem();
            _sellItem.Click += (_, _) => SellInventoryItem();

            toolbar.Controls.Add(_addItem);
            toolbar.Controls.Add(_editItem);
            toolbar.Controls.Add(_deleteItem);
            toolbar.Controls.Add(_restockItem);
            toolbar.Controls.Add(_sellItem);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(_inventoryGrid, 0, 1);
            page.Controls.Add(layout);

            _addItem.Enabled = _canEdit;
            _editItem.Enabled = _canEdit;
            _deleteItem.Enabled = _canEdit;
            _restockItem.Enabled = _canEdit;
            _sellItem.Enabled = _canEdit;

            return page;
        }

        private void LoadInventory()
        {
            try
            {
                _inventoryGrid.DataSource = _controller.GetInventoryItems();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AddInventoryItem()
        {
            var suppliers = _controller.GetActiveSuppliers();

            using var dialog = new InventoryDialogForm(existing: null, suppliers);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                _controller.AddInventoryItem(dialog.ItemName, dialog.Category, dialog.QuantityOnHand, dialog.ReorderLevel, dialog.SupplierId, dialog.IsForSale, dialog.SellingPrice);
                LoadInventory();
                MessageBox.Show("Inventory item added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void EditInventoryItem()
        {
            if (_inventoryGrid.SelectedRows.Count == 0) return;
            if (_inventoryGrid.SelectedRows[0].DataBoundItem is not InventoryRow selected) return;

            Inventory entity;
            try
            {
                entity = _controller.GetInventoryItem(selected.InventoryId);
            }
            catch (Exception ex)
            {
                ShowError(ex);
                return;
            }

            var suppliers = _controller.GetActiveSuppliers();

            using var dialog = new InventoryDialogForm(entity, suppliers);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                _controller.UpdateInventoryItem(selected.InventoryId, dialog.ItemName, dialog.Category, dialog.QuantityOnHand, dialog.ReorderLevel, dialog.SupplierId, dialog.IsForSale, dialog.SellingPrice);
                LoadInventory();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void DeleteInventoryItem()
        {
            if (_inventoryGrid.SelectedRows.Count == 0) return;
            if (_inventoryGrid.SelectedRows[0].DataBoundItem is not InventoryRow selected) return;

            if (MessageBox.Show(
                    $"Delete '{selected.ItemName}'? It will no longer appear in this list.",
                    "Delete Inventory Item", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                _controller.DeleteInventoryItem(selected.InventoryId);
                LoadInventory();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void RestockInventoryItem()
        {
            if (_inventoryGrid.SelectedRows.Count == 0) return;
            if (_inventoryGrid.SelectedRows[0].DataBoundItem is not InventoryRow selected) return;

            Inventory entity;
            try
            {
                entity = _controller.GetInventoryItem(selected.InventoryId);
            }
            catch (Exception ex)
            {
                ShowError(ex);
                return;
            }

            var suppliers = _controller.GetActiveSuppliers();

            using var dialog = new RestockDialogForm(entity, suppliers);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                _controller.RestockInventoryItem(selected.InventoryId, dialog.SupplierId, dialog.QuantityReceived, dialog.UnitCost);
                LoadInventory();
                MessageBox.Show("Item restocked successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void SellInventoryItem()
        {
            if (_inventoryGrid.SelectedRows.Count == 0) return;
            if (_inventoryGrid.SelectedRows[0].DataBoundItem is not InventoryRow selected) return;

            Inventory entity;
            try
            {
                entity = _controller.GetInventoryItem(selected.InventoryId);
            }
            catch (Exception ex)
            {
                ShowError(ex);
                return;
            }

            if (!entity.IsForSale)
            {
                MessageBox.Show("This item isn't marked as customer-purchasable. Edit it first to enable selling.", "Sell Item", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var customers = _controller.GetActiveCustomers();
            var bookings = _controller.GetOpenBookings();

            using var dialog = new SellDialogForm(entity, customers, bookings);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                _controller.SellInventoryItem(selected.InventoryId, dialog.Quantity, dialog.CustomerId, dialog.BookingId, dialog.PaymentMethod);
                LoadInventory();
                LoadProductSales();
                MessageBox.Show("Sale recorded successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        // ----- Suppliers tab -----

        private TabPage BuildSupplierTab()
        {
            var page = new TabPage("Suppliers") { BackColor = AppTheme.WhiteColor };

            AppTheme.StyleGrid(_supplierGrid);
            _supplierGrid.Dock = DockStyle.Fill;
            _supplierGrid.ReadOnly = true;
            _supplierGrid.AutoGenerateColumns = true;
            _supplierGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _supplierGrid.MultiSelect = false;

            AppTheme.StyleGrid(_supplierSpendGrid);
            _supplierSpendGrid.Dock = DockStyle.Fill;
            _supplierSpendGrid.ReadOnly = true;
            _supplierSpendGrid.AutoGenerateColumns = true;
            _supplierSpendGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _supplierSpendGrid.MultiSelect = false;

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.WhiteColor
            };

            _addSupplier.Text = "+ Add Supplier";
            _editSupplier.Text = "Edit";
            _deleteSupplier.Text = "Delete";
            _addSupplier.Width = 140;
            _editSupplier.Width = 90;
            _deleteSupplier.Width = 90;
            _addSupplier.Margin = new Padding(0, 0, 8, 0);
            _editSupplier.Margin = new Padding(0, 0, 8, 0);

            _addSupplier.Click += (_, _) => AddSupplier();
            _editSupplier.Click += (_, _) => EditSupplier();
            _deleteSupplier.Click += (_, _) => DeleteSupplier();

            toolbar.Controls.Add(_addSupplier);
            toolbar.Controls.Add(_editSupplier);
            toolbar.Controls.Add(_deleteSupplier);

            var spendLabel = AppTheme.CreateSectionLabel("Supplier Spend History");
            spendLabel.Margin = new Padding(0, 12, 0, 6);
            spendLabel.Dock = DockStyle.Fill;
            var spendHeaderPanel = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.WhiteColor, Padding = new Padding(0, 12, 0, 6) };
            spendHeaderPanel.Controls.Add(spendLabel);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(_supplierGrid, 0, 1);
            layout.Controls.Add(spendHeaderPanel, 0, 2);
            layout.Controls.Add(_supplierSpendGrid, 0, 3);
            page.Controls.Add(layout);

            _addSupplier.Enabled = _canEdit;
            _editSupplier.Enabled = _canEdit;
            _deleteSupplier.Enabled = _canEdit;

            return page;
        }

        private void LoadSuppliers()
        {
            try
            {
                _supplierGrid.DataSource = _controller.GetSuppliers();
                _supplierSpendGrid.DataSource = _controller.GetSupplierSpend();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AddSupplier()
        {
            using var dialog = new SupplierDialogForm(existing: null);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                _controller.AddSupplier(dialog.SupplierName, dialog.ContactPerson, dialog.ContactNumber, dialog.Notes);
                LoadSuppliers();
                MessageBox.Show("Supplier added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void EditSupplier()
        {
            if (_supplierGrid.SelectedRows.Count == 0) return;
            if (_supplierGrid.SelectedRows[0].DataBoundItem is not Supplier selected) return;

            using var dialog = new SupplierDialogForm(selected);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                _controller.UpdateSupplier(selected.SupplierId, dialog.SupplierName, dialog.ContactPerson, dialog.ContactNumber, dialog.Notes);
                LoadSuppliers();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void DeleteSupplier()
        {
            if (_supplierGrid.SelectedRows.Count == 0) return;
            if (_supplierGrid.SelectedRows[0].DataBoundItem is not Supplier selected) return;

            if (MessageBox.Show(
                    $"Delete '{selected.SupplierName}'? Inventory items referencing it keep their history.",
                    "Delete Supplier", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                _controller.DeleteSupplier(selected.SupplierId);
                LoadSuppliers();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        // ----- Purchase Orders tab -----

        private TabPage BuildPurchaseOrdersTab()
        {
            var page = new TabPage("Purchase Orders") { BackColor = AppTheme.WhiteColor };

            AppTheme.StyleGrid(_poGrid);
            _poGrid.Dock = DockStyle.Fill;
            _poGrid.ReadOnly = true;
            _poGrid.AutoGenerateColumns = true;
            _poGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _poGrid.MultiSelect = false;
            AppTheme.StyleStatusColumn(_poGrid, "Status", value => (value?.ToString() ?? string.Empty) switch
            {
                "Received" => ("Received", StatusKind.Success),
                "PartiallyReceived" => ("Partially Received", StatusKind.Warning),
                "Cancelled" => ("Cancelled", StatusKind.Danger),
                "Ordered" => ("Ordered", StatusKind.Info),
                var other => (other, StatusKind.Neutral)
            });

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.WhiteColor
            };

            _addPo.Text = "+ New Purchase Order";
            _editPo.Text = "Edit";
            _receivePo.Text = "Receive";
            _addPo.Width = 180;
            _editPo.Width = 90;
            _receivePo.Width = 90;
            _editPo.Margin = new Padding(0, 0, 8, 0);
            _receivePo.Margin = new Padding(0, 0, 8, 0);

            _addPo.Click += (_, _) => AddPurchaseOrder();
            _editPo.Click += (_, _) => EditPurchaseOrder();
            _receivePo.Click += (_, _) => ReceivePurchaseOrder();

            toolbar.Controls.Add(_addPo);
            toolbar.Controls.Add(_editPo);
            toolbar.Controls.Add(_receivePo);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(_poGrid, 0, 1);
            page.Controls.Add(layout);

            _addPo.Enabled = _canEdit;
            _editPo.Enabled = _canEdit;
            _receivePo.Enabled = _canEdit;

            return page;
        }

        private void LoadPurchaseOrders()
        {
            try
            {
                _poGrid.DataSource = _controller.GetPurchaseOrders();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AddPurchaseOrder()
        {
            var suppliers = _controller.GetActiveSuppliers();
            var items = _controller.GetActiveInventoryItems();

            if (suppliers.Count == 0 || items.Count == 0)
            {
                MessageBox.Show("Add at least one active Supplier and Inventory Item first.", "New Purchase Order", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new PurchaseOrderDialogForm(suppliers, items);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var lines = dialog.Lines.Select(l => new PurchaseOrderLineInput(l.InventoryId, l.QuantityOrdered, l.UnitCost)).ToList();
                _controller.AddPurchaseOrder(dialog.SupplierId, dialog.Status, dialog.OrderDate, dialog.ExpectedDate, dialog.Notes, lines);
                LoadPurchaseOrders();
                MessageBox.Show("Purchase order created successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void EditPurchaseOrder()
        {
            if (_poGrid.SelectedRows.Count == 0) return;
            if (_poGrid.SelectedRows[0].DataBoundItem is not PurchaseOrderRow selected) return;

            PurchaseOrder po;
            try
            {
                po = _controller.GetPurchaseOrderForEdit(selected.PurchaseOrderId);
            }
            catch (Exception ex)
            {
                ShowError(ex);
                return;
            }

            var suppliers = _controller.GetActiveSuppliers();
            var items = _controller.GetActiveInventoryItems();

            using var dialog = new PurchaseOrderDialogForm(suppliers, items, po);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var lines = dialog.Lines.Select(l => new PurchaseOrderLineInput(l.InventoryId, l.QuantityOrdered, l.UnitCost)).ToList();
                _controller.UpdatePurchaseOrder(selected.PurchaseOrderId, dialog.SupplierId, dialog.Status, dialog.OrderDate, dialog.ExpectedDate, dialog.Notes, lines);
                LoadPurchaseOrders();
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        private void ReceivePurchaseOrder()
        {
            if (_poGrid.SelectedRows.Count == 0) return;
            if (_poGrid.SelectedRows[0].DataBoundItem is not PurchaseOrderRow selected) return;

            PurchaseOrder po;
            try
            {
                po = _controller.GetPurchaseOrderForReceive(selected.PurchaseOrderId);
            }
            catch (Exception ex)
            {
                ShowError(ex);
                return;
            }

            using var dialog = new ReceivePurchaseOrderDialogForm(po);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var receipts = dialog.Receipts.Select(r => new PurchaseOrderReceiptInput(r.PurchaseOrderLineId, r.InventoryId, r.QuantityReceivedNow, r.UnitCost)).ToList();
                var expenseAmount = _controller.ReceivePurchaseOrder(selected.PurchaseOrderId, receipts);
                LoadPurchaseOrders();
                LoadInventory();
                MessageBox.Show($"Stock received. ₱{expenseAmount:N2} added to Accounts Payable as an unpaid Expense.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ShowError(ex);
            }
        }

        // ----- Product Sales tab -----

        private TabPage BuildProductSalesTab()
        {
            var page = new TabPage("Product Sales") { BackColor = AppTheme.WhiteColor };

            AppTheme.StyleGrid(_productSalesGrid);
            _productSalesGrid.Dock = DockStyle.Fill;
            _productSalesGrid.ReadOnly = true;
            _productSalesGrid.AutoGenerateColumns = true;
            _productSalesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _productSalesGrid.MultiSelect = false;

            page.Controls.Add(AppTheme.WrapContent(_productSalesGrid));
            return page;
        }

        private void LoadProductSales()
        {
            try
            {
                _productSalesGrid.DataSource = _controller.GetProductSales();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
