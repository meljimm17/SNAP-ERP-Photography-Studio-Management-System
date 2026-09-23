namespace SNAP.domain.entities
{
    public class Customer
    {
        public int CustomerId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string ContactNumber { get; set; } = string.Empty;

        public string? EmailAddress { get; set; }

        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
