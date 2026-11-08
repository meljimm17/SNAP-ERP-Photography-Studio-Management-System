using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNAP.api.Services;
using SNAP.domain.dtos;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.services;

namespace SNAP.api.Controllers
{
    /// <summary>
    /// Equipment management API - handles camera gear and studio asset tracking,
    /// maintenance status, and depreciation tracking.
    ///
    /// Branch scoping mirrors InventoryController exactly: a branch-bound user
    /// (BranchManager, Staff, Photographer) can only see and mutate their own branch's
    /// equipment; a company-wide role (Admin, FinanceManager) can filter by passing
    /// branchId or omit it to get all branches in one response.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequirePlanTier(PlanTier.Small)]
    [RequireAccessLevel(SystemModule.EquipmentManagement, AccessLevel.AssignedView)]
    [Route("api/equipment")]
    public class EquipmentController : ControllerBase
    {
        private readonly TenantDbContextFactory _tenantDbContextFactory;
        private readonly BranchScope _branchScope;

        public EquipmentController(TenantDbContextFactory tenantDbContextFactory, BranchScope branchScope)
        {
            _tenantDbContextFactory = tenantDbContextFactory;
            _branchScope = branchScope;
        }

        /// <summary>
        /// branchId is a hint for company-wide roles only. A branch-bound caller's own
        /// branch always wins, so passing someone else's id here changes nothing.
        /// </summary>
        [HttpGet]
        public ActionResult<List<EquipmentRow>> GetEquipment(int? branchId = null)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var scopedBranchId = _branchScope.ResolveForRead(branchId);

            var rows = db.Equipment
                .Where(x => x.IsActive)
                .Where(x => scopedBranchId == null || x.BranchId == scopedBranchId)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.EquipmentId)
                .Select(x => new EquipmentRow
                {
                    EquipmentId = x.EquipmentId,
                    EquipmentName = x.EquipmentName,
                    Category = x.Category.ToString(),
                    Status = x.Status.ToString(),
                    AcquisitionCost = x.AcquisitionCost,
                    BranchId = x.BranchId,
                    BranchName = x.Branch != null ? x.Branch.BranchName : string.Empty
                })
                .ToList();
            return Ok(rows);
        }

        /// <summary>Get a specific equipment item by ID</summary>
        [HttpGet("{id:int}")]
        public ActionResult<EquipmentDetailDto> GetEquipment(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var equipment = db.Equipment.Find(id);
            if (equipment is null || !equipment.IsActive)
                return NotFound(new { message = "Equipment not found." });

            // A branch-bound caller may only touch their own branch's equipment.
            _branchScope.EnsureCanAccess(equipment.BranchId);

            return Ok(new EquipmentDetailDto
            {
                EquipmentId = equipment.EquipmentId,
                EquipmentName = equipment.EquipmentName,
                Category = equipment.Category,
                Status = equipment.Status,
                AcquisitionCost = equipment.AcquisitionCost,
                IsActive = equipment.IsActive
            });
        }

        /// <summary>Create new equipment</summary>
        [RequireAccessLevel(SystemModule.EquipmentManagement, AccessLevel.Manage)]
        [HttpPost]
        public ActionResult<int> CreateEquipment(CreateEquipmentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.EquipmentName))
                return BadRequest(new { message = "Equipment name is required." });

            var db = _tenantDbContextFactory.GetTenantDbContext();
            var equipment = new Equipment
            {
                EquipmentName = request.EquipmentName.Trim(),
                Category = request.Category,
                Status = EquipmentStatus.Available,
                AcquisitionCost = request.AcquisitionCost,
                BranchId = _branchScope.ResolveForWrite(db, request.BranchId),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.Equipment.Add(equipment);
            TenantSyncService.SaveAndSync(db);
            return Ok(equipment.EquipmentId);
        }

        /// <summary>Update existing equipment</summary>
        [RequireAccessLevel(SystemModule.EquipmentManagement, AccessLevel.Manage)]
        [HttpPut("{id:int}")]
        public IActionResult UpdateEquipment(int id, UpdateEquipmentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.EquipmentName))
                return BadRequest(new { message = "Equipment name is required." });

            var db = _tenantDbContextFactory.GetTenantDbContext();
            var equipment = db.Equipment.Find(id);
            if (equipment is null || !equipment.IsActive)
                return NotFound(new { message = "Equipment not found." });

            _branchScope.EnsureCanAccess(equipment.BranchId);

            equipment.EquipmentName = request.EquipmentName.Trim();
            equipment.Category = request.Category;
            equipment.Status = request.Status;
            equipment.AcquisitionCost = request.AcquisitionCost;

            TenantSyncService.SaveAndSync(db);
            return Ok();
        }

        /// <summary>Delete equipment (soft delete)</summary>
        [RequireAccessLevel(SystemModule.EquipmentManagement, AccessLevel.Manage)]
        [HttpDelete("{id:int}")]
        public IActionResult DeleteEquipment(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var equipment = db.Equipment.Find(id);
            if (equipment is null)
                return NotFound(new { message = "Equipment not found." });

            _branchScope.EnsureCanAccess(equipment.BranchId);

            equipment.IsActive = false;

            TenantSyncService.SaveAndSync(db);
            return Ok();
        }
    }
}
