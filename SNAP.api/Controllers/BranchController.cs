using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNAP.api.Services;
using SNAP.domain.dtos;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP.api.Controllers
{
    /// <summary>
    /// Branch management - the one genuinely new module at Medium tier. Gated to Medium so a
    /// Micro or Small company can't reach it even by calling the endpoint directly; below that
    /// tier a company conceptually has exactly one branch and never sees this screen.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequirePlanTier(PlanTier.Medium)]
    [Route("api/branches")]
    public class BranchController : ControllerBase
    {
        private readonly TenantDbContextFactory _tenantDbContextFactory;

        public BranchController(TenantDbContextFactory tenantDbContextFactory)
        {
            _tenantDbContextFactory = tenantDbContextFactory;
        }

        /// <summary>Every branch, newest first, including retired ones so they can be reactivated.</summary>
        [HttpGet]
        public ActionResult<List<BranchRow>> GetBranches()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var rows = db.Branches
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.BranchId)
                .Select(x => new BranchRow
                {
                    BranchId = x.BranchId,
                    BranchName = x.BranchName,
                    Address = x.Address ?? string.Empty,
                    ContactNumber = x.ContactNumber ?? string.Empty,
                    IsMainBranch = x.IsMainBranch,
                    IsActive = x.IsActive,
                    StaffCount = db.TenantUsers.Count(u => u.BranchId == x.BranchId)
                })
                .ToList();
            return Ok(rows);
        }

        /// <summary>Active branches only - the source for every branch picker in the app.</summary>
        [HttpGet("active")]
        public ActionResult<List<BranchRow>> GetActiveBranches()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var rows = db.Branches
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.IsMainBranch)
                .ThenBy(x => x.BranchName)
                .Select(x => new BranchRow
                {
                    BranchId = x.BranchId,
                    BranchName = x.BranchName,
                    Address = x.Address ?? string.Empty,
                    ContactNumber = x.ContactNumber ?? string.Empty,
                    IsMainBranch = x.IsMainBranch,
                    IsActive = x.IsActive
                })
                .ToList();
            return Ok(rows);
        }

        [HttpGet("{id:int}")]
        public ActionResult<BranchDetailDto> GetBranch(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var branch = db.Branches.Find(id);
            if (branch is null)
                return NotFound(new { message = "Branch not found." });

            return Ok(new BranchDetailDto
            {
                BranchId = branch.BranchId,
                BranchName = branch.BranchName,
                Address = branch.Address,
                ContactNumber = branch.ContactNumber,
                IsMainBranch = branch.IsMainBranch,
                IsActive = branch.IsActive
            });
        }

        // Add/Edit are Admin-only. A BranchManager's PermissionService access level is View (a
        // read-only report of every branch), so WinForms already disables these buttons for them -
        // but a disabled button is a UI convenience, not enforcement (see BranchScope's remarks on
        // the same point). Without this attribute a BranchManager, Staff or Photographer could
        // still create or rename branches by calling the endpoint directly.
        [Authorize(Roles = $"{nameof(SystemRole.SuperAdmin)},{nameof(SystemRole.Admin)}")]
        [HttpPost]
        public ActionResult<BranchDetailDto> CreateBranch([FromBody] CreateBranchRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.BranchName))
                return BadRequest(new { message = "Branch name is required." });

            var db = _tenantDbContextFactory.GetTenantDbContext();

            var branch = new Branch
            {
                BranchName = request.BranchName.Trim(),
                Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
                ContactNumber = string.IsNullOrWhiteSpace(request.ContactNumber) ? null : request.ContactNumber.Trim(),
                IsMainBranch = request.IsMainBranch,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            // The very first branch is the main one whatever the request said - a company can
            // never be left without one.
            if (!db.Branches.Any())
                branch.IsMainBranch = true;

            db.Branches.Add(branch);
            TenantSyncService.SaveAndSync(db);

            if (branch.IsMainBranch)
                DemoteOtherMainBranches(db, branch.BranchId);

            return Ok(new BranchDetailDto
            {
                BranchId = branch.BranchId,
                BranchName = branch.BranchName,
                Address = branch.Address,
                ContactNumber = branch.ContactNumber,
                IsMainBranch = branch.IsMainBranch,
                IsActive = branch.IsActive
            });
        }

        [Authorize(Roles = $"{nameof(SystemRole.SuperAdmin)},{nameof(SystemRole.Admin)}")]
        [HttpPut("{id:int}")]
        public IActionResult UpdateBranch(int id, [FromBody] UpdateBranchRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.BranchName))
                return BadRequest(new { message = "Branch name is required." });

            var db = _tenantDbContextFactory.GetTenantDbContext();
            var branch = db.Branches.Find(id);
            if (branch is null)
                return NotFound(new { message = "Branch not found." });

            // Retiring or demoting the only main branch would leave the company with none, and
            // every branch-scoped screen resolves against it.
            if (branch.IsMainBranch && (!request.IsMainBranch || !request.IsActive))
                return BadRequest(new { message = "Promote another branch to main before retiring or demoting this one." });

            if (!request.IsActive && db.TenantUsers.Any(u => u.BranchId == id))
                return BadRequest(new { message = "Reassign this branch's staff before retiring it." });

            branch.BranchName = request.BranchName.Trim();
            branch.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
            branch.ContactNumber = string.IsNullOrWhiteSpace(request.ContactNumber) ? null : request.ContactNumber.Trim();
            branch.IsMainBranch = request.IsMainBranch;
            branch.IsActive = request.IsActive;

            TenantSyncService.SaveAndSync(db);

            if (branch.IsMainBranch)
                DemoteOtherMainBranches(db, branch.BranchId);

            return Ok(new { message = "Branch updated." });
        }

        /// <summary>
        /// Keeps the "exactly one main branch" rule true by clearing the flag everywhere else.
        /// Run after the promotion is saved, so a failure can't leave the company with none.
        /// </summary>
        private static void DemoteOtherMainBranches(TenantErpDbContext db, int keepBranchId)
        {
            var others = db.Branches.Where(x => x.IsMainBranch && x.BranchId != keepBranchId).ToList();
            if (others.Count == 0) return;

            foreach (var other in others)
                other.IsMainBranch = false;

            TenantSyncService.SaveAndSync(db);
        }
    }
}
