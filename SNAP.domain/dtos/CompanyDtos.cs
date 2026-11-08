using SNAP.domain.enums;

namespace SNAP.domain.dtos
{
    /// <summary>
    /// DTOs for the Master/Super Admin company + subscription endpoints. These are the only DTOs
    /// that describe master-database rows rather than one tenant's data.
    /// </summary>

    public sealed class CompanyRow
    {
        public int CompanyId { get; set; }
        public string CompanyCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string PlanTier { get; set; } = string.Empty;
        public string SubscriptionStatus { get; set; } = string.Empty;
        public DateTime SubscriptionStartDate { get; set; }
        public DateTime? SubscriptionRenewalDate { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Set only when CreateCompany's admin-provisioning step failed - the Company and
        /// CompanyDatabase rows were still saved, but this company has no TenantUser yet.</summary>
        public string? AdminProvisioningError { get; set; }

        /// <summary>Set only when CreateCompany's remote-database-provisioning step failed (or
        /// wasn't attempted because the remote was unreachable) - the remote database may still be
        /// missing its schema, so every sync push for this company would fail until it's fixed.</summary>
        public string? RemoteProvisioningError { get; set; }

        /// <summary>A pending tenant-initiated "please upgrade us to this tier" request, or null if
        /// none is pending. Doesn't change what the company can actually access - see Company's own
        /// remarks on the matching fields.</summary>
        public string? RequestedPlanTier { get; set; }
        public DateTime? UpgradeRequestedAt { get; set; }
    }

    public sealed class CreateCompanyRequest
    {
        public string CompanyCode { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public PlanTier PlanTier { get; set; }
        public DateTime? SubscriptionRenewalDate { get; set; }

        /// <summary>
        /// Where this company's local tenant database lives. Required: a company without a
        /// database row can be created but can never be logged into, so both are written together.
        /// </summary>
        public string ServerName { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;

        /// <summary>All four empty means "this company doesn't sync anywhere" - a valid, normal
        /// state. Filling them in is what gives this company real local+MonsterASP dual hosting.</summary>
        public string? RemoteServerName { get; set; }
        public string? RemoteDatabaseName { get; set; }
        public string? RemoteUsername { get; set; }
        public string? RemotePassword { get; set; }

        /// <summary>The company's first TenantUser - without one the company has no way to log in
        /// at all, since its tenant database starts with zero users.</summary>
        public string AdminUsername { get; set; } = string.Empty;
        public string AdminPassword { get; set; } = string.Empty;
    }

    public sealed class UpdateCompanySubscriptionRequest
    {
        public PlanTier PlanTier { get; set; }
        public SubscriptionStatus SubscriptionStatus { get; set; }
        public DateTime? SubscriptionRenewalDate { get; set; }
    }

    /// <summary>A tenant Admin asking to move their own company to a specific tier - just a
    /// request, reviewed and actually applied by a SuperAdmin through the existing Change Plan
    /// flow.</summary>
    public sealed class RequestTierUpgradeRequest
    {
        public PlanTier RequestedTier { get; set; }
    }

    /// <summary>Cross-tenant counts for the Super Admin's BI charts - master database only.</summary>
    public sealed class PlatformStatsDto
    {
        public List<CountByLabel> CompaniesByPlanTier { get; set; } = new();
        public List<CountByLabel> CompaniesBySubscriptionStatus { get; set; } = new();
        public List<CountByLabel> NewCompaniesByMonth { get; set; } = new();
    }

    public sealed class CountByLabel
    {
        public string Label { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
