namespace SNAP.domain.entities
{
    public class PurchaseOrderLine
    {
        public int PurchaseOrderLineId { get; set; }

        public int PurchaseOrderId { get; set; }

        public PurchaseOrder? PurchaseOrder { get; set; }

        public int InventoryId { get; set; }

        public Inventory? Inventory { get; set; }

        public decimal QuantityOrdered { get; set; }

        public decimal QuantityReceived { get; set; }

        public decimal UnitCost { get; set; }
    }
}
