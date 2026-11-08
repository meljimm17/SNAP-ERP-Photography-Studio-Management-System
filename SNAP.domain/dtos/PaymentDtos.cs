using SNAP.domain.enums;

namespace SNAP.domain.dtos
{
    /// <summary>
    /// DTOs for Payment API endpoints - shared wire format between client and server.
    /// </summary>

    public sealed class PaymentRow
    {
        public int PaymentId { get; set; }
        public int BookingId { get; set; }
        public string Booking { get; set; } = string.Empty;
        public decimal AmountPaid { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public string ReferenceNumber { get; set; } = string.Empty;
    }

    public sealed class PaymentDetailDto
    {
        public int PaymentId { get; set; }
        public int BookingId { get; set; }
        public decimal AmountPaid { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? ReferenceNumber { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class CreatePaymentRequest
    {
        public int BookingId { get; set; }
        public decimal AmountPaid { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? ReferenceNumber { get; set; }
    }

    public sealed class UpdatePaymentRequest
    {
        public decimal AmountPaid { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public DateTime PaymentDate { get; set; }
        public string? ReferenceNumber { get; set; }
    }

    public sealed class BookingPaymentSummaryDto
    {
        public int BookingId { get; set; }
        public decimal BookingPrice { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal RemainingBalance { get; set; }
        public List<PaymentDetailDto> Payments { get; set; } = new();
    }

    public sealed class CreatePaymentResponse
    {
        public int PaymentId { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal RemainingBalance { get; set; }
        public decimal Change { get; set; }
    }
}
