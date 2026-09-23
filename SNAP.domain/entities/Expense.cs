using SNAP.domain.enums;

namespace SNAP.domain.entities
{
    public class Expense
    {
        public int ExpenseId { get; set; }

        public ExpenseCategory Category { get; set; }

        public string? Description { get; set; }

        public decimal Amount { get; set; }

        public DateTime ExpenseDate { get; set; }

        public int? SupplierId { get; set; }

        public Supplier? Supplier { get; set; }

        public int? EquipmentId { get; set; }

        public Equipment? Equipment { get; set; }

        /// <summary>An unpaid Expense is Accounts Payable - a real liability. Defaults to true so
        /// existing/manually-entered expenses (already paid out of pocket) behave as before.</summary>
        public bool IsPaid { get; set; } = true;

        /// <summary>Set the moment IsPaid flips to true. Required (not left null) once paid, since
        /// the Balance Sheet and Cash Flow Statement need Accounts Payable computed as of a past
        /// date, not just "right now" - a live IsPaid boolean alone can't answer that once an
        /// expense has since been marked paid.</summary>
        public DateTime? PaidDate { get; set; }

        /// <summary>Traces a PO-generated Expense back to the order that generated it.</summary>
        public int? PurchaseOrderId { get; set; }

        public PurchaseOrder? PurchaseOrder { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
