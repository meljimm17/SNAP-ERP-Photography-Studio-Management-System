namespace SNAP.domain.entities
{
    /// <summary>
    /// A physical studio location. Only meaningful for a Medium-tier company: Micro and Small
    /// tenants conceptually have exactly one branch (the one the backfill migration creates) and
    /// never see branch pickers or the Branch Management screen.
    ///
    /// Branches are operationally independent - each holds its own Inventory and Equipment and
    /// places its own Purchase Orders. Nothing transfers stock between them.
    /// </summary>
    public class Branch
    {
        public int BranchId { get; set; }

        public string BranchName { get; set; } = string.Empty;

        public string? Address { get; set; }

        public string? ContactNumber { get; set; }

        /// <summary>
        /// Exactly one branch per company carries this flag. Promoting another branch clears it
        /// from the previous holder, so there is never more or less than one main branch.
        /// </summary>
        public bool IsMainBranch { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
