using Microsoft.EntityFrameworkCore;
using SNAP.infrastructure.Data;
using System;

namespace SNAP.infrastructure.services
{
    /// <summary>
    /// Provides database context instances for master and tenant databases.
    /// Handles dynamic connection string resolution based on company context.
    /// </summary>
    public class DatabaseContextProvider
    {
        private readonly string _masterConnectionString;
        private MasterErpDbContext? _masterContext;
        private TenantErpDbContext? _tenantContext;

        public DatabaseContextProvider(string masterConnectionString)
        {
            if (string.IsNullOrWhiteSpace(masterConnectionString))
                throw new ArgumentNullException(nameof(masterConnectionString));

            _masterConnectionString = masterConnectionString;
        }

        /// <summary>
        /// Gets or creates the Master ERP database context.
        /// </summary>
        public MasterErpDbContext GetMasterContext()
        {
            if (_masterContext == null)
            {
                var options = new DbContextOptionsBuilder<MasterErpDbContext>()
                    .UseSqlServer(_masterConnectionString)
                    .Options;

                _masterContext = new MasterErpDbContext(options);
            }

            return _masterContext;
        }

        /// <summary>
        /// Gets or creates the Tenant ERP database context with the specified connection string.
        /// </summary>
        public TenantErpDbContext GetTenantContext(string connectionString)
        {
            // Always create a new instance for tenant context to support multi-tenant scenarios
            var options = new DbContextOptionsBuilder<TenantErpDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new TenantErpDbContext(options);
        }

        /// <summary>
        /// Gets or creates the default Tenant ERP database context.
        /// </summary>
        public TenantErpDbContext GetDefaultTenantContext(string defaultTenantConnectionString)
        {
            if (_tenantContext == null)
            {
                var options = new DbContextOptionsBuilder<TenantErpDbContext>()
                    .UseSqlServer(defaultTenantConnectionString)
                    .Options;

                _tenantContext = new TenantErpDbContext(options);
            }

            return _tenantContext;
        }

        /// <summary>
        /// Disposes all managed contexts.
        /// </summary>
        public void Dispose()
        {
            _masterContext?.Dispose();
            _tenantContext?.Dispose();
        }
    }
}
