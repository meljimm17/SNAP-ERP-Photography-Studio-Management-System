using System;
using System.Collections.Generic;
using System.Text;

namespace SNAP.domain.entities
{
    public class CompanyDatabase
    {
        public int CompanyDatabaseId { get; set; }

        public int CompanyId { get; set; }

        public string ServerName { get; set; } = string.Empty;

        public string DatabaseName { get; set; } = string.Empty;

        /// <summary>
        /// Legacy/descriptive only - no longer used to resolve the actual connection string (see
        /// ConnectionStrings.BuildTenantConnectionString's remarks). Kept on existing rows rather
        /// than dropped outright.
        /// </summary>
        public string? CredentialKey { get; set; }

        // Changed from isActive to IsActive (PascalCase)
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// This company's own hosted (MonsterASP) database, paired with the local one above the
        /// same way TenantSyncService pairs a local write to its remote push target - except
        /// stored per-company here instead of a handful of hardcoded appsettings.json entries, so
        /// any company created through Add Company gets real sync without editing config files.
        /// All four null together means "this company doesn't sync anywhere" (same meaning as a
        /// tenant with no "..._Remote" appsettings.json entry used to have).
        /// </summary>
        public string? RemoteServerName { get; set; }
        public string? RemoteDatabaseName { get; set; }
        public string? RemoteUsername { get; set; }
        public string? RemotePassword { get; set; }

        public Company? Company { get; set; }
    }
}