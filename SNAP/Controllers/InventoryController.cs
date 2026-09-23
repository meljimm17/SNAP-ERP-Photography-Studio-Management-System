using SNAP.domain.dtos;
using SNAP.domain.entities;
using SNAP.domain.enums;

namespace SNAP.Controllers
{
    /// <summary>
    /// Owns every server call and business-rule decision for the Inventory module (Items,
    /// Suppliers, Purchase Orders, Product Sales) - the Presenter/Controller half of an MVP split.
    /// InventoryForm (the View) only builds/renders controls and forwards user actions here; it
    /// never talks to the network or the database directly.
    ///
    /// This used to open a TenantErpDbContext directly; it now calls SNAP.api over HTTP via
    /// ApiClient instead (see SNAP.api/Controllers/InventoryController.cs for the server side of
    /// each of these calls). A business-rule violation (e.g. "only Draft/Ordered POs can be
    /// edited") still reaches the View as an InvalidOperationException - ApiClient translates the
    /// API's 400/404 responses into the same exception type a local EF error used to throw, so
    /// InventoryForm's existing catch blocks needed no changes.
    ///
    /// The API's DTOs (SNAP.domain.dtos) are the wire format for read models that are already
    /// display-shaped (InventoryRow, PurchaseOrderRow, ...). Where a dialog form still expects a
    /// domain entity type (Supplier, Inventory, Customer, Booking, PurchaseOrder - all plain
    /// POCOs), this maps the DTO into one of those instead of changing the dialogs' constructors.
    /// </summary>
    public class InventoryController
    {
        // ----- Inventory Items -----

        public List<InventoryRow> GetInventoryItems() =>
            ApiClient.Get<List<InventoryRow>>("/api/inventory/items");

        public List<Supplier> GetActiveSuppliers() =>
            ApiClient.Get<List<SupplierDto>>("/api/inventory/suppliers/active").Select(ToSupplier).ToList();

        public List<Customer> GetActiveCustomers() =>
            ApiClient.Get<List<CustomerDto>>("/api/inventory/customers/active")
                .Select(x => new Customer { CustomerId = x.CustomerId, FullName = x.FullName })
                .ToList();

        public List<Booking> GetOpenBookings() =>
            ApiClient.Get<List<BookingOptionDto>>("/api/inventory/bookings/open")
                .Select(x => new Booking { BookingId = x.BookingId, PackageNameSnapshot = x.PackageNameSnapshot, SessionDate = x.SessionDate })
                .ToList();

        public List<Inventory> GetActiveInventoryItems() =>
            ApiClient.Get<List<InventoryItemDto>>("/api/inventory/items/active").Select(ToInventory).ToList();

        public Inventory GetInventoryItem(int inventoryId) =>
            ToInventory(ApiClient.Get<InventoryItemDto>($"/api/inventory/items/{inventoryId}"));

        public void AddInventoryItem(string itemName, string? category, decimal quantityOnHand, decimal reorderLevel, int? supplierId, bool isForSale, decimal? sellingPrice) =>
            ApiClient.Post("/api/inventory/items", new AddInventoryItemRequest(itemName, category, quantityOnHand, reorderLevel, supplierId, isForSale, sellingPrice));

        public void UpdateInventoryItem(int inventoryId, string itemName, string? category, decimal quantityOnHand, decimal reorderLevel, int? supplierId, bool isForSale, decimal? sellingPrice) =>
            ApiClient.Put($"/api/inventory/items/{inventoryId}", new UpdateInventoryItemRequest(itemName, category, quantityOnHand, reorderLevel, supplierId, isForSale, sellingPrice));

        public void DeleteInventoryItem(int inventoryId) =>
            ApiClient.Delete($"/api/inventory/items/{inventoryId}");

        public void RestockInventoryItem(int inventoryId, int? supplierId, decimal quantityReceived, decimal? unitCost) =>
            ApiClient.Post($"/api/inventory/items/{inventoryId}/restock", new RestockRequest(supplierId, quantityReceived, unitCost));

        public void SellInventoryItem(int inventoryId, decimal quantity, int? customerId, int? bookingId, PaymentMethod paymentMethod) =>
            ApiClient.Post($"/api/inventory/items/{inventoryId}/sell", new SellRequest(quantity, customerId, bookingId, paymentMethod));

        // ----- Suppliers -----

        public List<Supplier> GetSuppliers() =>
            ApiClient.Get<List<SupplierDto>>("/api/inventory/suppliers").Select(ToSupplier).ToList();

