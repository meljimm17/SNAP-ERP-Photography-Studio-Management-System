namespace SNAP.domain.config
{
    /// <summary>
    /// Provides database connection strings for master and tenant databases.
    /// </summary>
    public interface IConnectionStringProvider
    {
        string MasterErpConnectionString { get; }
        string GetTenantConnectionString(string serverName, string databaseName, string? credentialKey = null);
    }
}
