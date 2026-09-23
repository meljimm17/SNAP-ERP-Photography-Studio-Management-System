using Microsoft.EntityFrameworkCore;
using SNAP.infrastructure.Data;

namespace SNAP.infrastructure.services
{
    /// <summary>
    /// Helps initialize and migrate databases
    /// </summary>
    public static class DatabaseInitializer
    {
        /// <summary>
        /// Initializes the Master ERP database by applying pending migrations
        /// </summary>
        public static async Task InitializeMasterDatabaseAsync(string connectionString)
        {
            try
            {
                var options = new DbContextOptionsBuilder<MasterErpDbContext>()
                    .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3))
                    .Options;

                using var dbContext = new MasterErpDbContext(options);

                Console.WriteLine("Applying Master ERP migrations...");
                await dbContext.Database.MigrateAsync();
                Console.WriteLine("✓ Master ERP database initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error initializing Master database: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Initializes the Tenant ERP database by applying pending migrations
        /// </summary>
        public static async Task InitializeTenantDatabaseAsync(string connectionString)
        {
            try
            {
                var options = new DbContextOptionsBuilder<TenantErpDbContext>()
                    .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3))
                    .Options;

                using var dbContext = new TenantErpDbContext(options);

                Console.WriteLine("Applying Tenant ERP migrations...");
                await dbContext.Database.MigrateAsync();
                Console.WriteLine("✓ Tenant ERP database initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error initializing Tenant database: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Ensures the Master database exists and is up to date
        /// </summary>
        public static async Task EnsureMasterDatabaseAsync(string connectionString)
        {
            try
            {
                var options = new DbContextOptionsBuilder<MasterErpDbContext>()
                    .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3))
                    .Options;

                using var dbContext = new MasterErpDbContext(options);

                // Creates the database if missing and applies any pending migrations. Must not be
                // combined with EnsureCreatedAsync, which bypasses the migrations history table and
                // makes every future migration fail with "object already exists".
                await dbContext.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ensuring Master database: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Ensures the Tenant database exists and is up to date
        /// </summary>
        public static async Task EnsureTenantDatabaseAsync(string connectionString)
        {
            try
            {
                var options = new DbContextOptionsBuilder<TenantErpDbContext>()
                    .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3))
                    .Options;

                using var dbContext = new TenantErpDbContext(options);

                // Creates the database if missing and applies any pending migrations. Must not be
                // combined with EnsureCreatedAsync, which bypasses the migrations history table and
                // makes every future migration fail with "object already exists".
                await dbContext.Database.MigrateAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ensuring Tenant database: {ex.Message}");
                throw;
            }
        }
    }
}
