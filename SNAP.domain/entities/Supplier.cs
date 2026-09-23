namespace SNAP.domain.entities
{
    public class Supplier
    {
        public int SupplierId { get; set; }

        public string SupplierName { get; set; } = string.Empty;

        public string? ContactPerson { get; set; }

        public string? ContactNumber { get; set; }

        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
