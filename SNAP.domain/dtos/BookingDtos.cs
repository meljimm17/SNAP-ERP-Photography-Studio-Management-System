using SNAP.domain.enums;

namespace SNAP.domain.dtos
{
    /// <summary>
    /// DTOs for Booking API endpoints - shared wire format between client and server.
    /// </summary>

    public sealed class BookingRow
    {
        public int BookingId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Package { get; set; } = string.Empty;
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionStartTime { get; set; }
        public TimeSpan SessionEndTime { get; set; }
        public string Photographer { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Branch { get; set; } = string.Empty;
    }

    public sealed class BookingDetailDto
    {
        public int BookingId { get; set; }
        public int CustomerId { get; set; }
        public int PackageId { get; set; }
        public int? AssignedPhotographerUserId { get; set; }
        public string PackageNameSnapshot { get; set; } = string.Empty;
        public decimal PriceSnapshot { get; set; }
        public decimal DurationSnapshot { get; set; }
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionStartTime { get; set; }
        public TimeSpan SessionEndTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public BookingStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class CreateBookingRequest
    {
        public int CustomerId { get; set; }
        public string? NewCustomerName { get; set; }
        public string? NewCustomerContact { get; set; }
        public string? NewCustomerEmail { get; set; }
        public string? NewCustomerAddress { get; set; }
        public int PackageId { get; set; }
        public int? AssignedPhotographerUserId { get; set; }
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionStartTime { get; set; }
        public TimeSpan SessionEndTime { get; set; }
        public string Location { get; set; } = string.Empty;
        /// <summary>Required for company-wide roles; ignored for branch-bound callers.</summary>
        public int? BranchId { get; set; }
        public List<AddOnSelectionDto> AddOns { get; set; } = new();
    }

    public sealed class UpdateBookingRequest
    {
        public int? AssignedPhotographerUserId { get; set; }
        public DateTime SessionDate { get; set; }
        public TimeSpan SessionStartTime { get; set; }
        public TimeSpan SessionEndTime { get; set; }
        public string Location { get; set; } = string.Empty;
        public BookingStatus Status { get; set; }
        public List<AddOnSelectionDto> AddOns { get; set; } = new();
    }

    public sealed class AddOnSelectionDto
    {
        public int InventoryId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public sealed class AvailableSlotsRequest
    {
        public int PackageId { get; set; }
        public int? PhotographerId { get; set; }
        public DateTime SessionDate { get; set; }
    }

    public sealed class TimeSlotDto
    {
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
    }

    public sealed class BookingLookupDataDto
    {
        public List<BookingCustomerDto> Customers { get; set; } = new();
        public List<BookingPackageDto> Packages { get; set; } = new();
        public List<BookingPhotographerDto> Photographers { get; set; } = new();
        public List<BookingInventoryItemDto> InventoryItems { get; set; } = new();
    }

    public sealed class BookingCustomerDto
    {
        public int CustomerId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public string? EmailAddress { get; set; }
        public string? Address { get; set; }
    }

    public sealed class BookingPackageDto
    {
        public int PackageId { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal DurationHours { get; set; }
    }

    public sealed class BookingPhotographerDto
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
    }

    public sealed class BookingInventoryItemDto
    {
        public int InventoryId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal? SellingPrice { get; set; }
    }
}
