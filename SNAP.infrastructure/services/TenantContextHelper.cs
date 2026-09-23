using Microsoft.EntityFrameworkCore;
using SNAP.infrastructure.Data;
using System;

namespace SNAP.infrastructure.services
{
    /// <summary>
    /// Provides easy access to the current tenant's DbContext instance.
    /// Uses connection string provided to get the database context.
    /// </summary>
    public static class TenantContextHelper
    {
        /// <summary>
        /// Gets a tenant DbContext for the specified connection string.
        /// </summary>
        public static TenantErpDbContext GetTenantContext(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "No tenant connection string provided. Connection string is required.");
            }

            var options = new DbContextOptionsBuilder<TenantErpDbContext>()
                .UseSqlServer(connectionString)
                .Options;

            return new TenantErpDbContext(options);
        }

        /// <summary>
        /// Gets the Master ERP DbContext.
        /// </summary>
        public static MasterErpDbContext GetMasterContext(string masterConnectionString)
        {
            if (string.IsNullOrWhiteSpace(masterConnectionString))
            {
                throw new InvalidOperationException(
                    "No master connection string provided. Connection string is required.");
            }

            var options = new DbContextOptionsBuilder<MasterErpDbContext>()
                .UseSqlServer(masterConnectionString)
                .Options;

            return new MasterErpDbContext(options);
        }
    }
}
