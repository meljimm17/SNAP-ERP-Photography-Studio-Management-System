using SNAP.domain.enums;

namespace SNAP.domain.entities
{
    public class TenantUser
    {
        public int UserId { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public SystemRole Role { get; set; }

        public UserStatus Status { get; set; } = UserStatus.Active;

        /// <summary>Base monthly salary used by "Run Payroll" to compute statutory deductions and
        /// net pay. Null for a user who isn't currently on payroll.</summary>
        public decimal? MonthlySalary { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// The branch this user works at. Required in practice for Staff/Photographer/BranchManager,
        /// whose data access is filtered to it; null for the company-wide roles (Admin,
        /// FinanceManager, SuperAdmin) which are not tied to any one location.
        /// </summary>
        public int? BranchId { get; set; }

        public Branch? Branch { get; set; }

    }
}
