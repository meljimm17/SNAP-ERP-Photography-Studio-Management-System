using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNAP.api.Services;
using SNAP.domain.dtos;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.services;

namespace SNAP.api.Controllers
{
    /// <summary>
    /// Photography package management API - handles preset photography service packages
    /// with fixed prices and durations.
    /// </summary>
    [ApiController]
    [Authorize]
    [RequireAccessLevel(SystemModule.PackageManagement, AccessLevel.AssignedView)]
    [Route("api/packages")]
    public class PackageController : ControllerBase
    {
        private readonly TenantDbContextFactory _tenantDbContextFactory;

        public PackageController(TenantDbContextFactory tenantDbContextFactory)
        {
            _tenantDbContextFactory = tenantDbContextFactory;
        }

        /// <summary>Get all active packages for the current tenant</summary>
        [HttpGet]
        public ActionResult<List<PackageRow>> GetPackages()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var rows = db.PhotographyPackages
                .Where(x => x.IsActive)
                .OrderBy(x => x.PackageName)
                .Select(x => new PackageRow
                {
                    PackageId = x.PackageId,
                    PackageName = x.PackageName,
                    Price = x.Price,
                    DurationHours = x.DurationHours,
                    BookingCount = db.Bookings.Count(b => b.PackageId == x.PackageId && b.Status != b.Status)
                })
                .ToList();
            return Ok(rows);
        }

        /// <summary>Get a specific package by ID</summary>
        [HttpGet("{id:int}")]
        public ActionResult<PackageDetailDto> GetPackage(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var package = db.PhotographyPackages.Find(id);
            if (package is null || !package.IsActive)
                return NotFound(new { message = "Package not found." });

            return Ok(new PackageDetailDto
            {
                PackageId = package.PackageId,
                PackageName = package.PackageName,
                Price = package.Price,
                DurationHours = package.DurationHours,
                IsActive = package.IsActive
            });
        }

        /// <summary>Create a new package</summary>
        [RequireAccessLevel(SystemModule.PackageManagement, AccessLevel.Manage)]
        [HttpPost]
        public ActionResult<int> CreatePackage(CreatePackageRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PackageName))
                return BadRequest(new { message = "Package name is required." });

            if (request.Price <= 0)
                return BadRequest(new { message = "Price must be greater than zero." });

            if (request.DurationHours <= 0)
                return BadRequest(new { message = "Duration must be greater than zero." });

            var db = _tenantDbContextFactory.GetTenantDbContext();
            var package = new PhotographyPackage
            {
                PackageName = request.PackageName.Trim(),
                Price = request.Price,
                DurationHours = request.DurationHours,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            db.PhotographyPackages.Add(package);
            TenantSyncService.SaveAndSync(db);
            return Ok(package.PackageId);
        }

        /// <summary>Update an existing package</summary>
        [RequireAccessLevel(SystemModule.PackageManagement, AccessLevel.Manage)]
        [HttpPut("{id:int}")]
        public IActionResult UpdatePackage(int id, UpdatePackageRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PackageName))
                return BadRequest(new { message = "Package name is required." });

            if (request.Price <= 0)
                return BadRequest(new { message = "Price must be greater than zero." });

            if (request.DurationHours <= 0)
                return BadRequest(new { message = "Duration must be greater than zero." });

            var db = _tenantDbContextFactory.GetTenantDbContext();
            var package = db.PhotographyPackages.Find(id);
            if (package is null || !package.IsActive)
                return NotFound(new { message = "Package not found." });

            package.PackageName = request.PackageName.Trim();
            package.Price = request.Price;
            package.DurationHours = request.DurationHours;

            TenantSyncService.SaveAndSync(db);
            return Ok();
        }

        /// <summary>Delete a package (soft delete)</summary>
        [RequireAccessLevel(SystemModule.PackageManagement, AccessLevel.Manage)]
        [HttpDelete("{id:int}")]
        public IActionResult DeletePackage(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var package = db.PhotographyPackages.Find(id);
            if (package is null)
                return NotFound(new { message = "Package not found." });

            var bookingCount = db.Bookings
                .Count(b => b.PackageId == id);

            if (bookingCount > 0)
                return BadRequest(new { message = $"Cannot delete package with {bookingCount} existing booking(s). Consider archiving instead." });

            package.IsActive = false;
            TenantSyncService.SaveAndSync(db);
            return Ok();
        }
    }
}
