using SNAP.domain.enums;

namespace SNAP
{
    public static class AppSession
    {
        public static string? CurrentCompanyCode { get; set; }
        public static int CurrentCompanyId { get; set; }
        public static string? CurrentUsername { get; set; }
        public static string? CurrentDisplayName { get; set; }
        public static SystemRole CurrentRole { get; set; }

        /// <summary>
        /// The current company's subscription tier, used to gate Small-tier-only modules
        /// (Equipment/Inventory/Finance) and Reports sections that Micro was never meant to see.
        /// </summary>
        public static PlanTier CurrentPlanTier { get; set; }

        /// <summary>
        /// Gets or sets the connection string for the current tenant's database.
        /// </summary>
        public static string? CurrentTenantConnectionString { get; set; }
    }
}
