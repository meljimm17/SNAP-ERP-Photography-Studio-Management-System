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
        /// Optional per-tenant key used to look up this company's database credentials
        /// (e.g. "tenant_a" -> SNAP_TENANTDB_PASSWORD_TENANT_A). Null means "use the
        /// shared/legacy credential env vars", so existing single-tenant setups are unaffected.
        /// </summary>
        public string? CredentialKey { get; set; }

        // Changed from isActive to IsActive (PascalCase)
        public bool IsActive { get; set; } = true;

        public Company? Company { get; set; }
    }
}