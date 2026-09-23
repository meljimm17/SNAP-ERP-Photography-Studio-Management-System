using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SNAP.infrastructure.config;
using SNAP.infrastructure.Data;

namespace SNAP.api.Services
{
    /// <summary>
    /// Resolves the correct tenant database for the current authenticated request. The client never
    /// supplies which database to use - the JWT's companyId claim (set once, server-side, at login)
    /// is looked up against Companies/CompanyDatabases here, the same lookup
    /// AuthenticationService.TryAuthenticateCompany already does at login. Registered scoped, so
    /// every controller method in a single request shares one resolved TenantErpDbContext.
    /// </summary>
    public class TenantDbContextFactory
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly MasterErpDbContext _masterDb;
        private TenantErpDbContext? _tenantDb;

        public TenantDbContextFactory(IHttpContextAccessor httpContextAccessor, MasterErpDbContext masterDb)
        {
            _httpContextAccessor = httpContextAccessor;
            _masterDb = masterDb;
        }

        public TenantErpDbContext GetTenantDbContext()
        {
            if (_tenantDb is not null)
            {
                return _tenantDb;
            }

            var companyIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("companyId")?.Value;
            if (!int.TryParse(companyIdClaim, out var companyId) || companyId <= 0)
            {
                throw new UnauthorizedAccessException("This request isn't scoped to a company.");
            }

            var companyDb = _masterDb.CompanyDatabases
                .AsNoTracking()
                .FirstOrDefault(x => x.CompanyId == companyId && x.IsActive)
                ?? throw new InvalidOperationException("This company's database is not configured.");

            var connectionString = ConnectionStrings.BuildTenantConnectionString(companyDb.ServerName, companyDb.DatabaseName, companyDb.CredentialKey);
            var options = new DbContextOptionsBuilder<TenantErpDbContext>().UseSqlServer(connectionString).Options;

            _tenantDb = new TenantErpDbContext(options);

            // Self-healing schema: migrate this specific tenant's database right before first use,
            // the same way AuthenticationService.TryAuthenticateCompany already does at login -
            // no company's database silently falls behind because nobody ran `dotnet ef database
            // update` against it by hand.
            _tenantDb.Database.Migrate();

            return _tenantDb;
        }

        public string GetActorName() =>
            _httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Name)?.Value ?? "Unknown";

        public int? GetActorUserId()
        {
            var sub = _httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(sub, out var id) && id > 0 ? id : null;
        }
    }
}
