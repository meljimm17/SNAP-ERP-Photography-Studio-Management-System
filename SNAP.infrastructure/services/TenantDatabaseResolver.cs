using Microsoft.EntityFrameworkCore;
using SNAP.infrastructure.Data;
using System;
using System.Threading.Tasks;

namespace SNAP.infrastructure.services
{
    /// <summary>
    /// Handles resolution of tenant database connections based on company code or company database information.
    /// </summary>
    public class TenantDatabaseResolver
    {
        private readonly DatabaseContextProvider _contextProvider;
        private readonly Func<string, string, string?, string> _connectionStringBuilder;

        public TenantDatabaseResolver(
            DatabaseContextProvider contextProvider,
            Func<string, string, string?, string> connectionStringBuilder)
        {
            _contextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
            _connectionStringBuilder = connectionStringBuilder ?? throw new ArgumentNullException(nameof(connectionStringBuilder));
        }

        /// <summary>
        /// Resolves the tenant database connection string for a given company code.
        /// </summary>
        public async Task<TenantErpDbContext> ResolveTenantContextByCompanyCodeAsync(string companyCode)
        {
            var masterContext = _contextProvider.GetMasterContext();

            // Find the company and its database configuration
            var company = await masterContext.Companies
                .FirstOrDefaultAsync(c => c.CompanyCode == companyCode && c.IsActive);

            if (company == null)
            {
                throw new InvalidOperationException($"Company with code '{companyCode}' not found or is inactive.");
            }

            // Get the company's database configuration
            var companyDb = await masterContext.CompanyDatabases
                .FirstOrDefaultAsync(cd => cd.CompanyId == company.CompanyId && cd.IsActive);

            if (companyDb == null)
            {
                throw new InvalidOperationException($"No active database configuration found for company '{companyCode}'.");
            }

            // Build the connection string and return the tenant context
            var connectionString = _connectionStringBuilder(companyDb.ServerName, companyDb.DatabaseName, companyDb.CredentialKey);
            return _contextProvider.GetTenantContext(connectionString);
        }

        /// <summary>
        /// Resolves the tenant database connection string for a given company ID.
        /// </summary>
        public async Task<TenantErpDbContext> ResolveTenantContextByCompanyIdAsync(int companyId)
        {
            var masterContext = _contextProvider.GetMasterContext();

            // Get the company's database configuration
            var companyDb = await masterContext.CompanyDatabases
                .FirstOrDefaultAsync(cd => cd.CompanyId == companyId && cd.IsActive);

            if (companyDb == null)
            {
                throw new InvalidOperationException($"No active database configuration found for company ID {companyId}.");
            }

            // Build the connection string and return the tenant context
            var connectionString = _connectionStringBuilder(companyDb.ServerName, companyDb.DatabaseName, companyDb.CredentialKey);
            return _contextProvider.GetTenantContext(connectionString);
        }
    }
}
