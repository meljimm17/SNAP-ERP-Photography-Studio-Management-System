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

        public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
