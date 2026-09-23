using Microsoft.EntityFrameworkCore;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP.Controllers
{
    // ----- Display-shape DTOs the View renders (the Controller's output, not the domain Model) -----

    public sealed class InventoryRow
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

    public sealed class SupplierSpendRow
    {
        public string Supplier { get; set; } = string.Empty;
        public int PurchaseOrders { get; set; }
        public decimal TotalSpend { get; set; }
    }

    public sealed class PurchaseOrderRow
    {
        public int PurchaseOrderId { get; set; }
        public string Supplier { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public int Lines { get; set; }
        public decimal OrderTotal { get; set; }
    }

    public sealed class ProductSaleRow
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

    public sealed record PurchaseOrderLineInput(int InventoryId, decimal QuantityOrdered, decimal UnitCost);

    public sealed record PurchaseOrderReceiptInput(int PurchaseOrderLineId, int InventoryId, decimal QuantityReceivedNow, decimal UnitCost);

    /// <summary>
    /// Owns every data access and business-rule decision for the Inventory module (Items,
    /// Suppliers, Purchase Orders, Product Sales) - the Presenter/Controller half of an MVP split.
    /// InventoryForm (the View) only builds/renders controls and forwards user actions here; it
    /// never queries or saves against TenantErpDbContext directly. A business-rule violation (e.g.
    /// "only Draft/Ordered POs can be edited") is reported back as an InvalidOperationException,
    /// which the View catches and shows - the same way a database error already would.
    /// </summary>
    public class InventoryController
    {
        private static TenantErpDbContext CreateDbContext()
        {
            var connectionString = AppSession.CurrentTenantConnectionString
                ?? AppDbConfig.TenantErpConnectionString;

            var options = new DbContextOptionsBuilder<TenantErpDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new TenantErpDbContext(options);
        }

        // ----- Inventory Items -----

        public List<InventoryRow> GetInventoryItems()
        {
            using var db = CreateDbContext();
            return db.InventoryItems
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

        public List<Supplier> GetActiveSuppliers()
        {
            using var db = CreateDbContext();
            return db.Suppliers.Where(x => x.IsActive).OrderBy(x => x.SupplierName).ToList();
        }

        public List<Customer> GetActiveCustomers()
        {
            using var db = CreateDbContext();
            return db.Customers.Where(x => x.IsActive).OrderBy(x => x.FullName).ToList();
        }

        public List<Booking> GetOpenBookings()
        {
            using var db = CreateDbContext();
            return db.Bookings.Where(x => x.Status != BookingStatus.Cancelled).OrderByDescending(x => x.SessionDate).ToList();
        }

        public List<Inventory> GetActiveInventoryItems()
        {
            using var db = CreateDbContext();
            return db.InventoryItems.Where(x => x.IsActive).OrderBy(x => x.ItemName).ToList();
        }

        public Inventory GetInventoryItem(int inventoryId)
        {
            using var db = CreateDbContext();
            return db.InventoryItems.Find(inventoryId)
                ?? throw new InvalidOperationException("That inventory item no longer exists.");
        }

        public void AddInventoryItem(string itemName, string? category, decimal quantityOnHand, decimal reorderLevel, int? supplierId, bool isForSale, decimal? sellingPrice)
        {
            using var db = CreateDbContext();
            db.InventoryItems.Add(new Inventory
            {
                ItemName = itemName,
                Category = category,
                QuantityOnHand = quantityOnHand,
                ReorderLevel = reorderLevel,
                SupplierId = supplierId,
                IsForSale = isForSale,
                SellingPrice = sellingPrice,
                IsActive = true,
                LastUpdatedAt = DateTime.UtcNow
            });
            TenantSyncService.SaveAndSync(db);
        }

        public void UpdateInventoryItem(int inventoryId, string itemName, string? category, decimal quantityOnHand, decimal reorderLevel, int? supplierId, bool isForSale, decimal? sellingPrice)
        {
            using var db = CreateDbContext();
            var entity = db.InventoryItems.Find(inventoryId)
                ?? throw new InvalidOperationException("That inventory item no longer exists.");

            entity.ItemName = itemName;
            entity.Category = category;
            entity.QuantityOnHand = quantityOnHand;
            entity.ReorderLevel = reorderLevel;
            entity.SupplierId = supplierId;
            entity.IsForSale = isForSale;
            entity.SellingPrice = sellingPrice;
            entity.LastUpdatedAt = DateTime.UtcNow;

            TenantSyncService.SaveAndSync(db);
        }

        public void DeleteInventoryItem(int inventoryId)
        {
            using var db = CreateDbContext();
            var entity = db.InventoryItems.Find(inventoryId)
                ?? throw new InvalidOperationException("That inventory item no longer exists.");

            entity.IsActive = false;
            entity.LastUpdatedAt = DateTime.UtcNow;
            TenantSyncService.SaveAndSync(db);
        }

        public void RestockInventoryItem(int inventoryId, int? supplierId, decimal quantityReceived, decimal? unitCost)
        {
            using var db = CreateDbContext();
            var entity = db.InventoryItems.Find(inventoryId)
                ?? throw new InvalidOperationException("That inventory item no longer exists.");

            var supplierName = supplierId is int id
                ? db.Suppliers.Find(id)?.SupplierName ?? "supplier"
                : "no supplier";

            entity.QuantityOnHand += quantityReceived;
            if (supplierId is not null) entity.SupplierId = supplierId;
            if (unitCost is not null) entity.UnitCost = unitCost;
            entity.LastUpdatedAt = DateTime.UtcNow;

            TenantSyncService.SaveAndSync(db, entry => entry.Entity == entity
                ? $"Restocked {entity.ItemName}: +{quantityReceived:0.##} units from {supplierName}"
                : null);
        }

        public void SellInventoryItem(int inventoryId, decimal quantity, int? customerId, int? bookingId, PaymentMethod paymentMethod)
        {
            using var db = CreateDbContext();
            var entity = db.InventoryItems.Find(inventoryId)
                ?? throw new InvalidOperationException("That inventory item no longer exists.");

            if (!entity.IsForSale)
            {
                throw new InvalidOperationException("This item isn't marked as customer-purchasable. Edit it first to enable selling.");
            }

            if (quantity > entity.QuantityOnHand)
            {
                throw new InvalidOperationException($"Only {entity.QuantityOnHand:0.##} unit(s) on hand.");
            }

            var unitPrice = entity.SellingPrice ?? 0m;
            db.ProductSales.Add(new ProductSale
            {
                InventoryId = entity.InventoryId,
                CustomerId = customerId,
                BookingId = bookingId,
                Quantity = quantity,
                UnitPrice = unitPrice,
                TotalAmount = quantity * unitPrice,
                SaleDate = DateTime.Today,
                PaymentMethod = paymentMethod,
                CreatedAt = DateTime.UtcNow
            });
            entity.QuantityOnHand -= quantity;
            entity.LastUpdatedAt = DateTime.UtcNow;

            TenantSyncService.SaveAndSync(db);
        }

        // ----- Suppliers -----

        public List<Supplier> GetSuppliers()
        {
            using var db = CreateDbContext();
            return db.Suppliers.Where(x => x.IsActive).OrderByDescending(x => x.CreatedAt).ToList();
        }

        public List<SupplierSpendRow> GetSupplierSpend()
        {
            using var db = CreateDbContext();

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

            return db.Suppliers
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

        public void AddSupplier(string supplierName, string? contactPerson, string? contactNumber, string? notes)
        {
            using var db = CreateDbContext();
            db.Suppliers.Add(new Supplier
            {
                SupplierName = supplierName,
                ContactPerson = contactPerson,
                ContactNumber = contactNumber,
                Notes = notes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            TenantSyncService.SaveAndSync(db);
        }

        public void UpdateSupplier(int supplierId, string supplierName, string? contactPerson, string? contactNumber, string? notes)
        {
            using var db = CreateDbContext();
            var entity = db.Suppliers.Find(supplierId)
                ?? throw new InvalidOperationException("That supplier no longer exists.");

            entity.SupplierName = supplierName;
            entity.ContactPerson = contactPerson;
            entity.ContactNumber = contactNumber;
            entity.Notes = notes;

            TenantSyncService.SaveAndSync(db);
        }

        public void DeleteSupplier(int supplierId)
        {
            using var db = CreateDbContext();
            var entity = db.Suppliers.Find(supplierId)
                ?? throw new InvalidOperationException("That supplier no longer exists.");

            entity.IsActive = false;
            TenantSyncService.SaveAndSync(db);
        }

        // ----- Purchase Orders -----

        public List<PurchaseOrderRow> GetPurchaseOrders()
        {
            using var db = CreateDbContext();
            return db.PurchaseOrders
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

        public PurchaseOrder GetPurchaseOrderForEdit(int purchaseOrderId)
        {
            using var db = CreateDbContext();
            var po = db.PurchaseOrders.Include(p => p.Lines).FirstOrDefault(p => p.PurchaseOrderId == purchaseOrderId)
                ?? throw new InvalidOperationException("That purchase order no longer exists.");

            if (po.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Ordered))
            {
                throw new InvalidOperationException("Only Draft or Ordered purchase orders can be edited.");
            }

            return po;
        }

        public PurchaseOrder GetPurchaseOrderForReceive(int purchaseOrderId)
        {
            using var db = CreateDbContext();
            var po = db.PurchaseOrders
                .Include(p => p.Supplier)
                .Include(p => p.Lines).ThenInclude(l => l.Inventory)
                .FirstOrDefault(p => p.PurchaseOrderId == purchaseOrderId)
                ?? throw new InvalidOperationException("That purchase order no longer exists.");

            if (po.Status is not (PurchaseOrderStatus.Ordered or PurchaseOrderStatus.PartiallyReceived))
            {
                throw new InvalidOperationException("Only an Ordered or Partially Received purchase order can be received against.");
            }

            return po;
        }

        public void AddPurchaseOrder(int supplierId, PurchaseOrderStatus status, DateTime orderDate, DateTime? expectedDate, string? notes, List<PurchaseOrderLineInput> lines)
        {
            using var db = CreateDbContext();
            var supplierName = db.Suppliers.Find(supplierId)?.SupplierName ?? $"Supplier #{supplierId}";

            var po = new PurchaseOrder
            {
                SupplierId = supplierId,
                Status = status,
                OrderDate = orderDate,
                ExpectedDate = expectedDate,
                CreatedByUserId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : null,
                Notes = notes,
                CreatedAt = DateTime.UtcNow
            };
            foreach (var line in lines)
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
        }

        public void UpdatePurchaseOrder(int purchaseOrderId, int supplierId, PurchaseOrderStatus status, DateTime orderDate, DateTime? expectedDate, string? notes, List<PurchaseOrderLineInput> lines)
        {
            using var db = CreateDbContext();
            var po = db.PurchaseOrders.Include(p => p.Lines).FirstOrDefault(p => p.PurchaseOrderId == purchaseOrderId)
                ?? throw new InvalidOperationException("That purchase order no longer exists.");

            po.SupplierId = supplierId;
            po.Status = status;
            po.OrderDate = orderDate;
            po.ExpectedDate = expectedDate;
            po.Notes = notes;

            db.PurchaseOrderLines.RemoveRange(po.Lines);
            po.Lines.Clear();
            foreach (var line in lines)
            {
                po.Lines.Add(new PurchaseOrderLine
                {
                    InventoryId = line.InventoryId,
                    QuantityOrdered = line.QuantityOrdered,
                    UnitCost = line.UnitCost
                });
            }

            TenantSyncService.SaveAndSync(db);
        }

        /// <summary>Applies received quantities, updates Inventory and the PO's status/receiver,
        /// and creates the partial-receipt-aware unpaid Expense. Returns the Expense amount so the
        /// View can report it back to the user.</summary>
        public decimal ReceivePurchaseOrder(int purchaseOrderId, List<PurchaseOrderReceiptInput> receipts)
        {
            using var db = CreateDbContext();
            var po = db.PurchaseOrders
                .Include(p => p.Supplier)
                .Include(p => p.Lines)
                .FirstOrDefault(p => p.PurchaseOrderId == purchaseOrderId)
                ?? throw new InvalidOperationException("That purchase order no longer exists.");

            decimal expenseAmount = 0m;
            foreach (var receipt in receipts)
            {
                var line = po.Lines.First(l => l.PurchaseOrderLineId == receipt.PurchaseOrderLineId);
                line.QuantityReceived += receipt.QuantityReceivedNow;

                var inventory = db.InventoryItems.Find(receipt.InventoryId);
                if (inventory is not null)
                {
                    inventory.QuantityOnHand += receipt.QuantityReceivedNow;
                    inventory.UnitCost = receipt.UnitCost;
                    inventory.LastUpdatedAt = DateTime.UtcNow;
                }

                expenseAmount += receipt.QuantityReceivedNow * receipt.UnitCost;
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

            return expenseAmount;
        }

        // ----- Product Sales -----

        public List<ProductSaleRow> GetProductSales()
        {
            using var db = CreateDbContext();
            return db.ProductSales
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
    }
}
