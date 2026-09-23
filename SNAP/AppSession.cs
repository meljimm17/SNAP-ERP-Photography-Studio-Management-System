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
        /// The logged-in TenantUser's own primary key, used as AuditLog.TenantUserId so an audit
        /// entry is a real FK reference, not just a name string. 0 when no real tenant user record
        /// backs the session (e.g. the offline demo login).
        /// </summary>
        public static int CurrentUserId { get; set; }

        /// <summary>
        /// The current company's subscription tier, used to gate Small-tier-only modules
        /// (Equipment/Inventory/Finance) and Reports sections that Micro was never meant to see.
        /// </summary>
        public static PlanTier CurrentPlanTier { get; set; }

        /// <summary>
        /// Gets or sets the connection string for the current tenant's database. Still used by
        /// modules not yet converted to call SNAP.api instead of talking to the database directly.
        /// </summary>
        public static string? CurrentTenantConnectionString { get; set; }

        /// <summary>
        /// The JWT issued by SNAP.api at login, attached as a bearer token to every ApiClient
        /// request. Set by LoginForm, cleared on logout.
        /// </summary>
        public static string? CurrentApiToken { get; set; }
    }
}