        public List<SupplierSpendRow> GetSupplierSpend() =>
            ApiClient.Get<List<SupplierSpendRow>>("/api/inventory/suppliers/spend");

        public void AddSupplier(string supplierName, string? contactPerson, string? contactNumber, string? notes) =>
            ApiClient.Post("/api/inventory/suppliers", new AddSupplierRequest(supplierName, contactPerson, contactNumber, notes));

        public void UpdateSupplier(int supplierId, string supplierName, string? contactPerson, string? contactNumber, string? notes) =>
            ApiClient.Put($"/api/inventory/suppliers/{supplierId}", new UpdateSupplierRequest(supplierName, contactPerson, contactNumber, notes));

        public void DeleteSupplier(int supplierId) =>
            ApiClient.Delete($"/api/inventory/suppliers/{supplierId}");

        // ----- Purchase Orders -----

        public List<PurchaseOrderRow> GetPurchaseOrders() =>
            ApiClient.Get<List<PurchaseOrderRow>>("/api/inventory/purchase-orders");

        public PurchaseOrder GetPurchaseOrderForEdit(int purchaseOrderId) =>
            ToPurchaseOrder(ApiClient.Get<PurchaseOrderDetail>($"/api/inventory/purchase-orders/{purchaseOrderId}/edit"));

        public PurchaseOrder GetPurchaseOrderForReceive(int purchaseOrderId) =>
            ToPurchaseOrder(ApiClient.Get<PurchaseOrderDetail>($"/api/inventory/purchase-orders/{purchaseOrderId}/receive"));

        public void AddPurchaseOrder(int supplierId, PurchaseOrderStatus status, DateTime orderDate, DateTime? expectedDate, string? notes, List<PurchaseOrderLineInput> lines) =>
            ApiClient.Post("/api/inventory/purchase-orders", new AddPurchaseOrderRequest(supplierId, status, orderDate, expectedDate, notes, lines));

        public void UpdatePurchaseOrder(int purchaseOrderId, int supplierId, PurchaseOrderStatus status, DateTime orderDate, DateTime? expectedDate, string? notes, List<PurchaseOrderLineInput> lines) =>
            ApiClient.Put($"/api/inventory/purchase-orders/{purchaseOrderId}", new UpdatePurchaseOrderRequest(supplierId, status, orderDate, expectedDate, notes, lines));

        public decimal ReceivePurchaseOrder(int purchaseOrderId, List<PurchaseOrderReceiptInput> receipts) =>
            ApiClient.Post<ReceivePurchaseOrderResponse>($"/api/inventory/purchase-orders/{purchaseOrderId}/receive", new ReceivePurchaseOrderRequest(receipts)).ExpenseAmount;

        // ----- Product Sales -----

        public List<ProductSaleRow> GetProductSales() =>
            ApiClient.Get<List<ProductSaleRow>>("/api/inventory/product-sales");

        // ----- DTO -> domain-entity mapping (dialogs still take these types as parameters) -----

        private static Supplier ToSupplier(SupplierDto x) => new()
        {
            SupplierId = x.SupplierId,
            SupplierName = x.SupplierName,
            ContactPerson = x.ContactPerson,
            ContactNumber = x.ContactNumber,
            Notes = x.Notes,
            IsActive = true,
            CreatedAt = x.CreatedAt
        };

        private static Inventory ToInventory(InventoryItemDto x) => new()
        {
            InventoryId = x.InventoryId,
            ItemName = x.ItemName,
            Category = x.Category,
            QuantityOnHand = x.QuantityOnHand,
            ReorderLevel = x.ReorderLevel,
            SupplierId = x.SupplierId,
            IsForSale = x.IsForSale,
            SellingPrice = x.SellingPrice,
            UnitCost = x.UnitCost,
            IsActive = true
        };

        private static PurchaseOrder ToPurchaseOrder(PurchaseOrderDetail detail) => new()
        {
            PurchaseOrderId = detail.PurchaseOrderId,
            SupplierId = detail.SupplierId,
            Status = detail.Status,
            OrderDate = detail.OrderDate,
            ExpectedDate = detail.ExpectedDate,
            Notes = detail.Notes,
            Lines = detail.Lines.Select(l => new PurchaseOrderLine
            {
                PurchaseOrderLineId = l.PurchaseOrderLineId,
                InventoryId = l.InventoryId,
                QuantityOrdered = l.QuantityOrdered,
                QuantityReceived = l.QuantityReceived,
                UnitCost = l.UnitCost,
                Inventory = new Inventory { InventoryId = l.InventoryId, ItemName = l.InventoryName }
            }).ToList()
        };
    }
}
