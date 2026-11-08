using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNAP.api.Services;
using SNAP.domain.dtos;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;

namespace SNAP.api.Controllers
{
    /// <summary>
    /// Lets a tenant monitor its own subscription (plan tier, status, next renewal) - read-only,
    /// matching what a customer should be able to see about their own billing. Changing any of it
    /// stays SuperAdmin-only, through CompaniesController's Master Panel; nothing here writes.
    ///
    /// Works directly against MasterErpDbContext like CompaniesController, since subscription data
    /// lives there rather than in any tenant database - but unlike CompaniesController this isn't
    /// SuperAdmin-only. It's scoped to "whichever company issued my own token" via the companyId
    /// claim, which the client never gets to choose (see JwtTokenService), so any authenticated
    /// tenant user can only ever see their own company's billing state, never another's.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequireAccessLevel(SystemModule.SubscriptionManagement, AccessLevel.View)]
    [Route("api/subscription")]
    public class SubscriptionController : ControllerBase
    {
        private readonly MasterErpDbContext _masterDb;

        public SubscriptionController(MasterErpDbContext masterDb)
        {
            _masterDb = masterDb;
        }

        [HttpGet("me")]
        public ActionResult<CompanyRow> GetMySubscription()
        {
            var companyIdClaim = User.FindFirst("companyId")?.Value;
            if (!int.TryParse(companyIdClaim, out var companyId) || companyId <= 0)
            {
                // CompanyId 0 is the SuperAdmin/system-admin login, which isn't scoped to any one
                // company - there's nothing for this endpoint to return.
                return BadRequest(new { message = "This account isn't tied to a company subscription." });
            }

            var company = _masterDb.Companies
                .AsNoTracking()
                .FirstOrDefault(x => x.CompanyId == companyId);

            if (company is null)
            {
                return NotFound(new { message = "Company not found." });
            }

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
                RequestedPlanTier = company.RequestedPlanTier?.ToString(),
                UpgradeRequestedAt = company.UpgradeRequestedAt
            });
        }

        /// <summary>
        /// Records a request only - nothing about this company's actual access changes here. A
        /// SuperAdmin reviews it and applies (or overrides) it through the Master Panel's existing
        /// Change Plan / Renewal flow, which clears these two fields once actioned either way.
        /// Admin-only: this is a company-level business decision, not something every role that can
        /// merely view the subscription (e.g. FinanceManager) should be able to trigger on its own.
        /// </summary>
        [Authorize(Roles = nameof(SystemRole.Admin))]
        [HttpPost("request-upgrade")]
        public IActionResult RequestUpgrade([FromBody] RequestTierUpgradeRequest request)
        {
            var companyIdClaim = User.FindFirst("companyId")?.Value;
            if (!int.TryParse(companyIdClaim, out var companyId) || companyId <= 0)
            {
                return BadRequest(new { message = "This account isn't tied to a company subscription." });
            }

            var company = _masterDb.Companies.Find(companyId);
            if (company is null)
            {
                return NotFound(new { message = "Company not found." });
            }

            if (request.RequestedTier <= company.PlanTier)
            {
                return BadRequest(new { message = $"You're already on {company.PlanTier} or higher - pick a tier above your current plan." });
            }

            company.RequestedPlanTier = request.RequestedTier;
            company.UpgradeRequestedAt = DateTime.UtcNow;
            _masterDb.SaveChanges();

            return Ok(new { message = $"Upgrade to {request.RequestedTier} requested. A SuperAdmin will review it." });
        }
    }
}
