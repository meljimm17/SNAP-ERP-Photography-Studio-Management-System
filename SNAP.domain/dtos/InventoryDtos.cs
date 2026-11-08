using SNAP.domain.enums;

namespace SNAP.domain.dtos
{
    // Shared wire-format contracts for the Inventory module, used by both the WinForms client's
    // InventoryController (SNAP/Controllers) and the API's InventoryController (SNAP.api/Controllers)
    // - one shape, not two, since neither project can reference the other (SNAP.api can't depend on
    // the WinForms project, and SNAP can't depend on SNAP.api), but both already reference
    // SNAP.domain.

    public sealed class InventoryRow
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
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
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
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

    // ----- Request bodies for the API's write endpoints -----

    /// <summary>BranchId is ignored for a branch-bound caller, who always writes to their own branch.</summary>
    public sealed record AddInventoryItemRequest(string ItemName, string? Category, decimal QuantityOnHand, decimal ReorderLevel, int? SupplierId, bool IsForSale, decimal? SellingPrice, int? BranchId = null);

    public sealed record UpdateInventoryItemRequest(string ItemName, string? Category, decimal QuantityOnHand, decimal ReorderLevel, int? SupplierId, bool IsForSale, decimal? SellingPrice);

    public sealed record RestockRequest(int? SupplierId, decimal QuantityReceived, decimal? UnitCost);

    public sealed record SellRequest(decimal Quantity, int? CustomerId, int? BookingId, PaymentMethod PaymentMethod);

    public sealed record AddSupplierRequest(string SupplierName, string? ContactPerson, string? ContactNumber, string? Notes);

    public sealed record UpdateSupplierRequest(string SupplierName, string? ContactPerson, string? ContactNumber, string? Notes);

    /// <summary>BranchId is ignored for a branch-bound caller, who always writes to their own branch.</summary>
    public sealed record AddPurchaseOrderRequest(int SupplierId, PurchaseOrderStatus Status, DateTime OrderDate, DateTime? ExpectedDate, string? Notes, List<PurchaseOrderLineInput> Lines, int? BranchId = null);

    public sealed record UpdatePurchaseOrderRequest(int SupplierId, PurchaseOrderStatus Status, DateTime OrderDate, DateTime? ExpectedDate, string? Notes, List<PurchaseOrderLineInput> Lines);

    public sealed record ReceivePurchaseOrderRequest(List<PurchaseOrderReceiptInput> Receipts);

    public sealed record ReceivePurchaseOrderResponse(decimal ExpenseAmount);

    // Flat, cycle-free shapes for handing a PurchaseOrder + its lines to the WinForms
    // PurchaseOrderDialogForm/ReceivePurchaseOrderDialogForm - lighter than serializing the raw EF
    // entity graph (which has a PurchaseOrderLine.PurchaseOrder back-reference).

    public sealed class PurchaseOrderLineDetail
    {
        public int PurchaseOrderLineId { get; set; }
        public int InventoryId { get; set; }
        public string InventoryName { get; set; } = string.Empty;
        public decimal QuantityOrdered { get; set; }
        public decimal QuantityReceived { get; set; }
        public decimal UnitCost { get; set; }
    }

    public sealed class PurchaseOrderDetail
    {
        public int PurchaseOrderId { get; set; }
        public int SupplierId { get; set; }
        public PurchaseOrderStatus Status { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public string? Notes { get; set; }
        public List<PurchaseOrderLineDetail> Lines { get; set; } = new();
    }

    public sealed class SupplierDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? ContactNumber { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class InventoryItemDto
    {
        public int InventoryId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public decimal QuantityOnHand { get; set; }
        public decimal ReorderLevel { get; set; }
        public int? SupplierId { get; set; }
        public bool IsForSale { get; set; }
        public decimal? SellingPrice { get; set; }
        public decimal? UnitCost { get; set; }
    }

    public sealed class CustomerDto
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
    }

    public sealed class BookingOptionDto
    {
        public int BookingId { get; set; }
        public string PackageNameSnapshot { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
    }
}
