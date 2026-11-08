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

            try
            {
                var options = new DbContextOptionsBuilder<MasterErpDbContext>()
                    .UseSqlServer(ConnectionStrings.MasterErp, sql => sql.EnableRetryOnFailure(3))
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
                return (false, "Unable to connect to the system admin database.", SystemRole.SuperAdmin, string.Empty);
            }
        }

        /// <summary>
        /// Authenticates a tenant user against one specific company, resolved directly by
        /// CompanyCode rather than searching every company's tenant database for a username match.
        /// Also gates login by the company's PlanTier: Micro only allows the Admin role to sign in
        /// (Staff/Photographer accounts stay in TenantUsers untouched - this is a login-time check,
        /// not a data change, so nothing needs to be migrated if the company later upgrades tiers).
        /// </summary>
        public static (bool Success, string Message, SystemRole Role, string DisplayName, int CompanyId, string? TenantConnectionString, PlanTier PlanTier, int UserId, int? BranchId) TryAuthenticateCompany(string companyCode, string username, string password)
        {
            if (string.IsNullOrWhiteSpace(companyCode) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return (false, "Company code, username, and password are required.", SystemRole.Admin, string.Empty, 0, null, PlanTier.Micro, 0, null);
            }

            var trimmedCompanyCode = companyCode.Trim();

            // Scoped to the "demo" company code only (not "tenant_a") so this offline shortcut can
            // never mask the real Micro-tier role gate below when testing against tenant_a itself.
            if (string.Equals(trimmedCompanyCode, "demo", StringComparison.OrdinalIgnoreCase) &&
                TryDemoTenantLogin(username, password, out var demoUser))
            {
                return (true, "Welcome.", demoUser.Role, demoUser.FullName, 0, null, PlanTier.Micro, demoUser.UserId, null);
            }

            try
            {
                // EnableRetryOnFailure: a remote free-tier SQL host drops connections often enough
                // that a single transient failure here shouldn't surface as "invalid credentials"
                // or a scary raw SqlException to someone just trying to log in - EF retries the
                // connection itself before this bubbles up to the catch block below.
                var masterOptions = new DbContextOptionsBuilder<MasterErpDbContext>()
                    .UseSqlServer(ConnectionStrings.MasterErp, sql => sql.EnableRetryOnFailure(3))
                    .Options;

                using var masterDb = new MasterErpDbContext(masterOptions);
                var company = masterDb.Companies
                    .AsNoTracking()
                    .FirstOrDefault(x => x.CompanyCode == trimmedCompanyCode && x.IsActive);

                if (company is null)
                {
                    return (false, "Company code not found.", SystemRole.Admin, string.Empty, 0, null, PlanTier.Micro, 0, null);
                }

                // IsActive and SubscriptionStatus are deliberately separate flags (see Company's
                // remarks): IsActive is the platform-operator soft-delete (already filtered out of
                // the query above, so it fails as an anonymous "not found" - existence isn't
                // confirmed either way). Suspended is a billing state the company itself caused
                // and needs to act on, so unlike IsActive it gets a specific, named message rather
                // than a generic one - there's nothing to hide from a legitimate customer about
                // why their own account stopped working.
                if (company.SubscriptionStatus == SubscriptionStatus.Suspended)
                {
                    return (false, "This company's subscription is suspended. Please contact support to reactivate your account.", SystemRole.Admin, string.Empty, company.CompanyId, null, company.PlanTier, 0, null);
                }

                var companyDb = masterDb.CompanyDatabases
                    .AsNoTracking()
                    .FirstOrDefault(x => x.CompanyId == company.CompanyId && x.IsActive);

                if (companyDb is null)
                {
                    return (false, "Company code not found.", SystemRole.Admin, string.Empty, 0, null, PlanTier.Micro, 0, null);
                }

                var tenantConnectionString = ConnectionStrings.BuildTenantConnectionString(companyDb.ServerName, companyDb.DatabaseName, companyDb.CredentialKey);
                var tenantOptions = new DbContextOptionsBuilder<TenantErpDbContext>()
                    .UseSqlServer(tenantConnectionString, sql => sql.EnableRetryOnFailure(3))
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
                    return (false, "Invalid username or password.", SystemRole.Admin, string.Empty, company.CompanyId, null, company.PlanTier, 0, null);
                }

                if (company.PlanTier == PlanTier.Micro && user.Role != SystemRole.Admin)
                {
                    return (false, "This account type isn't available on your current plan.", SystemRole.Admin, string.Empty, company.CompanyId, null, company.PlanTier, 0, null);
                }

                // Only a branch-bound role gets pinned to a branch; a company-wide role sees every
                // branch even if an old BranchId is still sitting on their row (see BranchScoping).
                var branchId = BranchScoping.RoleIsBranchBound(user.Role) ? user.BranchId : null;

                return (true, "Welcome.", user.Role, user.FullName, company.CompanyId, tenantConnectionString, company.PlanTier, user.UserId, branchId);
            }
            catch (Exception ex)
            {
                return (false, $"Unable to connect to the database: {ex.Message}", SystemRole.Admin, string.Empty, 0, null, PlanTier.Micro, 0, null);
            }
        }

        /// <summary>
        /// Resolves which physical tenant database a company's own modules (the ones not yet
        /// converted to call SNAP.api - Equipment, Finance, Package, Reports, User Management,
        /// Payment, Booking's New/Edit dialog) should connect to, now that login itself goes through
        /// SNAP.api's /api/auth/company-login instead of TryAuthenticateCompany. Deliberately does
        /// no credential check of its own - the API already authenticated the user; this only
        /// answers "which database is this company's." Without it, AppSession.CurrentTenantConnectionString
        /// stays null after every login and every legacy module silently falls back to the single
        /// default TenantErp connection string, so a real company (e.g. tenant_b) would see whichever
        /// company that default happens to point at instead of its own data. Returns null (rather than
        /// throwing) on any failure - callers already have a "no connection string" fallback path.
        /// </summary>
        public static string? ResolveTenantConnectionString(string companyCode)
        {
            if (string.Equals(companyCode?.Trim(), "demo", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            try
            {
                var masterOptions = new DbContextOptionsBuilder<MasterErpDbContext>()
                    .UseSqlServer(ConnectionStrings.MasterErp, sql => sql.EnableRetryOnFailure(3))
                    .Options;

                using var masterDb = new MasterErpDbContext(masterOptions);
                var company = masterDb.Companies
                    .AsNoTracking()
                    .FirstOrDefault(x => x.CompanyCode == companyCode!.Trim() && x.IsActive);
                if (company is null) return null;

                var companyDb = masterDb.CompanyDatabases
                    .AsNoTracking()
                    .FirstOrDefault(x => x.CompanyId == company.CompanyId && x.IsActive);
                if (companyDb is null) return null;

                return ConnectionStrings.BuildTenantConnectionString(companyDb.ServerName, companyDb.DatabaseName, companyDb.CredentialKey);
            }
            catch
            {
                return null;
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

        /// <summary>
        /// Migrates a freshly provisioned company's tenant database (creating its schema, which
        /// also seeds the one Main Branch every Micro/Small/Medium company starts with - see the
        /// AddBranchAndBranchScoping migration) and inserts its first TenantUser, an Admin with no
        /// BranchId (company-wide). Without this a new company has a Company/CompanyDatabase row
        /// but literally no way to log in - its tenant database starts with zero users. Shared by
        /// both company-creation paths (the SuperAdmin WinForms screen and CompaniesController) so
        /// neither one can silently drift from the other.
        /// </summary>
        public static (bool Success, string? Error) ProvisionAdminUser(CompanyDatabase companyDatabase, string username, string password)
        {
            try
            {
                var tenantConnectionString = ConnectionStrings.BuildTenantConnectionString(
                    companyDatabase.ServerName, companyDatabase.DatabaseName, companyDatabase.CredentialKey);
                var tenantOptions = new DbContextOptionsBuilder<TenantErpDbContext>()
                    .UseSqlServer(tenantConnectionString, sql => sql.EnableRetryOnFailure(3))
                    .Options;

                using var tenantDb = new TenantErpDbContext(tenantOptions);
                tenantDb.Database.Migrate();

                tenantDb.TenantUsers.Add(new TenantUser
                {
                    FullName = "Admin",
                    Username = username,
                    PasswordHash = HashPassword(password),
                    Role = SystemRole.Admin,
                    Status = UserStatus.Active,
                    BranchId = null,
                    CreatedAt = DateTime.UtcNow
                });

                // SaveAndSync, not a bare SaveChanges - this user needs a SyncOutboxEntry queued
                // the same as any other write, or it never reaches the remote database at all.
                // Without this, every future row that references them by foreign key (a
                // PurchaseOrder's CreatedByUserId, a Booking's AssignedPhotographerUserId, ...)
                // would permanently fail to sync with a foreign-key-constraint error, since the
                // row it points at on the remote side would never exist to satisfy it.
                TenantSyncService.SaveAndSync(tenantDb);

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Migrates a company's remote (MonsterASP) database so its schema actually matches the
        /// local one. Without this, a freshly-created remote database sits there with zero tables
        /// forever - PushEntity's inserts/updates against a table that doesn't exist throw, get
        /// swallowed by PushPendingNow's per-row try/catch, and the row just stays "pending" with
        /// no visible error anywhere. ProvisionAdminUser only ever migrated the local side; this is
        /// the remote counterpart, called separately since Remote fields can be added at company
        /// creation or later via Edit Company, and either path needs this to actually run.
        /// A no-op success (not an error) when no remote is configured at all (any of the four
        /// Remote* fields empty) - remote hosting is opt-in per company. Safe to call every time
        /// Remote fields are saved, not just once - Migrate() is a no-op once already up to date.
        /// </summary>
        public static (bool Success, string? Error) ProvisionRemoteDatabase(CompanyDatabase companyDatabase)
        {
            var remoteConnectionString = ConnectionStrings.BuildRemoteTenantConnectionString(
                companyDatabase.RemoteServerName, companyDatabase.RemoteDatabaseName,
                companyDatabase.RemoteUsername, companyDatabase.RemotePassword);

            if (remoteConnectionString is null)
            {
                return (true, null);
            }

            try
            {
                var remoteOptions = new DbContextOptionsBuilder<TenantErpDbContext>()
                    .UseSqlServer(remoteConnectionString, sql => sql.EnableRetryOnFailure(3))
                    .Options;

                using var remoteDb = new TenantErpDbContext(remoteOptions);
                remoteDb.Database.Migrate();

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}