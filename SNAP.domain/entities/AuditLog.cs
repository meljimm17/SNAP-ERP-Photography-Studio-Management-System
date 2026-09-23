using SNAP.domain.enums;

namespace SNAP.domain.entities
{
    public class AuditLog
    {
        public int AuditLogId { get; set; }

        public int? TenantUserId { get; set; }

        public TenantUser? TenantUser { get; set; }

        /// <summary>
        /// Snapshot of the actor's display name at the time of the action, so the log still reads
        /// correctly even if the user is later renamed or removed.
        /// </summary>
        public string ActorName { get; set; } = string.Empty;

        public string EntityName { get; set; } = string.Empty;

        public int EntityId { get; set; }

        public AuditAction Action { get; set; }

        public string? Summary { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
