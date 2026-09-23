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
    /// Inventory Management, with Suppliers managed from a second tab on the same screen rather
    /// than as its own sidebar module.
    /// </summary>
    public class InventoryForm : UserControl
    {
        private readonly DataGridView _inventoryGrid = new();
        private readonly RoundedButton _addItem = new();
        private readonly RoundedButton _editItem = new() { IsPrimary = false };
        private readonly RoundedButton _deleteItem = new() { IsPrimary = false };

        private readonly DataGridView _supplierGrid = new();
        private readonly RoundedButton _addSupplier = new();
        private readonly RoundedButton _editSupplier = new() { IsPrimary = false };
        private readonly RoundedButton _deleteSupplier = new() { IsPrimary = false };

        public InventoryForm(AccessLevel accessLevel = AccessLevel.Full)
        {
            Dock = DockStyle.Fill;
            BackColor = AppTheme.BackgroundColor;

            var canEdit = accessLevel is AccessLevel.Manage or AccessLevel.Full;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            AppTheme.StyleTabControl(tabs);
            tabs.TabPages.Add(BuildInventoryTab(canEdit));
            tabs.TabPages.Add(BuildSupplierTab(canEdit));

            var header = AppTheme.CreatePageHeader("Inventory", "Monitor studio supplies and stock levels.");
            Controls.Add(AppTheme.BuildPage(header, null, AppTheme.WrapContent(tabs)));

            LoadInventory();
            LoadSuppliers();
        }

        private TabPage BuildInventoryTab(bool canEdit)
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
            _addItem.Width = 120;
            _editItem.Width = 90;
            _deleteItem.Width = 90;
            _addItem.Margin = new Padding(0, 0, 8, 0);
            _editItem.Margin = new Padding(0, 0, 8, 0);

            _addItem.Click += (_, _) => AddInventoryItem();
            _editItem.Click += (_, _) => EditInventoryItem();
            _deleteItem.Click += (_, _) => DeleteInventoryItem();

            toolbar.Controls.Add(_addItem);
            toolbar.Controls.Add(_editItem);
            toolbar.Controls.Add(_deleteItem);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(_inventoryGrid, 0, 1);
            page.Controls.Add(layout);

            _addItem.Enabled = canEdit;
            _editItem.Enabled = canEdit;
            _deleteItem.Enabled = canEdit;

            return page;
        }

        private TabPage BuildSupplierTab(bool canEdit)
        {
            var page = new TabPage("Suppliers") { BackColor = AppTheme.WhiteColor };

            AppTheme.StyleGrid(_supplierGrid);
            _supplierGrid.Dock = DockStyle.Fill;
            _supplierGrid.ReadOnly = true;
            _supplierGrid.AutoGenerateColumns = true;
            _supplierGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _supplierGrid.MultiSelect = false;

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

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(_supplierGrid, 0, 1);
            page.Controls.Add(layout);

            _addSupplier.Enabled = canEdit;
            _editSupplier.Enabled = canEdit;
            _deleteSupplier.Enabled = canEdit;

            return page;
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
                        Supplier = x.Supplier != null ? x.Supplier.SupplierName : string.Empty
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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

        private sealed class InventoryRow
        {
            public int InventoryId { get; set; }
            public string ItemName { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public decimal QuantityOnHand { get; set; }
            public decimal ReorderLevel { get; set; }
            public bool LowStock { get; set; }
            public string Supplier { get; set; } = string.Empty;
        }
    }
}
