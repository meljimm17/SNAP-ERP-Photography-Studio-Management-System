using SNAP.domain.enums;

namespace SNAP.domain.dtos
{
    /// <summary>
    /// DTOs for Equipment API endpoints - shared wire format between client and server.
    /// </summary>

    public sealed class EquipmentRow
    {
        public int EquipmentId { get; set; }
        public string EquipmentName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal? AcquisitionCost { get; set; }
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
    }

    public sealed class EquipmentDetailDto
    {
        public int EquipmentId { get; set; }
        public string EquipmentName { get; set; } = string.Empty;
        public EquipmentCategory Category { get; set; }
        public EquipmentStatus Status { get; set; }
        public decimal? AcquisitionCost { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class CreateEquipmentRequest
    {
        public string EquipmentName { get; set; } = string.Empty;
        public EquipmentCategory Category { get; set; }
        public decimal? AcquisitionCost { get; set; }
        /// <summary>Required for company-wide roles; ignored for branch-bound users (their own branch is used).</summary>
        public int? BranchId { get; set; }
    }

    public sealed class UpdateEquipmentRequest
    {
        public string EquipmentName { get; set; } = string.Empty;
        public EquipmentCategory Category { get; set; }
        public EquipmentStatus Status { get; set; }
        public decimal? AcquisitionCost { get; set; }
    }
}
