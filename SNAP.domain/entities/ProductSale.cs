using SNAP.domain.enums;

namespace SNAP.domain.entities
{
    /// <summary>
    /// A direct sale of an IsForSale Inventory item to a customer - either a standalone walk-in
    /// sale (Inventory's Sell action) or a booking add-on (Booking Dialog / Payment Management's
    /// Add-ons dialog). IsDeleted marks a reversed sale (a booking add-on removed/reduced on edit,
    /// or restored when its booking is cancelled) - the row and its stock effect are kept for
    /// audit, not erased, the same convention as Payment.
    /// </summary>
    public class ProductSale
    {
        public int ProductSaleId { get; set; }

        public int InventoryId { get; set; }

        public Inventory? Inventory { get; set; }

        public int? CustomerId { get; set; }

        public Customer? Customer { get; set; }

        public int? BookingId { get; set; }

        public Booking? Booking { get; set; }

        public decimal Quantity { get; set; }

        /// <summary>Snapshotted at sale time, so a later change to Inventory.SellingPrice doesn't
        /// rewrite the historical record of what was actually charged.</summary>
        public decimal UnitPrice { get; set; }

        public decimal TotalAmount { get; set; }

        public DateTime SaleDate { get; set; }

        public PaymentMethod PaymentMethod { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>The branch this belongs to. Required - every row sits at exactly one branch.</summary>
        public int BranchId { get; set; }

        public Branch? Branch { get; set; }

    }
}
