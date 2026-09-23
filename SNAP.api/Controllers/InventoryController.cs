using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNAP.api.Services;
using SNAP.domain.dtos;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.services;

namespace SNAP.api.Controllers
{
    /// <summary>
    /// Server-side home for everything that used to live directly in the WinForms
    /// InventoryController (SNAP/Controllers/InventoryController.cs) - every TenantErpDbContext
    /// query/save and business-rule check now runs here instead, reached over HTTP. The WinForms
    /// Controller becomes a thin client of these same endpoints; InventoryForm (the View) is
    /// unaffected either way.
    /// </summary>
    [ApiController]
    [Authorize]
    [Route("api/inventory")]
    public class InventoryController : ControllerBase
    {
        private readonly TenantDbContextFactory _tenantDbContextFactory;

        public InventoryController(TenantDbContextFactory tenantDbContextFactory)
        {
            _tenantDbContextFactory = tenantDbContextFactory;
        }

        // ----- Inventory Items -----

        [HttpGet("items")]
        public ActionResult<List<InventoryRow>> GetInventoryItems()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var rows = db.InventoryItems
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
            return Ok(rows);
        }

        [HttpGet("items/active")]
        public ActionResult<List<InventoryItemDto>> GetActiveInventoryItems()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var items = db.InventoryItems.Where(x => x.IsActive).OrderBy(x => x.ItemName)
                .Select(x => ToDto(x)).ToList();
            return Ok(items);
        }

        [HttpGet("items/{id:int}")]
        public ActionResult<InventoryItemDto> GetInventoryItem(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var entity = db.InventoryItems.Find(id);
            if (entity is null) return NotFound(new { message = "That inventory item no longer exists." });
            return Ok(ToDto(entity));
        }

        [HttpPost("items")]
        public IActionResult AddInventoryItem(AddInventoryItemRequest request)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            db.InventoryItems.Add(new Inventory
            {
                ItemName = request.ItemName,
                Category = request.Category,
                QuantityOnHand = request.QuantityOnHand,
                ReorderLevel = request.ReorderLevel,
                SupplierId = request.SupplierId,
                IsForSale = request.IsForSale,
                SellingPrice = request.SellingPrice,
                IsActive = true,
                LastUpdatedAt = DateTime.UtcNow
            });
            Save(db);
            return Ok();
        }

        [HttpPut("items/{id:int}")]
        public IActionResult UpdateInventoryItem(int id, UpdateInventoryItemRequest request)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var entity = db.InventoryItems.Find(id);
            if (entity is null) return NotFound(new { message = "That inventory item no longer exists." });

            entity.ItemName = request.ItemName;
            entity.Category = request.Category;
            entity.QuantityOnHand = request.QuantityOnHand;
            entity.ReorderLevel = request.ReorderLevel;
            entity.SupplierId = request.SupplierId;
            entity.IsForSale = request.IsForSale;
            entity.SellingPrice = request.SellingPrice;
            entity.LastUpdatedAt = DateTime.UtcNow;

            Save(db);
            return Ok();
        }

        [HttpDelete("items/{id:int}")]
        public IActionResult DeleteInventoryItem(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var entity = db.InventoryItems.Find(id);
            if (entity is null) return NotFound(new { message = "That inventory item no longer exists." });

            entity.IsActive = false;
            entity.LastUpdatedAt = DateTime.UtcNow;
            Save(db);
            return Ok();
        }

        [HttpPost("items/{id:int}/restock")]
        public IActionResult RestockInventoryItem(int id, RestockRequest request)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var entity = db.InventoryItems.Find(id);
            if (entity is null) return NotFound(new { message = "That inventory item no longer exists." });

            var supplierName = request.SupplierId is int supplierId
                ? db.Suppliers.Find(supplierId)?.SupplierName ?? "supplier"
                : "no supplier";

            entity.QuantityOnHand += request.QuantityReceived;
            if (request.SupplierId is not null) entity.SupplierId = request.SupplierId;
            if (request.UnitCost is not null) entity.UnitCost = request.UnitCost;
            entity.LastUpdatedAt = DateTime.UtcNow;

            Save(db, entry => entry.Entity == entity
                ? $"Restocked {entity.ItemName}: +{request.QuantityReceived:0.##} units from {supplierName}"
                : null);
            return Ok();
        }

        [HttpPost("items/{id:int}/sell")]
        public IActionResult SellInventoryItem(int id, SellRequest request)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var entity = db.InventoryItems.Find(id);
            if (entity is null) return NotFound(new { message = "That inventory item no longer exists." });

            if (!entity.IsForSale)
            {
                return BadRequest(new { message = "This item isn't marked as customer-purchasable. Edit it first to enable selling." });
            }

            if (request.Quantity > entity.QuantityOnHand)
            {
                return BadRequest(new { message = $"Only {entity.QuantityOnHand:0.##} unit(s) on hand." });
            }

            var unitPrice = entity.SellingPrice ?? 0m;
            db.ProductSales.Add(new ProductSale
            {
                InventoryId = entity.InventoryId,
                CustomerId = request.CustomerId,
                BookingId = request.BookingId,
                Quantity = request.Quantity,
                UnitPrice = unitPrice,
                TotalAmount = request.Quantity * unitPrice,
                SaleDate = DateTime.Today,
                PaymentMethod = request.PaymentMethod,
                CreatedAt = DateTime.UtcNow
            });
            entity.QuantityOnHand -= request.Quantity;
            entity.LastUpdatedAt = DateTime.UtcNow;

            Save(db);
            return Ok();
        }

        // ----- Suppliers -----

        [HttpGet("suppliers")]
        public ActionResult<List<SupplierDto>> GetSuppliers()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var suppliers = db.Suppliers.Where(x => x.IsActive).OrderByDescending(x => x.CreatedAt)
                .Select(x => ToDto(x)).ToList();
            return Ok(suppliers);
        }

        [HttpGet("suppliers/active")]
        public ActionResult<List<SupplierDto>> GetActiveSuppliers()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var suppliers = db.Suppliers.Where(x => x.IsActive).OrderBy(x => x.SupplierName)
                .Select(x => ToDto(x)).ToList();
            return Ok(suppliers);
        }

        [HttpGet("suppliers/spend")]
        public ActionResult<List<SupplierSpendRow>> GetSupplierSpend()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();

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

            var rows = db.Suppliers
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
            return Ok(rows);
        }

        [HttpPost("suppliers")]
        public IActionResult AddSupplier(AddSupplierRequest request)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            db.Suppliers.Add(new Supplier
            {
                SupplierName = request.SupplierName,
                ContactPerson = request.ContactPerson,
                ContactNumber = request.ContactNumber,
                Notes = request.Notes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            Save(db);
            return Ok();
        }

        [HttpPut("suppliers/{id:int}")]
        public IActionResult UpdateSupplier(int id, UpdateSupplierRequest request)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var entity = db.Suppliers.Find(id);
            if (entity is null) return NotFound(new { message = "That supplier no longer exists." });

            entity.SupplierName = request.SupplierName;
            entity.ContactPerson = request.ContactPerson;
            entity.ContactNumber = request.ContactNumber;
            entity.Notes = request.Notes;

            Save(db);
            return Ok();
        }

        [HttpDelete("suppliers/{id:int}")]
        public IActionResult DeleteSupplier(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var entity = db.Suppliers.Find(id);
            if (entity is null) return NotFound(new { message = "That supplier no longer exists." });

            entity.IsActive = false;
            Save(db);
            return Ok();
        }

        // ----- Purchase Orders -----

        [HttpGet("purchase-orders")]
        public ActionResult<List<PurchaseOrderRow>> GetPurchaseOrders()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var rows = db.PurchaseOrders
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
            return Ok(rows);
        }

        [HttpGet("purchase-orders/{id:int}/edit")]
        public ActionResult<PurchaseOrderDetail> GetPurchaseOrderForEdit(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var po = db.PurchaseOrders.Include(p => p.Lines).ThenInclude(l => l.Inventory)
                .FirstOrDefault(p => p.PurchaseOrderId == id);
            if (po is null) return NotFound(new { message = "That purchase order no longer exists." });

            if (po.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Ordered))
            {
                return BadRequest(new { message = "Only Draft or Ordered purchase orders can be edited." });
            }

            return Ok(ToDetail(po));
        }

        [HttpGet("purchase-orders/{id:int}/receive")]
        public ActionResult<PurchaseOrderDetail> GetPurchaseOrderForReceive(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var po = db.PurchaseOrders.Include(p => p.Lines).ThenInclude(l => l.Inventory)
                .FirstOrDefault(p => p.PurchaseOrderId == id);
            if (po is null) return NotFound(new { message = "That purchase order no longer exists." });

            if (po.Status is not (PurchaseOrderStatus.Ordered or PurchaseOrderStatus.PartiallyReceived))
            {
                return BadRequest(new { message = "Only an Ordered or Partially Received purchase order can be received against." });
            }

            return Ok(ToDetail(po));
        }

        [HttpPost("purchase-orders")]
        public IActionResult AddPurchaseOrder(AddPurchaseOrderRequest request)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var supplierName = db.Suppliers.Find(request.SupplierId)?.SupplierName ?? $"Supplier #{request.SupplierId}";

            var po = new PurchaseOrder
            {
                SupplierId = request.SupplierId,
                Status = request.Status,
                OrderDate = request.OrderDate,
                ExpectedDate = request.ExpectedDate,
                CreatedByUserId = _tenantDbContextFactory.GetActorUserId(),
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow
            };
            foreach (var line in request.Lines)
            {
                po.Lines.Add(new PurchaseOrderLine { InventoryId = line.InventoryId, QuantityOrdered = line.QuantityOrdered, UnitCost = line.UnitCost });
            }
            db.PurchaseOrders.Add(po);

            Save(db, entry => entry.Entity == po
                ? $"Created PO for {supplierName}: {po.Lines.Count} line item(s), status {po.Status}"
                : null);
            return Ok();
        }

        [HttpPut("purchase-orders/{id:int}")]
        public IActionResult UpdatePurchaseOrder(int id, UpdatePurchaseOrderRequest request)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var po = db.PurchaseOrders.Include(p => p.Lines).FirstOrDefault(p => p.PurchaseOrderId == id);
            if (po is null) return NotFound(new { message = "That purchase order no longer exists." });

            po.SupplierId = request.SupplierId;
            po.Status = request.Status;
            po.OrderDate = request.OrderDate;
            po.ExpectedDate = request.ExpectedDate;
            po.Notes = request.Notes;

            db.PurchaseOrderLines.RemoveRange(po.Lines);
            po.Lines.Clear();
            foreach (var line in request.Lines)
            {
                po.Lines.Add(new PurchaseOrderLine { InventoryId = line.InventoryId, QuantityOrdered = line.QuantityOrdered, UnitCost = line.UnitCost });
            }

            Save(db);
            return Ok();
        }

        [HttpPost("purchase-orders/{id:int}/receive")]
        public ActionResult<ReceivePurchaseOrderResponse> ReceivePurchaseOrder(int id, ReceivePurchaseOrderRequest request)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var po = db.PurchaseOrders
                .Include(p => p.Supplier)
                .Include(p => p.Lines)
                .FirstOrDefault(p => p.PurchaseOrderId == id);
            if (po is null) return NotFound(new { message = "That purchase order no longer exists." });

            decimal expenseAmount = 0m;
            foreach (var receipt in request.Receipts)
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
            po.ReceivedByUserId = _tenantDbContextFactory.GetActorUserId();

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

            Save(db, entry => entry.Entity == po
                ? $"Received PO #{po.PurchaseOrderId} from {supplierName}: now {po.Status}"
                : entry.Entity == expense
                    ? $"₱{expenseAmount:N2} Accounts Payable created for PO #{po.PurchaseOrderId} ({supplierName})"
                    : null);

            return Ok(new ReceivePurchaseOrderResponse(expenseAmount));
        }

        // ----- Product Sales -----

        [HttpGet("product-sales")]
        public ActionResult<List<ProductSaleRow>> GetProductSales()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var rows = db.ProductSales
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
            return Ok(rows);
        }

        // ----- Reference data used by the Sell dialog -----

        [HttpGet("customers/active")]
        public ActionResult<List<CustomerDto>> GetActiveCustomers()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var customers = db.Customers.Where(x => x.IsActive).OrderBy(x => x.FullName)
                .Select(x => new CustomerDto { CustomerId = x.CustomerId, FullName = x.FullName }).ToList();
            return Ok(customers);
        }

        [HttpGet("bookings/open")]
        public ActionResult<List<BookingOptionDto>> GetOpenBookings()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var bookings = db.Bookings.Where(x => x.Status != BookingStatus.Cancelled).OrderByDescending(x => x.SessionDate)
                .Select(x => new BookingOptionDto { BookingId = x.BookingId, PackageNameSnapshot = x.PackageNameSnapshot, SessionDate = x.SessionDate }).ToList();
            return Ok(bookings);
        }

        // ----- Helpers -----

        private void Save(SNAP.infrastructure.Data.TenantErpDbContext db, Func<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry, string?>? summarySelector = null)
        {
            AuditLogWriter.QueueAuditEntries(db, _tenantDbContextFactory.GetActorUserId(), _tenantDbContextFactory.GetActorName(), summarySelector);
            db.SaveChanges();
        }

        private static InventoryItemDto ToDto(Inventory x) => new()
        {
            InventoryId = x.InventoryId,
            ItemName = x.ItemName,
            Category = x.Category,
            QuantityOnHand = x.QuantityOnHand,
            ReorderLevel = x.ReorderLevel,
            SupplierId = x.SupplierId,
            IsForSale = x.IsForSale,
            SellingPrice = x.SellingPrice,
            UnitCost = x.UnitCost
        };

        private static SupplierDto ToDto(Supplier x) => new()
        {
            SupplierId = x.SupplierId,
            SupplierName = x.SupplierName,
            ContactPerson = x.ContactPerson,
            ContactNumber = x.ContactNumber,
            Notes = x.Notes,
            CreatedAt = x.CreatedAt
        };

        private static PurchaseOrderDetail ToDetail(PurchaseOrder po) => new()
        {
            PurchaseOrderId = po.PurchaseOrderId,
            SupplierId = po.SupplierId,
            Status = po.Status,
            OrderDate = po.OrderDate,
            ExpectedDate = po.ExpectedDate,
            Notes = po.Notes,
            Lines = po.Lines.Select(l => new PurchaseOrderLineDetail
            {
                PurchaseOrderLineId = l.PurchaseOrderLineId,
                InventoryId = l.InventoryId,
                InventoryName = l.Inventory?.ItemName ?? $"Item #{l.InventoryId}",
                QuantityOrdered = l.QuantityOrdered,
                QuantityReceived = l.QuantityReceived,
                UnitCost = l.UnitCost
            }).ToList()
        };
    }
}
