using SNAP.domain.enums;

namespace SNAP.domain.dtos
{
    /// <summary>
    /// DTOs for Customer API endpoints - shared wire format between client and server.
    /// </summary>

    public sealed class CustomerRow
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    public sealed class CustomerDetailDto
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string? EmailAddress { get; set; }
        public string? Address { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class CreateCustomerRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string? EmailAddress { get; set; }
        public string? Address { get; set; }
    }

    public sealed class UpdateCustomerRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string? EmailAddress { get; set; }
        public string? Address { get; set; }
    }

    public sealed class CustomerBookingHistoryDto
    {
        public int BookingId { get; set; }
        public string Package { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionStartTime { get; set; }
        public TimeSpan SessionEndTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal AmountPaid { get; set; }
    }
}
