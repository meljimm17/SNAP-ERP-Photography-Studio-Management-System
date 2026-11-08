using SNAP.infrastructure.config;

namespace SNAP
{
    public static class AppDbConfig
    {
        public static string ConnectionString => MasterErpConnectionString;

        /// <summary>
        /// Master ERP Database Connection String
        /// Database: DB_MasterERP (db67905)
        /// </summary>
        public static string MasterErpConnectionString => ConnectionStrings.MasterErp;

        /// <summary>
        /// Gets the tenant-specific database connection string for the given server/database.
        /// </summary>
        public static string GetTenantConnectionString(string serverName, string databaseName, string? credentialKey = null) =>
            ConnectionStrings.BuildTenantConnectionString(serverName, databaseName, credentialKey);

        /// <summary>
        /// Default tenant connection string for development/testing
        /// </summary>
        public static string TenantErpConnectionString => ConnectionStrings.TenantErpDefault;
    }
}
