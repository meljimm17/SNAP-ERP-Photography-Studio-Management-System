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
    /// User management API - handles tenant user accounts, roles, access control,
    /// and authentication credentials.
    /// </summary>
    [ApiController]
    [Authorize]
    // Only Admin/SuperAdmin ever get anything above None for UserManagement in PermissionService -
    // no role has View/AssignedView here, so Manage is the correct floor for every action,
    // including just listing users. Without this a Staff or Photographer could read or edit every
    // other user's role/status via a direct API call, despite having zero access in the actual
    // permission model.
    [RequireAccessLevel(SystemModule.UserManagement, AccessLevel.Manage)]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly TenantDbContextFactory _tenantDbContextFactory;

        public UserController(TenantDbContextFactory tenantDbContextFactory)
        {
            _tenantDbContextFactory = tenantDbContextFactory;
        }

        /// <summary>Get all active users for the current tenant</summary>
        [HttpGet]
        public ActionResult<List<UserRow>> GetUsers()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var rows = db.TenantUsers
                .Where(x => x.Status != UserStatus.Inactive)
                .OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.UserId)
                .Select(x => new UserRow
                {
                    UserId = x.UserId,
                    FullName = x.FullName,
                    Username = x.Username,
                    Role = x.Role.ToString(),
                    Status = x.Status.ToString()
                })
                .ToList();
            return Ok(rows);
        }

        /// <summary>Get a specific user by ID</summary>
        [HttpGet("{id:int}")]
        public ActionResult<UserDetailDto> GetUser(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var user = db.TenantUsers.Find(id);
            if (user is null || user.Status == UserStatus.Inactive)
                return NotFound(new { message = "User not found." });

            return Ok(new UserDetailDto
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Username = user.Username,
                Role = user.Role,
                Status = user.Status,
                CreatedAt = user.CreatedAt
            });
        }

        /// <summary>Create a new tenant user</summary>
        [HttpPost]
        public ActionResult<int> CreateUser(CreateUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
                return BadRequest(new { message = "Full name is required." });

            if (string.IsNullOrWhiteSpace(request.Username))
                return BadRequest(new { message = "Username is required." });

            if (string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { message = "Password is required." });

            var db = _tenantDbContextFactory.GetTenantDbContext();

            // Check for duplicate username
            if (db.TenantUsers.Any(x => x.Username == request.Username.Trim()))
                return BadRequest(new { message = "Username already exists." });

            var hashedPassword = HashPassword(request.Password);
            var user = new TenantUser
            {
                FullName = request.FullName.Trim(),
                Username = request.Username.Trim(),
                PasswordHash = hashedPassword,
                Role = request.Role,
                Status = UserStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            db.TenantUsers.Add(user);
            Save(db, entry => entry.Entity == user ? $"Created user: {user.FullName} ({user.Role})" : null);
            return Ok(user.UserId);
        }

        /// <summary>Update a user's details (not password)</summary>
        [HttpPut("{id:int}")]
        public IActionResult UpdateUser(int id, UpdateUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FullName))
                return BadRequest(new { message = "Full name is required." });

            if (string.IsNullOrWhiteSpace(request.Username))
                return BadRequest(new { message = "Username is required." });

            var db = _tenantDbContextFactory.GetTenantDbContext();
            var user = db.TenantUsers.Find(id);
            if (user is null || user.Status == UserStatus.Inactive)
                return NotFound(new { message = "User not found." });

            // Check for duplicate username (if changed)
            if (user.Username != request.Username.Trim() &&
                db.TenantUsers.Any(x => x.Username == request.Username.Trim()))
                return BadRequest(new { message = "Username already exists." });

            var previousRole = user.Role;
            var previousStatus = user.Status;

            user.FullName = request.FullName.Trim();
            user.Username = request.Username.Trim();
            user.Role = request.Role;
            user.Status = request.Status;

            Save(db, entry =>
            {
                if (entry.Entity != user) return null;
                var changes = new List<string>();
                if (previousRole != user.Role) changes.Add($"role {previousRole} → {user.Role}");
                if (previousStatus != user.Status) changes.Add($"status {previousStatus} → {user.Status}");
                var detail = changes.Count > 0 ? $" ({string.Join(", ", changes)})" : string.Empty;
                return $"Updated user: {user.FullName}{detail}";
            });
            return Ok();
        }

        /// <summary>Change a user's password (admin sets new password)</summary>
        [HttpPost("{id:int}/set-password")]
        public IActionResult SetPassword(int id, SetPasswordRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { message = "New password is required." });

            var db = _tenantDbContextFactory.GetTenantDbContext();
            var user = db.TenantUsers.Find(id);
            if (user is null)
                return NotFound(new { message = "User not found." });

            user.PasswordHash = HashPassword(request.NewPassword);
            // Deliberately doesn't echo the new password itself into the audit trail - "who reset
            // whose password and when" is what matters for accountability, not the password value.
            Save(db, entry => entry.Entity == user ? $"Reset password for user: {user.FullName}" : null);
            return Ok();
        }

        /// <summary>Deactivate a user (soft delete)</summary>
        [HttpDelete("{id:int}")]
        public IActionResult DeleteUser(int id)
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var user = db.TenantUsers.Find(id);
            if (user is null)
                return NotFound(new { message = "User not found." });

            user.Status = UserStatus.Inactive;
            Save(db, entry => entry.Entity == user ? $"Deactivated user: {user.FullName}" : null);
            return Ok();
        }

        /// <summary>
        /// Drop-in replacement for TenantSyncService.SaveAndSync, matching how every other
        /// converted API controller saves (see InventoryController's equivalent helper). Queues a
        /// correctly-attributed audit entry via AuditLogWriter - using this request's own JWT
        /// claims (_tenantDbContextFactory.GetActorUserId/GetActorName), not
        /// TenantSyncService.CurrentActorUserId/CurrentActorName, which are WinForms-only globals
        /// the API server process never sets - then queues the change for background sync the same
        /// way SaveAndSync does. Calling SaveAndSync directly here used to leave every User module
        /// audit log entry attributed to "Unknown" when the change came through the API.
        /// </summary>
        private void Save(SNAP.infrastructure.Data.TenantErpDbContext db, Func<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry, string?>? summarySelector = null)
        {
            var changedEntries = TenantSyncService.CaptureSyncableChanges(db);

            AuditLogWriter.QueueAuditEntries(db, _tenantDbContextFactory.GetActorUserId(), _tenantDbContextFactory.GetActorName(), summarySelector);
            db.SaveChanges();

            if (changedEntries.Count == 0)
            {
                return;
            }

            TenantSyncService.QueueOutboxEntries(db, changedEntries);
            db.SaveChanges();
            TenantSyncService.TriggerBackgroundPush(db);
        }

        // Must match AuthenticationService.HashPassword exactly - that's what login verifies
        // against (SHA-256, hex-encoded). This used to call BCrypt.Net.BCrypt.HashPassword
        // instead, a completely different hash format: any user created or password-reset through
        // this endpoint got a hash VerifyPassword could never match, locking them out with their
        // own correct password. The WinForms User Management screen was never affected - it writes
        // directly to the database using AuthenticationService.HashPassword already - but this
        // endpoint itself was 100% broken for anyone who actually called it.
        private static string HashPassword(string password) => AuthenticationService.HashPassword(password);
    }
}
