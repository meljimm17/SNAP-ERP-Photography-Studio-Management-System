using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNAP.domain.dtos;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP.api.Controllers
{
    /// <summary>
    /// The Master/Super Admin panel's API. Unlike every other controller here it works directly
    /// against MasterErpDbContext and never resolves a tenant database - the platform operator
    /// isn't scoped to any one company (their token carries companyId 0).
    ///
    /// This replaces the unauthenticated app.MapPost("/companies", ...) minimal-API endpoint that
    /// used to sit in Program.cs: that one accepted a raw Company from any caller at all and wrote
    /// no CompanyDatabase row, leaving behind a company that could never be logged into.
    /// </summary>
    [ApiController]
    [Authorize(Roles = nameof(SystemRole.SuperAdmin))]
    [Route("api/companies")]
    public class CompaniesController : ControllerBase
    {
        private readonly MasterErpDbContext _masterDb;

        public CompaniesController(MasterErpDbContext masterDb)
        {
            _masterDb = masterDb;
        }

        [HttpGet]
        public ActionResult<List<CompanyRow>> GetCompanies()
        {
            var rows = _masterDb.Companies
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.CompanyId)
                .Select(x => new CompanyRow
                {
                    CompanyId = x.CompanyId,
                    CompanyCode = x.CompanyCode,
                    CompanyName = x.CompanyName,
                    PlanTier = x.PlanTier.ToString(),
                    SubscriptionStatus = x.SubscriptionStatus.ToString(),
                    SubscriptionStartDate = x.SubscriptionStartDate,
                    SubscriptionRenewalDate = x.SubscriptionRenewalDate,
                    IsActive = x.IsActive,
                    CreatedAt = x.CreatedAt,
                    RequestedPlanTier = x.RequestedPlanTier == null ? null : x.RequestedPlanTier.ToString(),
                    UpgradeRequestedAt = x.UpgradeRequestedAt
                })
                .ToList();
            return Ok(rows);
        }

        /// <summary>
        /// Creates the Company and its CompanyDatabase together. Both or neither: a Company with
        /// no database row is unusable, and a database row pointing at a company that failed to
        /// save is orphaned, so they share one transaction.
        /// </summary>
        [HttpPost]
        public ActionResult<CompanyRow> CreateCompany([FromBody] CreateCompanyRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CompanyCode) || string.IsNullOrWhiteSpace(request.CompanyName))
                return BadRequest(new { message = "Company code and name are required." });

            if (string.IsNullOrWhiteSpace(request.ServerName) || string.IsNullOrWhiteSpace(request.DatabaseName))
                return BadRequest(new { message = "Server name and database name are required." });

            if (string.IsNullOrWhiteSpace(request.AdminUsername) || string.IsNullOrWhiteSpace(request.AdminPassword))
                return BadRequest(new { message = "Admin username and password are required so the company has a way to log in." });

            var code = request.CompanyCode.Trim();
            if (_masterDb.Companies.Any(x => x.CompanyCode == code))
                return BadRequest(new { message = "That company code is already taken." });

            // CreateExecutionStrategy().Execute(...) rather than a bare BeginTransaction - EF Core
            // forbids combining a retrying execution strategy (enabled on this DbContext in
            // Program.cs) with a user-managed transaction, so a bare transaction throws the moment
            // this endpoint is actually hit. This wraps the whole create-both-rows sequence as one
            // retriable unit instead.
            var company = new Company();
            var companyDatabase = new CompanyDatabase();
            var strategy = _masterDb.Database.CreateExecutionStrategy();
            strategy.Execute(() =>
            {
                using var transaction = _masterDb.Database.BeginTransaction();

                company.CompanyCode = code;
                company.CompanyName = request.CompanyName.Trim();
                company.PlanTier = request.PlanTier;
                company.SubscriptionStatus = SubscriptionStatus.Active;
                company.SubscriptionStartDate = DateTime.UtcNow;
                company.SubscriptionRenewalDate = request.SubscriptionRenewalDate;
                company.IsActive = true;
                company.CreatedAt = DateTime.UtcNow;
                _masterDb.Companies.Add(company);
                _masterDb.SaveChanges();

                companyDatabase.CompanyId = company.CompanyId;
                companyDatabase.ServerName = request.ServerName.Trim();
                companyDatabase.DatabaseName = request.DatabaseName.Trim();
                companyDatabase.RemoteServerName = string.IsNullOrWhiteSpace(request.RemoteServerName) ? null : request.RemoteServerName.Trim();
                companyDatabase.RemoteDatabaseName = string.IsNullOrWhiteSpace(request.RemoteDatabaseName) ? null : request.RemoteDatabaseName.Trim();
                companyDatabase.RemoteUsername = string.IsNullOrWhiteSpace(request.RemoteUsername) ? null : request.RemoteUsername.Trim();
                companyDatabase.RemotePassword = string.IsNullOrWhiteSpace(request.RemotePassword) ? null : request.RemotePassword;
                companyDatabase.IsActive = true;
                _masterDb.CompanyDatabases.Add(companyDatabase);
                _masterDb.SaveChanges();

                transaction.Commit();
            });

            var adminResult = AuthenticationService.ProvisionAdminUser(companyDatabase, request.AdminUsername.Trim(), request.AdminPassword);
            // Separate from admin provisioning - a freshly created remote database otherwise sits
            // with zero tables forever, since nothing else ever migrates it (see the method's own
            // remarks). A no-op success when no Remote fields were given at all.
            var remoteResult = AuthenticationService.ProvisionRemoteDatabase(companyDatabase);

            return Ok(new CompanyRow
            {
                CompanyId = company.CompanyId,
                CompanyCode = company.CompanyCode,
                CompanyName = company.CompanyName,
                PlanTier = company.PlanTier.ToString(),
                SubscriptionStatus = company.SubscriptionStatus.ToString(),
                SubscriptionStartDate = company.SubscriptionStartDate,
                SubscriptionRenewalDate = company.SubscriptionRenewalDate,
                IsActive = company.IsActive,
                CreatedAt = company.CreatedAt,
                AdminProvisioningError = adminResult.Success ? null : adminResult.Error,
                RemoteProvisioningError = remoteResult.Success ? null : remoteResult.Error
            });
        }

        /// <summary>
        /// Changes plan and billing state. Suspending takes effect on the company's very next
        /// login attempt (AuthenticationService.TryAuthenticateCompany checks SubscriptionStatus
        /// before it even opens their tenant database) - it does not kick out a session already in
        /// progress, since the JWT that session is holding was already issued and stays valid until
        /// it expires. IsActive is a separate, harsher action: it hides the company's existence
        /// entirely rather than naming the reason.
        /// </summary>
        [HttpPut("{id:int}/subscription")]
        public IActionResult UpdateSubscription(int id, [FromBody] UpdateCompanySubscriptionRequest request)
        {
            var company = _masterDb.Companies.Find(id);
            if (company is null)
                return NotFound(new { message = "Company not found." });

            company.PlanTier = request.PlanTier;
            company.SubscriptionStatus = request.SubscriptionStatus;
            company.SubscriptionRenewalDate = request.SubscriptionRenewalDate;
            // Whatever pending upgrade request existed has now been reviewed and actioned - clear
            // it whether the tier ended up matching what was requested or not.
            company.RequestedPlanTier = null;
            company.UpgradeRequestedAt = null;
            _masterDb.SaveChanges();

            return Ok(new { message = "Subscription updated." });
        }

        /// <summary>Soft-delete toggle - whether the company can log in at all.</summary>
        [HttpPut("{id:int}/active")]
        public IActionResult SetActive(int id, [FromBody] bool isActive)
        {
            var company = _masterDb.Companies.Find(id);
            if (company is null)
                return NotFound(new { message = "Company not found." });

            company.IsActive = isActive;
            _masterDb.SaveChanges();

            return Ok(new { message = isActive ? "Company reactivated." : "Company deactivated." });
        }

        /// <summary>
        /// Cross-tenant analytics for the Super Admin's charts. Counts Companies rows only - no
        /// tenant database is opened, so this stays fast and cannot leak any one company's data.
        /// </summary>
        [HttpGet("platform-stats")]
        public ActionResult<PlatformStatsDto> GetPlatformStats()
        {
            var companies = _masterDb.Companies.AsNoTracking().ToList();

            return Ok(new PlatformStatsDto
            {
                CompaniesByPlanTier = Enum.GetValues<PlanTier>()
                    .Select(tier => new CountByLabel
                    {
                        Label = tier.ToString(),
                        Count = companies.Count(c => c.PlanTier == tier)
                    })
                    .ToList(),

                CompaniesBySubscriptionStatus = Enum.GetValues<SubscriptionStatus>()
                    .Select(status => new CountByLabel
                    {
                        Label = status.ToString(),
                        Count = companies.Count(c => c.SubscriptionStatus == status)
                    })
                    .ToList(),

                NewCompaniesByMonth = companies
                    .GroupBy(c => new DateTime(c.CreatedAt.Year, c.CreatedAt.Month, 1))
                    .OrderBy(g => g.Key)
                    .Select(g => new CountByLabel
                    {
                        Label = g.Key.ToString("MMM yyyy"),
                        Count = g.Count()
                    })
                    .ToList()
            });
        }
    }
}
