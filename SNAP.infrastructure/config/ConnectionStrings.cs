using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SNAP.domain.config;

namespace SNAP.infrastructure.config
{
    /// <summary>
    /// Single source of truth for resolving database connection strings. MasterErp and each keyed
    /// tenant (TenantErp_TENANT_A/B/C/...) are read from appsettings.json's ConnectionStrings
    /// section, using the same SQL Server Authentication style for every one of them - see
    /// README.md for the local setup.
    /// </summary>
    public static class ConnectionStrings
    {
        private static readonly IConfiguration Configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        public static string MasterErp => GetRequired("MasterErp");

        // There used to be a separate generic "TenantErp" key that every no-credential-key company
        // fell back to - it happened to point at the exact same database as Tenant A, which was
        // confusing rather than meaningful (nothing distinguished "the default tenant" from "Tenant
        // A" except an extra config entry duplicating the same connection string). Tenant A now
        // plays that role explicitly instead: it's still the fallback for a company with no
        // CredentialKey of its own, and the one target for EF's design-time tooling
        // (TenantErpDbContext.OnConfiguring) - just named for what it actually is.
        public static string TenantErpDefault => GetRequired("TenantErp_TENANT_A");

        /// <summary>
        /// Builds a local tenant database's connection string directly from its own
        /// ServerName/DatabaseName (Windows Integrated Security, matching how every local tenant
        /// database in this project is actually set up - CredentialKey is no longer consulted
        /// here). ServerName/DatabaseName are required fields on the Add Company dialog, so a
        /// properly provisioned company always has both.
        /// </summary>
        public static string BuildTenantConnectionString(string serverName, string databaseName, string? credentialKey = null)
        {
            if (string.IsNullOrWhiteSpace(serverName) || string.IsNullOrWhiteSpace(databaseName))
            {
                // Only reachable for a CompanyDatabase row predating required ServerName/
                // DatabaseName - same legacy fallback this method always had.
                return TenantErpDefault;
            }

            return $"Server={serverName};Database={databaseName};Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Connect Timeout=10;";
        }

        /// <summary>
        /// Builds a company's remote (MonsterASP) connection string from its CompanyDatabase row's
        /// Remote* columns, or null if any piece is missing - remote hosting is opt-in per company,
        /// same meaning a tenant with no "..._Remote" appsettings.json entry used to have.
        /// </summary>
        public static string? BuildRemoteTenantConnectionString(string? remoteServerName, string? remoteDatabaseName, string? remoteUsername, string? remotePassword)
        {
            if (string.IsNullOrWhiteSpace(remoteServerName) || string.IsNullOrWhiteSpace(remoteDatabaseName)
                || string.IsNullOrWhiteSpace(remoteUsername) || string.IsNullOrWhiteSpace(remotePassword))
            {
                return null;
            }

            return $"Server={remoteServerName};Database={remoteDatabaseName};User Id={remoteUsername};Password={remotePassword};Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Connect Timeout=30;";
        }

        private static string GetRequired(string connectionStringKey)
        {
            return Configuration.GetConnectionString(connectionStringKey)
                ?? throw new InvalidOperationException(
                    $"Connection string 'ConnectionStrings:{connectionStringKey}' is not configured in appsettings.json.");
        }

