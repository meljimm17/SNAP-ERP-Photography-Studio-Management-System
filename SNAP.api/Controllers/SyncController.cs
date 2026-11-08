using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNAP.api.Services;
using SNAP.domain.dtos;
using SNAP.domain.enums;
using SNAP.infrastructure.config;
using SNAP.infrastructure.services;

namespace SNAP.api.Controllers
{
    /// <summary>
    /// "Sync Now" - lets a tenant check and force-flush its local-first-write outbox to its own
    /// MonsterASP remote (see TenantSyncService's remarks) on demand, rather than only ever finding
    /// out whether it worked by making an unrelated save and hoping. Every write already triggers
    /// a background best-effort push by itself; this is purely a way to see the result instead of
    /// waiting on one incidentally.
    /// </summary>
    [ApiController]
    [Authorize]
    // Same gate as the Data Sync tab itself (SystemAdministrationForm shows it only at Full User
    // Management, i.e. Admin) - without it any signed-in role could list or force a push.
    [RequireAccessLevel(SystemModule.UserManagement, AccessLevel.Full)]
    [Route("api/sync")]
    public class SyncController : ControllerBase
    {
        private readonly TenantDbContextFactory _tenantDbContextFactory;

        public SyncController(TenantDbContextFactory tenantDbContextFactory)
        {
            _tenantDbContextFactory = tenantDbContextFactory;
        }

        /// <summary>How many rows are waiting, without attempting to push them.</summary>
        [HttpGet("status")]
        public ActionResult<SyncStatusDto> GetStatus()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var localConnectionString = db.Database.GetConnectionString();
            var pending = db.SyncOutboxEntries.Count(x => x.SyncedAt == null);
            var remoteConfigured = localConnectionString is not null
                && ConnectionStrings.TryResolveRemoteForLocal(localConnectionString) is not null;

            return Ok(new SyncStatusDto
            {
                RemoteConfigured = remoteConfigured,
                TotalPending = pending,
                Synced = 0,
                StillPending = pending
            });
        }

        /// <summary>Every row still waiting to reach this tenant's remote, described in plain
        /// terms (e.g. "Inventory: Canon Battery") rather than leaving the caller to guess what a
        /// pending count actually refers to.</summary>
        [HttpGet("pending")]
        public ActionResult<List<SyncPendingRowDto>> GetPendingDetails()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var rows = TenantSyncService.GetPendingDetails(db)
                .Select(r => new SyncPendingRowDto
                {
                    EntityType = r.EntityName,
                    Description = r.Description,
                    CreatedAt = r.CreatedAt
                })
                .ToList();
            return Ok(rows);
        }

        /// <summary>Actually attempts the push, right now, and reports what happened - the one
        /// place in the app that waits on TenantSyncService's result instead of firing it into a
        /// background Task.</summary>
        [HttpPost("now")]
        public ActionResult<SyncStatusDto> SyncNow()
        {
            var db = _tenantDbContextFactory.GetTenantDbContext();
            var localConnectionString = db.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(localConnectionString))
            {
                return Ok(new SyncStatusDto { RemoteConfigured = false, TotalPending = 0, Synced = 0, StillPending = 0 });
            }

            var remoteConnectionString = ConnectionStrings.TryResolveRemoteForLocal(localConnectionString);
            var result = TenantSyncService.PushPendingNow(localConnectionString, remoteConnectionString);

            return Ok(new SyncStatusDto
            {
                RemoteConfigured = result.RemoteConfigured,
                TotalPending = result.TotalPending,
                Synced = result.Synced,
                StillPending = result.StillPending,
                RemoteReachable = result.RemoteReachable,
                LastError = result.LastError
            });
        }
    }
}
