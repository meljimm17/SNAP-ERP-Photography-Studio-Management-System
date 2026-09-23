namespace SNAP.domain.entities
{
    public class PhotographyPackage
    {
        public int PackageId { get; set; }

        public string PackageName { get; set; } = string.Empty;

        public string? Description { get; set; }

        public decimal Price { get; set; }

        public decimal DurationHours { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
