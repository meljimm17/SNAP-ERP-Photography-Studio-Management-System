namespace SNAP.domain.dtos
{
    /// <summary>
    /// DTOs for Branch API endpoints - shared wire format between client and server.
    /// </summary>

    public sealed class BranchRow
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public bool IsMainBranch { get; set; }
        public bool IsActive { get; set; }

        /// <summary>Staff assigned here - surfaced so an admin can see a branch isn't empty
        /// before retiring it.</summary>
        public int StaffCount { get; set; }
    }

    public sealed class BranchDetailDto
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? ContactNumber { get; set; }
        public bool IsMainBranch { get; set; }
        public bool IsActive { get; set; }
    }

    public sealed class CreateBranchRequest
    {
        public string BranchName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? ContactNumber { get; set; }
        public bool IsMainBranch { get; set; }
    }

    public sealed class UpdateBranchRequest
    {
        public string BranchName { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string? ContactNumber { get; set; }
        public bool IsMainBranch { get; set; }
        public bool IsActive { get; set; }
    }
}
