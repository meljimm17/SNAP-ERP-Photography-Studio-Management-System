using Microsoft.Extensions.Configuration;
using SNAP.domain.config;

namespace SNAP.infrastructure.config
{
    /// <summary>
    /// Single source of truth for resolving database connection strings. All three (MasterErp,
    /// the default/local tenant, and each keyed remote tenant) are read from appsettings.json's
    /// ConnectionStrings section, using the same SQL Server Authentication style for every one of
    /// them - see README.md for the local setup.
    /// </summary>
    public static class ConnectionStrings
    {
        private static readonly IConfiguration Configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        public static string MasterErp => GetRequired("MasterErp");

        public static string TenantErpDefault => GetRequired("TenantErp");

        public static string BuildTenantConnectionString(string serverName, string databaseName, string? credentialKey = null)
        {
            // No credential key -> legacy behavior: every company without its own keyed entry
            // shares the default "TenantErp" connection string (the local dev tenant).
            if (string.IsNullOrWhiteSpace(credentialKey))
            {
                return TenantErpDefault;
            }

            var suffix = credentialKey.Trim().ToUpperInvariant().Replace('-', '_');
            return GetRequired($"TenantErp_{suffix}");
        }

        private static string GetRequired(string connectionStringKey)
        {
            return Configuration.GetConnectionString(connectionStringKey)
                ?? throw new InvalidOperationException(
                    $"Connection string 'ConnectionStrings:{connectionStringKey}' is not configured in appsettings.json.");
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
