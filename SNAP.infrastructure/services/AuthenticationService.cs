using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.config;
using SNAP.infrastructure.Data;

namespace SNAP.infrastructure.services
{
    public static class AuthenticationService
    {
        public static (bool Success, string Message, SystemRole Role, string DisplayName) TryAuthenticateSystemAdmin(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return (false, "Username and password are required.", SystemRole.SuperAdmin, string.Empty);
            }

            if (TryDemoSystemAdminLogin(username, password, out var demoUser))
            {
                return (true, "Welcome.", demoUser.Role, demoUser.Username);
            }

            try
            {
                var options = new DbContextOptionsBuilder<MasterErpDbContext>()
                    .UseSqlServer(ConnectionStrings.MasterErp)
                    .Options;

                using var db = new MasterErpDbContext(options);
                var user = db.PlatformUsers
                    .AsNoTracking()
                    .FirstOrDefault(x => x.Username == username && x.IsActive);

                if (user is null)
                {
                    return (false, "System admin not found.", SystemRole.SuperAdmin, string.Empty);
                }

                if (!VerifyPassword(password, user.PasswordHash))
                {
                    return (false, "Invalid system admin credentials.", SystemRole.SuperAdmin, string.Empty);
                }

                return (true, "Welcome.", user.Role, user.Username);
            }
            catch
            {
                return (false, "Unable to connect to the system admin database. Use the demo credentials or configure the connection string.", SystemRole.SuperAdmin, string.Empty);
            }
        }

        /// <summary>
        /// Authenticates a tenant user against one specific company, resolved directly by
        /// CompanyCode rather than searching every company's tenant database for a username match.
        /// Also gates login by the company's PlanTier: Micro only allows the Admin role to sign in
        /// (Staff/Photographer accounts stay in TenantUsers untouched - this is a login-time check,
        /// not a data change, so nothing needs to be migrated if the company later upgrades tiers).
        /// </summary>
        public static (bool Success, string Message, SystemRole Role, string DisplayName, int CompanyId, string? TenantConnectionString, PlanTier PlanTier, int UserId) TryAuthenticateCompany(string companyCode, string username, string password)
        {
            if (string.IsNullOrWhiteSpace(companyCode) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return (false, "Company code, username, and password are required.", SystemRole.Admin, string.Empty, 0, null, PlanTier.Micro, 0);
            }

            var trimmedCompanyCode = companyCode.Trim();

            // Scoped to the "demo" company code only (not "tenant_a") so this offline shortcut can
            // never mask the real Micro-tier role gate below when testing against tenant_a itself.
            if (string.Equals(trimmedCompanyCode, "demo", StringComparison.OrdinalIgnoreCase) &&
                TryDemoTenantLogin(username, password, out var demoUser))
            {
                return (true, "Welcome.", demoUser.Role, demoUser.FullName, 0, null, PlanTier.Micro, demoUser.UserId);
            }

            try
            {
                var masterOptions = new DbContextOptionsBuilder<MasterErpDbContext>()
                    .UseSqlServer(ConnectionStrings.MasterErp)
                    .Options;

                using var masterDb = new MasterErpDbContext(masterOptions);
                var company = masterDb.Companies
                    .AsNoTracking()
                    .FirstOrDefault(x => x.CompanyCode == trimmedCompanyCode && x.IsActive);

                if (company is null)
                {
                    return (false, "Company code not found.", SystemRole.Admin, string.Empty, 0, null, PlanTier.Micro, 0);
                }

                var companyDb = masterDb.CompanyDatabases
                    .AsNoTracking()
                    .FirstOrDefault(x => x.CompanyId == company.CompanyId && x.IsActive);

                if (companyDb is null)
                {
                    return (false, "Company code not found.", SystemRole.Admin, string.Empty, 0, null, PlanTier.Micro, 0);
                }

                var tenantConnectionString = ConnectionStrings.BuildTenantConnectionString(companyDb.ServerName, companyDb.DatabaseName, companyDb.CredentialKey);
                var tenantOptions = new DbContextOptionsBuilder<TenantErpDbContext>()
                    .UseSqlServer(tenantConnectionString)
                    .Options;

                using var tenantDb = new TenantErpDbContext(tenantOptions);

                // Program.cs only auto-migrates the single default tenant database at startup, not
                // every company's own database (each CompanyDatabases row can point at a completely
                // different server/database). Migrating here, right before the first real query
                // against this specific tenant, makes every company's database self-healing on
                // login instead of silently relying on someone having remembered to run
                // `dotnet ef database update` against it by hand. Migrate() is a no-op (one quick
                // history-table check) once a database is already up to date.
                tenantDb.Database.Migrate();

                var user = tenantDb.TenantUsers
                    .AsNoTracking()
                    .FirstOrDefault(x => x.Username == username && x.Status == UserStatus.Active);

                if (user is null || !VerifyPassword(password, user.PasswordHash))
                {
                    return (false, "Invalid username or password.", SystemRole.Admin, string.Empty, company.CompanyId, null, company.PlanTier, 0);
                }

                if (company.PlanTier == PlanTier.Micro && user.Role != SystemRole.Admin)
                {
                    return (false, "This account type isn't available on your current plan.", SystemRole.Admin, string.Empty, company.CompanyId, null, company.PlanTier, 0);
                }

                return (true, "Welcome.", user.Role, user.FullName, company.CompanyId, tenantConnectionString, company.PlanTier, user.UserId);
            }
            catch (Exception ex)
            {
                return (false, $"Unable to connect to the database: {ex.Message}", SystemRole.Admin, string.Empty, 0, null, PlanTier.Micro, 0);
            }
        }

