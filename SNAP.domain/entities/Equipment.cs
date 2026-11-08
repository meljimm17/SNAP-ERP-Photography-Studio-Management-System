using SNAP.domain.enums;

namespace SNAP.domain.entities
{
    public class Equipment
    {
        public int EquipmentId { get; set; }

        public string EquipmentName { get; set; } = string.Empty;

        public EquipmentCategory Category { get; set; } = EquipmentCategory.PhotographyGear;

        /// <summary>Values Equipment as a fixed asset on the Balance Sheet.</summary>
        public decimal? AcquisitionCost { get; set; }

        public EquipmentStatus Status { get; set; } = EquipmentStatus.Available;

        public int? AssignedToUserId { get; set; }

        public TenantUser? AssignedToUser { get; set; }

        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>The branch this belongs to. Required - every row sits at exactly one branch.</summary>
        public int BranchId { get; set; }

        public Branch? Branch { get; set; }

    }
}
