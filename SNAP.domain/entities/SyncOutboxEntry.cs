namespace SNAP.domain.entities
{
    public class SyncOutboxEntry
    {
        public int Id { get; set; }

        public string EntityName { get; set; } = string.Empty;

        public int EntityId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SyncedAt { get; set; }
    }
}
