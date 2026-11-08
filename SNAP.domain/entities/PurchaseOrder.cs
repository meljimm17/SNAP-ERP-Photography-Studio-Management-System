using SNAP.domain.enums;

namespace SNAP.domain.entities
{
    public class PurchaseOrder
    {
        public int PurchaseOrderId { get; set; }

        public int SupplierId { get; set; }

        public Supplier? Supplier { get; set; }

        public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

        public DateTime OrderDate { get; set; }

        public DateTime? ExpectedDate { get; set; }

        /// <summary>Who created/placed the order.</summary>
        public int? CreatedByUserId { get; set; }

        public TenantUser? CreatedByUser { get; set; }

        /// <summary>Who executed the (most recent) Receive action - often different staff from
        /// whoever placed the order.</summary>
        public int? ReceivedByUserId { get; set; }

        public TenantUser? ReceivedByUser { get; set; }

        public string? Notes { get; set; }

        public List<PurchaseOrderLine> Lines { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>The branch this belongs to. Required - every row sits at exactly one branch.</summary>
        public int BranchId { get; set; }

        public Branch? Branch { get; set; }

    }
}
