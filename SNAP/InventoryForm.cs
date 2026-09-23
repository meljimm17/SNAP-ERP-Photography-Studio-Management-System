using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP
{
    /// <summary>
    /// Inventory Management: Inventory Items, Suppliers (with a spend-history panel), Purchase
    /// Orders, and Product Sales history, on four tabs of one screen.
    /// </summary>
    public class InventoryForm : UserControl
    {
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
                using var db = CreateDbContext();
                _inventoryGrid.DataSource = db.InventoryItems
                    .Include(x => x.Supplier)
                    .Where(x => x.IsActive)
                    .OrderByDescending(x => x.LastUpdatedAt)
                    .Select(x => new InventoryRow
                    {
                        InventoryId = x.InventoryId,
                        ItemName = x.ItemName,
                        Category = x.Category ?? string.Empty,
                        QuantityOnHand = x.QuantityOnHand,
                        ReorderLevel = x.ReorderLevel,
                        LowStock = x.QuantityOnHand <= x.ReorderLevel,
                        Supplier = x.Supplier != null ? x.Supplier.SupplierName : string.Empty,
                        IsForSale = x.IsForSale,
                        SellingPrice = x.SellingPrice,
                        UnitCost = x.UnitCost
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AddInventoryItem()
        {
            using var db = CreateDbContext();
            var suppliers = db.Suppliers.Where(x => x.IsActive).OrderBy(x => x.SupplierName).ToList();

            using var dialog = new InventoryDialogForm(existing: null, suppliers);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            db.InventoryItems.Add(new Inventory
            {
                ItemName = dialog.ItemName,
                Category = dialog.Category,
                QuantityOnHand = dialog.QuantityOnHand,
                ReorderLevel = dialog.ReorderLevel,
                SupplierId = dialog.SupplierId,
                IsForSale = dialog.IsForSale,
                SellingPrice = dialog.SellingPrice,
                IsActive = true,
                LastUpdatedAt = DateTime.UtcNow
            });
            TenantSyncService.SaveAndSync(db);
            LoadInventory();

            MessageBox.Show("Inventory item added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditInventoryItem()
        {
            if (_inventoryGrid.SelectedRows.Count == 0) return;

            var selected = (InventoryRow)_inventoryGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            using var db = CreateDbContext();
            var entity = db.InventoryItems.Find(selected.InventoryId);
            if (entity is null) return;

            var suppliers = db.Suppliers.Where(x => x.IsActive).OrderBy(x => x.SupplierName).ToList();

            using var dialog = new InventoryDialogForm(entity, suppliers);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            entity.ItemName = dialog.ItemName;
            entity.Category = dialog.Category;
            entity.QuantityOnHand = dialog.QuantityOnHand;
            entity.ReorderLevel = dialog.ReorderLevel;
            entity.SupplierId = dialog.SupplierId;
            entity.IsForSale = dialog.IsForSale;
            entity.SellingPrice = dialog.SellingPrice;
            entity.LastUpdatedAt = DateTime.UtcNow;

            TenantSyncService.SaveAndSync(db);
            LoadInventory();
        }

        private void DeleteInventoryItem()
        {
            if (_inventoryGrid.SelectedRows.Count == 0) return;

            var selected = (InventoryRow)_inventoryGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            if (MessageBox.Show(
                    $"Delete '{selected.ItemName}'? It will no longer appear in this list.",
                    "Delete Inventory Item", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            using var db = CreateDbContext();
            var entity = db.InventoryItems.Find(selected.InventoryId);
            if (entity is null) return;

            entity.IsActive = false;
            entity.LastUpdatedAt = DateTime.UtcNow;
            TenantSyncService.SaveAndSync(db);
            LoadInventory();
        }

        private void RestockInventoryItem()
        {
            if (_inventoryGrid.SelectedRows.Count == 0) return;

            var selected = (InventoryRow)_inventoryGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            using var db = CreateDbContext();
            var entity = db.InventoryItems.Find(selected.InventoryId);
            if (entity is null) return;

            var suppliers = db.Suppliers.Where(x => x.IsActive).OrderBy(x => x.SupplierName).ToList();

            using var dialog = new RestockDialogForm(entity, suppliers);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var supplierName = dialog.SupplierId is int supplierId
                ? suppliers.FirstOrDefault(s => s.SupplierId == supplierId)?.SupplierName ?? "supplier"
                : "no supplier";

            entity.QuantityOnHand += dialog.QuantityReceived;
            if (dialog.SupplierId is not null) entity.SupplierId = dialog.SupplierId;
            if (dialog.UnitCost is not null) entity.UnitCost = dialog.UnitCost;
            entity.LastUpdatedAt = DateTime.UtcNow;

            TenantSyncService.SaveAndSync(db, entry => entry.Entity == entity
                ? $"Restocked {entity.ItemName}: +{dialog.QuantityReceived:0.##} units from {supplierName}"
                : null);
            LoadInventory();

            MessageBox.Show("Item restocked successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SellInventoryItem()
        {
            if (_inventoryGrid.SelectedRows.Count == 0) return;

            var selected = (InventoryRow)_inventoryGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            if (!selected.IsForSale)
            {
                MessageBox.Show("This item isn't marked as customer-purchasable. Edit it first to enable selling.", "Sell Item", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var db = CreateDbContext();
            var entity = db.InventoryItems.Find(selected.InventoryId);
            if (entity is null) return;

            var customers = db.Customers.Where(x => x.IsActive).OrderBy(x => x.FullName).ToList();
            var bookings = db.Bookings.Where(x => x.Status != BookingStatus.Cancelled).OrderByDescending(x => x.SessionDate).ToList();

            using var dialog = new SellDialogForm(entity, customers, bookings);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var unitPrice = entity.SellingPrice ?? 0m;
            var sale = new ProductSale
            {
                InventoryId = entity.InventoryId,
                CustomerId = dialog.CustomerId,
                BookingId = dialog.BookingId,
                Quantity = dialog.Quantity,
                UnitPrice = unitPrice,
                TotalAmount = dialog.Quantity * unitPrice,
                SaleDate = DateTime.Today,
                PaymentMethod = dialog.PaymentMethod,
                CreatedAt = DateTime.UtcNow
            };
            db.ProductSales.Add(sale);
            entity.QuantityOnHand -= dialog.Quantity;
            entity.LastUpdatedAt = DateTime.UtcNow;

            TenantSyncService.SaveAndSync(db);
            LoadInventory();
            LoadProductSales();

            MessageBox.Show("Sale recorded successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                using var db = CreateDbContext();
                _supplierGrid.DataSource = db.Suppliers
                    .Where(x => x.IsActive)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToList();

                var spendBySupplier = db.PurchaseOrders
                    .Include(p => p.Lines)
                    .Where(p => p.Status != PurchaseOrderStatus.Cancelled)
                    .AsEnumerable()
                    .GroupBy(p => p.SupplierId)
                    .ToDictionary(g => g.Key, g => new
                    {
                        OrderCount = g.Count(),
                        Spend = g.SelectMany(p => p.Lines).Sum(l => l.QuantityReceived * l.UnitCost)
                    });

                _supplierSpendGrid.DataSource = db.Suppliers
                    .Where(x => x.IsActive)
                    .OrderBy(x => x.SupplierName)
                    .AsEnumerable()
                    .Select(s => new SupplierSpendRow
                    {
                        Supplier = s.SupplierName,
                        PurchaseOrders = spendBySupplier.GetValueOrDefault(s.SupplierId)?.OrderCount ?? 0,
                        TotalSpend = spendBySupplier.GetValueOrDefault(s.SupplierId)?.Spend ?? 0m
                    })
                    .ToList();
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

            using var db = CreateDbContext();
            db.Suppliers.Add(new Supplier
            {
                SupplierName = dialog.SupplierName,
                ContactPerson = dialog.ContactPerson,
                ContactNumber = dialog.ContactNumber,
                Notes = dialog.Notes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            TenantSyncService.SaveAndSync(db);
            LoadSuppliers();

            MessageBox.Show("Supplier added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditSupplier()
        {
            if (_supplierGrid.SelectedRows.Count == 0) return;

            var selected = (Supplier)_supplierGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            using var db = CreateDbContext();
            var entity = db.Suppliers.Find(selected.SupplierId);
            if (entity is null) return;

            using var dialog = new SupplierDialogForm(entity);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            entity.SupplierName = dialog.SupplierName;
            entity.ContactPerson = dialog.ContactPerson;
            entity.ContactNumber = dialog.ContactNumber;
            entity.Notes = dialog.Notes;

            TenantSyncService.SaveAndSync(db);
            LoadSuppliers();
        }

        private void DeleteSupplier()
        {
            if (_supplierGrid.SelectedRows.Count == 0) return;

            var selected = (Supplier)_supplierGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            if (MessageBox.Show(
                    $"Delete '{selected.SupplierName}'? Inventory items referencing it keep their history.",
                    "Delete Supplier", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            using var db = CreateDbContext();
            var entity = db.Suppliers.Find(selected.SupplierId);
            if (entity is null) return;

            entity.IsActive = false;
            TenantSyncService.SaveAndSync(db);
            LoadSuppliers();
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
                using var db = CreateDbContext();
                _poGrid.DataSource = db.PurchaseOrders
                    .Include(p => p.Supplier)
                    .Include(p => p.Lines)
                    .OrderByDescending(p => p.CreatedAt)
                    .AsEnumerable()
                    .Select(p => new PurchaseOrderRow
                    {
                        PurchaseOrderId = p.PurchaseOrderId,
                        Supplier = p.Supplier?.SupplierName ?? $"Supplier #{p.SupplierId}",
                        Status = p.Status.ToString(),
                        OrderDate = p.OrderDate,
                        ExpectedDate = p.ExpectedDate,
                        Lines = p.Lines.Count,
                        OrderTotal = p.Lines.Sum(l => l.QuantityOrdered * l.UnitCost)
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AddPurchaseOrder()
        {
            using var db = CreateDbContext();
            var suppliers = db.Suppliers.Where(x => x.IsActive).OrderBy(x => x.SupplierName).ToList();
            var items = db.InventoryItems.Where(x => x.IsActive).OrderBy(x => x.ItemName).ToList();

            if (suppliers.Count == 0 || items.Count == 0)
            {
                MessageBox.Show("Add at least one active Supplier and Inventory Item first.", "New Purchase Order", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new PurchaseOrderDialogForm(suppliers, items);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var supplierName = suppliers.First(s => s.SupplierId == dialog.SupplierId).SupplierName;

            var po = new PurchaseOrder
            {
                SupplierId = dialog.SupplierId,
                Status = dialog.Status,
                OrderDate = dialog.OrderDate,
                ExpectedDate = dialog.ExpectedDate,
                CreatedByUserId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : null,
                Notes = dialog.Notes,
                CreatedAt = DateTime.UtcNow
            };
            foreach (var line in dialog.Lines)
            {
                po.Lines.Add(new PurchaseOrderLine
                {
                    InventoryId = line.InventoryId,
                    QuantityOrdered = line.QuantityOrdered,
                    UnitCost = line.UnitCost
                });
            }
            db.PurchaseOrders.Add(po);

            TenantSyncService.SaveAndSync(db, entry => entry.Entity == po
                ? $"Created PO for {supplierName}: {po.Lines.Count} line item(s), status {po.Status}"
                : null);
            LoadPurchaseOrders();

            MessageBox.Show("Purchase order created successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditPurchaseOrder()
        {
            if (_poGrid.SelectedRows.Count == 0) return;
            var selected = (PurchaseOrderRow)_poGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            using var db = CreateDbContext();
            var po = db.PurchaseOrders.Include(p => p.Lines).FirstOrDefault(p => p.PurchaseOrderId == selected.PurchaseOrderId);
            if (po is null) return;

            if (po.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Ordered))
            {
                MessageBox.Show("Only Draft or Ordered purchase orders can be edited.", "Edit Purchase Order", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var suppliers = db.Suppliers.Where(x => x.IsActive).OrderBy(x => x.SupplierName).ToList();
            var items = db.InventoryItems.Where(x => x.IsActive).OrderBy(x => x.ItemName).ToList();

            using var dialog = new PurchaseOrderDialogForm(suppliers, items, po);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            po.SupplierId = dialog.SupplierId;
            po.Status = dialog.Status;
            po.OrderDate = dialog.OrderDate;
            po.ExpectedDate = dialog.ExpectedDate;
            po.Notes = dialog.Notes;

            db.PurchaseOrderLines.RemoveRange(po.Lines);
            po.Lines.Clear();
            foreach (var line in dialog.Lines)
            {
                po.Lines.Add(new PurchaseOrderLine
                {
                    InventoryId = line.InventoryId,
                    QuantityOrdered = line.QuantityOrdered,
                    UnitCost = line.UnitCost
                });
            }

            TenantSyncService.SaveAndSync(db);
            LoadPurchaseOrders();
        }

        private void ReceivePurchaseOrder()
        {
            if (_poGrid.SelectedRows.Count == 0) return;
            var selected = (PurchaseOrderRow)_poGrid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            using var db = CreateDbContext();
            var po = db.PurchaseOrders
                .Include(p => p.Supplier)
                .Include(p => p.Lines).ThenInclude(l => l.Inventory)
                .FirstOrDefault(p => p.PurchaseOrderId == selected.PurchaseOrderId);
            if (po is null) return;

            if (po.Status is not (PurchaseOrderStatus.Ordered or PurchaseOrderStatus.PartiallyReceived))
            {
                MessageBox.Show("Only an Ordered or Partially Received purchase order can be received against.", "Receive Purchase Order", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dialog = new ReceivePurchaseOrderDialogForm(po);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            decimal expenseAmount = 0m;
            foreach (var (lineId, inventoryId, quantityNow, unitCost) in dialog.Receipts)
            {
                var line = po.Lines.First(l => l.PurchaseOrderLineId == lineId);
                line.QuantityReceived += quantityNow;

                var inventory = db.InventoryItems.Find(inventoryId);
                if (inventory is not null)
                {
                    inventory.QuantityOnHand += quantityNow;
                    inventory.UnitCost = unitCost;
                    inventory.LastUpdatedAt = DateTime.UtcNow;
                }

                expenseAmount += quantityNow * unitCost;
            }

            po.Status = po.Lines.All(l => l.QuantityReceived >= l.QuantityOrdered)
                ? PurchaseOrderStatus.Received
                : PurchaseOrderStatus.PartiallyReceived;
            po.ReceivedByUserId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : null;

            var supplierName = po.Supplier?.SupplierName ?? $"Supplier #{po.SupplierId}";
            var expense = new Expense
            {
                Category = ExpenseCategory.Supplies,
                Description = $"PO #{po.PurchaseOrderId} received from {supplierName}",
                Amount = expenseAmount,
                ExpenseDate = DateTime.Today,
                SupplierId = po.SupplierId,
                PurchaseOrderId = po.PurchaseOrderId,
                IsPaid = false,
                PaidDate = null,
                CreatedAt = DateTime.UtcNow
            };
            db.Expenses.Add(expense);

            TenantSyncService.SaveAndSync(db, entry => entry.Entity == po
                ? $"Received PO #{po.PurchaseOrderId} from {supplierName}: now {po.Status}"
                : entry.Entity == expense
                    ? $"₱{expenseAmount:N2} Accounts Payable created for PO #{po.PurchaseOrderId} ({supplierName})"
                    : null);

            LoadPurchaseOrders();
            LoadInventory();

            MessageBox.Show($"Stock received. ₱{expenseAmount:N2} added to Accounts Payable as an unpaid Expense.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                using var db = CreateDbContext();
                _productSalesGrid.DataSource = db.ProductSales
                    .Include(p => p.Inventory)
                    .Include(p => p.Customer)
                    .Include(p => p.Booking)
                    .Where(p => !p.IsDeleted)
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => new ProductSaleRow
                    {
                        SaleDate = p.SaleDate,
                        Item = p.Inventory != null ? p.Inventory.ItemName : $"Item #{p.InventoryId}",
                        Customer = p.Customer != null ? p.Customer.FullName : "Walk-in",
                        Booking = p.Booking != null ? $"#{p.Booking.BookingId} — {p.Booking.PackageNameSnapshot}" : "—",
                        Quantity = p.Quantity,
                        UnitPrice = p.UnitPrice,
                        TotalAmount = p.TotalAmount,
                        PaymentMethod = p.PaymentMethod.ToString()
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static TenantErpDbContext CreateDbContext()
        {
            var connectionString = AppSession.CurrentTenantConnectionString
                ?? AppDbConfig.TenantErpConnectionString;

            var options = new DbContextOptionsBuilder<TenantErpDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new TenantErpDbContext(options);
        }

        private sealed class InventoryRow
        {
            public int InventoryId { get; set; }
            public string ItemName { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public decimal QuantityOnHand { get; set; }
            public decimal ReorderLevel { get; set; }
            public bool LowStock { get; set; }
            public string Supplier { get; set; } = string.Empty;
            public bool IsForSale { get; set; }
            public decimal? SellingPrice { get; set; }
            public decimal? UnitCost { get; set; }
        }

        private sealed class SupplierSpendRow
        {
            public string Supplier { get; set; } = string.Empty;
            public int PurchaseOrders { get; set; }
            public decimal TotalSpend { get; set; }
        }

        private sealed class PurchaseOrderRow
        {
            public int PurchaseOrderId { get; set; }
            public string Supplier { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTime OrderDate { get; set; }
            public DateTime? ExpectedDate { get; set; }
            public int Lines { get; set; }
            public decimal OrderTotal { get; set; }
        }

        private sealed class ProductSaleRow
        {
            public DateTime SaleDate { get; set; }
            public string Item { get; set; } = string.Empty;
            public string Customer { get; set; } = string.Empty;
            public string Booking { get; set; } = string.Empty;
            public decimal Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal TotalAmount { get; set; }
            public string PaymentMethod { get; set; } = string.Empty;
        }
    }
}
