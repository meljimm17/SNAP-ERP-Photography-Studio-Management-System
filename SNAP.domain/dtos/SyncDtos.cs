namespace SNAP.domain.dtos
{
    /// <summary>DTO for the "Sync Now" screen - the shape of TenantSyncService.SyncResult.
    /// RemoteReachable/LastError only mean something on a Sync Now result, not on a plain status
    /// check (which never touches the remote).</summary>
    public sealed class SyncStatusDto
    {
        public bool RemoteConfigured { get; set; }
        public int TotalPending { get; set; }
        public int Synced { get; set; }
        public int StillPending { get; set; }
        public bool RemoteReachable { get; set; }
        public string? LastError { get; set; }
    }

    /// <summary>One row still waiting to reach MonsterASP - EntityType/Description name what it
    /// actually is (e.g. "Inventory" / "Canon Battery") rather than leaving the Data Sync screen
    /// to show a bare row count with nothing to point to.</summary>
    public sealed class SyncPendingRowDto
    {
        public string EntityType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
