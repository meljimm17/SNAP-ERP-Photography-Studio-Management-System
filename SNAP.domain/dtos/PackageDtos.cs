using SNAP.domain.enums;

namespace SNAP.domain.dtos
{
    /// <summary>
    /// DTOs for Package API endpoints - shared wire format between client and server.
    /// </summary>

    public sealed class PackageRow
    {
        public int PackageId { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal DurationHours { get; set; }
        public int BookingCount { get; set; }
    }

    public sealed class PackageDetailDto
    {
        public int PackageId { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal DurationHours { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class CreatePackageRequest
    {
        public string PackageName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal DurationHours { get; set; }
    }

    public sealed class UpdatePackageRequest
    {
        public string PackageName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal DurationHours { get; set; }
    }
}
