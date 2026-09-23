using SNAP.domain.enums;

namespace SNAP.domain.entities
{
    public class Payment
    {
        public int PaymentId { get; set; }

        public int BookingId { get; set; }

        public Booking? Booking { get; set; }

        public decimal AmountPaid { get; set; }

        public PaymentMethod PaymentMethod { get; set; }

        public DateTime PaymentDate { get; set; }

        public string? ReferenceNumber { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
