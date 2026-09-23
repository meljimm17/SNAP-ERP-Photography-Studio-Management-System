using SNAP.domain.enums;

namespace SNAP.domain.entities
{
    public class Booking
    {
        public int BookingId { get; set; }

        public int CustomerId { get; set; }

        public Customer? Customer { get; set; }

        public int PackageId { get; set; }

        public PhotographyPackage? Package { get; set; }

        public int? AssignedPhotographerUserId { get; set; }

        public TenantUser? AssignedPhotographer { get; set; }

        public string PackageNameSnapshot { get; set; } = string.Empty;

        public decimal PriceSnapshot { get; set; }

        public decimal DurationSnapshot { get; set; }

        public DateTime SessionDate { get; set; }

        public TimeSpan SessionStartTime { get; set; }

        public TimeSpan SessionEndTime { get; set; }

        public string Location { get; set; } = string.Empty;

        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
