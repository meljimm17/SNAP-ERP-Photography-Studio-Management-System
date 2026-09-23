namespace SNAP.domain.entities
{
    public class Inventory
    {
        public int InventoryId { get; set; }

        public string ItemName { get; set; } = string.Empty;

        public string? Category { get; set; }

        public decimal QuantityOnHand { get; set; }

        public decimal ReorderLevel { get; set; }

        public int? SupplierId { get; set; }

        public Supplier? Supplier { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>Whether this item can be sold directly to a customer (e.g. polaroids, prints)
        /// as opposed to being an internal-only supply.</summary>
        public bool IsForSale { get; set; }

        /// <summary>Price charged to a customer when sold. Only meaningful when IsForSale.</summary>
        public decimal? SellingPrice { get; set; }

        /// <summary>Last cost paid for this item, updated by Restock and PO receiving - values
        /// Inventory as an asset on the Balance Sheet.</summary>
        public decimal? UnitCost { get; set; }

        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