        /// <summary>
        /// Finds which TenantErp_{KEY} entry a given local connection string came from, and returns
        /// that same key's _Remote counterpart if one is configured - the pairing TenantSyncService
        /// needs to push a local save to the correct tenant's own remote database rather than a
        /// single global target. Returns null (not an error) when there's no match or no _Remote
        /// entry for it - background sync is opt-in per tenant just by whether a "..._Remote" key
        /// exists next to its local one, nothing else to configure.
        ///
        /// Compares parsed DataSource+InitialCatalog, not raw text: a connection string handed
        /// back by EF/SqlClient (e.g. TenantErpDbContext.Database.GetConnectionString()) is
        /// normalized - "Server=" becomes "Data Source=", "Database=" becomes "Initial Catalog=",
        /// properties get reordered, and SqlClient appends its own Application Name - so it is
        /// never byte-identical to the raw string sitting in appsettings.json even when it's the
        /// exact same database. SqlConnectionStringBuilder parses either form the same way,
        /// sidestepping all of that.
        /// </summary>
        public static string? TryResolveRemoteForLocal(string localConnectionString)
        {
            SqlConnectionStringBuilder local;
            try
            {
                local = new SqlConnectionStringBuilder(localConnectionString);
            }
            catch (ArgumentException)
            {
                return null;
            }

            var fromCompanyDatabase = TryResolveRemoteFromCompanyDatabase(local);
            if (fromCompanyDatabase is not null)
            {
                return fromCompanyDatabase;
            }

            var tenantKeys = Configuration.GetSection("ConnectionStrings").GetChildren()
                .Where(entry => entry.Key.StartsWith("TenantErp_", StringComparison.OrdinalIgnoreCase)
                                && !entry.Key.EndsWith("_Remote", StringComparison.OrdinalIgnoreCase));

            foreach (var entry in tenantKeys)
            {
                if (string.IsNullOrWhiteSpace(entry.Value)) continue;

                SqlConnectionStringBuilder candidate;
                try
                {
                    candidate = new SqlConnectionStringBuilder(entry.Value);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                if (string.Equals(candidate.DataSource, local.DataSource, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(candidate.InitialCatalog, local.InitialCatalog, StringComparison.OrdinalIgnoreCase))
                {
                    return Configuration.GetConnectionString($"{entry.Key}_Remote");
                }
            }

            return null;
        }

        /// <summary>
        /// Dynamic, database-driven counterpart to the appsettings.json-based lookup above - tries
        /// every active CompanyDatabase row's own local connection (built the same way
        /// BuildTenantConnectionString does) against the given local connection, and if one
        /// matches, builds its remote connection from that same row's Remote* columns. Checked
        /// first so any company provisioned through Add Company gets real sync without anyone
        /// hand-editing appsettings.json; the static lookup remains as a fallback only.
        /// </summary>
        private static string? TryResolveRemoteFromCompanyDatabase(SqlConnectionStringBuilder local)
        {
            try
            {
                using var masterDb = new SNAP.infrastructure.Data.MasterErpDbContext(
                    new DbContextOptionsBuilder<SNAP.infrastructure.Data.MasterErpDbContext>()
                        .UseSqlServer(MasterErp)
                        .Options);

                foreach (var companyDb in masterDb.CompanyDatabases.Where(x => x.IsActive).ToList())
                {
                    SqlConnectionStringBuilder candidate;
                    try
                    {
                        candidate = new SqlConnectionStringBuilder(BuildTenantConnectionString(companyDb.ServerName, companyDb.DatabaseName));
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }

                    if (string.Equals(candidate.DataSource, local.DataSource, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(candidate.InitialCatalog, local.InitialCatalog, StringComparison.OrdinalIgnoreCase))
                    {
                        return BuildRemoteTenantConnectionString(
                            companyDb.RemoteServerName, companyDb.RemoteDatabaseName, companyDb.RemoteUsername, companyDb.RemotePassword);
                    }
                }

                return null;
            }
            catch
            {
                // MasterErp unreachable, or this is a design-time/tooling context with no database
                // at all - fall through to the static appsettings.json lookup rather than throwing.
                return null;
            }
        }

        /// <summary>
        /// DI-friendly implementation of <see cref="IConnectionStringProvider"/> for the default tenant.
        /// </summary>
        public sealed class DefaultProvider : IConnectionStringProvider
        {
            public string MasterErpConnectionString => MasterErp;
            public string GetTenantConnectionString(string serverName, string databaseName, string? credentialKey = null) => BuildTenantConnectionString(serverName, databaseName, credentialKey);
        }
    }
}
