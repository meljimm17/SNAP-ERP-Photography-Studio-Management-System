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

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