        public static string HashPassword(string password)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
            return Convert.ToHexString(bytes);
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            return string.Equals(HashPassword(password), storedHash, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryDemoSystemAdminLogin(string username, string password, out PlatformUser user)
        {
            user = new PlatformUser
            {
                PlatformUserId = 1,
                Username = "superadmin",
                PasswordHash = HashPassword("superadmin123"),
                Role = SystemRole.SuperAdmin,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            if (string.Equals(username, user.Username, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(password, "superadmin123", StringComparison.Ordinal)
                && VerifyPassword(password, user.PasswordHash))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Fixed offline demo accounts that never touch the database - only reachable via company
        /// code "demo" (see TryAuthenticateCompany), so they can never interfere with a real
        /// company's tier gating.
        /// </summary>
        private static bool TryDemoTenantLogin(string username, string password, out TenantUser user)
        {
            user = new TenantUser
            {
                UserId = 1,
                FullName = "Demo Admin",
                Username = "admin",
                PasswordHash = HashPassword("admin123"),
                Role = SystemRole.Admin,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            if (string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase) &&
                VerifyPassword(password, user.PasswordHash))
            {
                return true;
            }

            if (string.Equals(username, "staff", StringComparison.OrdinalIgnoreCase) &&
                VerifyPassword(password, HashPassword("staff123")))
            {
                user = new TenantUser
                {
                    UserId = 2,
                    FullName = "Demo Staff",
                    Username = "staff",
                    PasswordHash = HashPassword("staff123"),
                    Role = SystemRole.Staff,
                    Status = UserStatus.Active,
                    CreatedAt = DateTime.UtcNow
                };
                return true;
            }

            if (string.Equals(username, "photographer", StringComparison.OrdinalIgnoreCase) &&
                VerifyPassword(password, HashPassword("photographer123")))
            {
                user = new TenantUser
                {
                    UserId = 3,
                    FullName = "Demo Photographer",
                    Username = "photographer",
                    PasswordHash = HashPassword("photographer123"),
                    Role = SystemRole.Photographer,
                    Status = UserStatus.Active,
                    CreatedAt = DateTime.UtcNow
                };
                return true;
            }

            return false;
        }
    }
}