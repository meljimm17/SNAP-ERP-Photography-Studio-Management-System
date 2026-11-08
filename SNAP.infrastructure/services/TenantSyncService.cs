using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.config;
using SNAP.infrastructure.Data;

namespace SNAP.infrastructure.services
{
    /// <summary>
    /// Local-first write path: every save goes to the local tenant database first (unchanged
    /// behavior), then this records which rows changed and best-effort-pushes them to that same
    /// tenant's own MonsterASP remote in the background. If the remote is unreachable, the row
    /// just stays pending in SyncOutboxEntries and gets retried on the next save, by the WinForms
    /// app's AutoSyncService (when Windows reports the network is back, and every 5 seconds
    /// while anything is pending), or by "Sync Now" - nothing is lost, and no local save is ever
    /// blocked or slowed down waiting on the remote.
    ///
    /// Which remote a save pushes to is resolved per tenant, not globally: SaveAndSync matches the
    /// TenantErpDbContext's own local connection string against appsettings.json's
    /// TenantErp_TENANT_A/B/C entries and pushes to that same key's "..._Remote" counterpart
    /// (ConnectionStrings.TryResolveRemoteForLocal) - so Tenant B's saves reach Tenant B's remote,
    /// never Tenant C's. A tenant with no "..._Remote" entry configured simply doesn't sync -
    /// nothing else to turn on or off per tenant. SNAP_TENANTERP_REMOTE_CONNECTIONSTRING is kept
    /// only as a last-resort fallback for the (now unlikely) case where no per-tenant entry
    /// matches at all.
    /// </summary>
    public static class TenantSyncService
    {
        private const string RemoteConnectionStringEnvVar = "SNAP_TENANTERP_REMOTE_CONNECTIONSTRING";

        /// <summary>
        /// Upper bound on how long one sync pass waits to reach the remote before giving up. A
        /// healthy MonsterASP free-tier login is often 1-5s (TLS + a cold database), so a tighter
        /// limit reported a perfectly good connection as "unreachable". An attempt that still
        /// times out just stays pending - AutoSyncService retries every few seconds, and its
        /// per-tenant gate means slow attempts never pile up.
        /// </summary>
        private const int RemoteConnectTimeoutSeconds = 10;

        /// <summary>
        /// One gate per local tenant database, so only one push for a given tenant runs at a time
        /// in this process - the save-triggered background push, the auto-sync timer/network
        /// event, and "Sync Now" would otherwise race each other over the same outbox rows.
        /// Per tenant rather than global so the API (which serves every tenant) never makes one
        /// tenant's slow remote hold up another's.
        /// </summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> PushGates =
            new(StringComparer.OrdinalIgnoreCase);

        private static SemaphoreSlim GateFor(string localConnectionString) =>
            PushGates.GetOrAdd(localConnectionString, _ => new SemaphoreSlim(1, 1));

        /// <summary>
        /// The signed-in actor for audit logging, set once at login (and cleared at logout) by the
        /// UI project. SNAP.infrastructure cannot reference SNAP's AppSession directly (SNAP
        /// references SNAP.infrastructure, not the other way around), so this is the
        /// infrastructure-side mirror of AppSession.CurrentUserId/CurrentDisplayName.
        /// </summary>
        public static int? CurrentActorUserId { get; set; }
        public static string CurrentActorName { get; set; } = "Unknown";

        private static readonly Dictionary<string, Type> SyncableEntityTypes = new()
        {
            [nameof(Customer)] = typeof(Customer),
            [nameof(PhotographyPackage)] = typeof(PhotographyPackage),
            [nameof(Booking)] = typeof(Booking),
            [nameof(Payment)] = typeof(Payment),
            [nameof(TenantUser)] = typeof(TenantUser),
            [nameof(Supplier)] = typeof(Supplier),
            [nameof(Inventory)] = typeof(Inventory),
            [nameof(Equipment)] = typeof(Equipment),
            [nameof(Expense)] = typeof(Expense),
            [nameof(SalaryPayment)] = typeof(SalaryPayment),
            [nameof(ProductSale)] = typeof(ProductSale),
            [nameof(PurchaseOrder)] = typeof(PurchaseOrder),
            [nameof(PurchaseOrderLine)] = typeof(PurchaseOrderLine),
            [nameof(OwnerTransaction)] = typeof(OwnerTransaction),
            [nameof(Branch)] = typeof(Branch),
        };

        /// <summary>
        /// Entity types that get a row written to AuditLog on Create/Update/Delete. Kept as its own
        /// set (distinct from SyncableEntityTypes) since AuditLog itself must never be audited or
        /// synced, and not every syncable entity necessarily needs to stay audited forever.
        /// </summary>
        private static readonly HashSet<string> AuditableEntityTypes = new()
        {
            nameof(Equipment),
            nameof(Inventory),
            nameof(Supplier),
            nameof(Payment),
            nameof(Expense),
            nameof(SalaryPayment),
            nameof(TenantUser),
            nameof(Booking),
            nameof(Customer),
            nameof(ProductSale),
            nameof(PurchaseOrder),
            nameof(PurchaseOrderLine),
            nameof(OwnerTransaction),
            nameof(Branch),
        };

        /// <summary>
        /// Drop-in replacement for a bare db.SaveChanges() call on a TenantErpDbContext. Saves
        /// locally exactly as before, then records the changed rows for background sync.
        /// summarySelector optionally supplies a richer, action-specific AuditLog.Summary for a
        /// given changed entry (e.g. "Restocked Canon batteries: +20 units from Manila Photo
        /// Supplies") - entries it doesn't cover fall back to a generic auto-generated summary.
        /// </summary>
        public static void SaveAndSync(TenantErpDbContext db, Func<EntityEntry, string?>? summarySelector = null)
        {
            var changedEntries = CaptureSyncableChanges(db);

            var auditEntries = db.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Where(e => AuditableEntityTypes.Contains(e.Metadata.ClrType.Name))
                .ToList();

            foreach (var entry in auditEntries)
            {
                var entityId = GetPrimaryKeyValue(entry);
                if (entityId is null)
                {
                    continue;
                }

                var action = entry.State switch
                {
                    EntityState.Added => AuditAction.Create,
                    EntityState.Deleted => AuditAction.Delete,
                    _ => AuditAction.Update
                };

                db.AuditLogs.Add(new AuditLog
                {
                    TenantUserId = CurrentActorUserId > 0 ? CurrentActorUserId : null,
                    ActorName = CurrentActorName,
                    EntityName = entry.Metadata.ClrType.Name,
                    EntityId = entityId.Value,
                    Action = action,
                    Summary = summarySelector?.Invoke(entry) ?? BuildDefaultSummary(entry, action, entityId.Value),
                    Timestamp = DateTime.UtcNow
                });
            }

            db.SaveChanges();

            if (changedEntries.Count == 0)
            {
                return;
            }

            QueueOutboxEntries(db, changedEntries);
            db.SaveChanges();
            TriggerBackgroundPush(db);
        }

        /// <summary>
        /// Captures which currently-tracked Added/Modified entries are on a syncable entity type -
        /// call before the caller's own SaveChanges() (while they're still tracked as
        /// Added/Modified), then pass the result to QueueOutboxEntries once that save has run.
        /// Used directly by callers (like SNAP.api's controllers) that write their own AuditLog
        /// rows via AuditLogWriter instead of this class's built-in audit half - SaveAndSync itself
        /// also uses this internally.
        /// </summary>
        public static List<EntityEntry> CaptureSyncableChanges(TenantErpDbContext db) =>
            db.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified)
                .Where(e => SyncableEntityTypes.ContainsKey(e.Metadata.ClrType.Name))
                .ToList();

        /// <summary>
        /// Queues one SyncOutboxEntry per entry from CaptureSyncableChanges - call only after the
        /// caller's own SaveChanges() has already run. That ordering matters: a newly Added
        /// entity's primary key is still EF's temporary negative placeholder until SaveChanges()
        /// actually executes the insert and EF back-fills the real database-generated id onto the
        /// same tracked entry. Save again afterward to persist these rows, then call
        /// TriggerBackgroundPush.
        /// </summary>
        public static void QueueOutboxEntries(TenantErpDbContext db, List<EntityEntry> changedEntries)
        {
            foreach (var entry in changedEntries)
            {
                var entityId = GetPrimaryKeyValue(entry);
                if (entityId is null)
                {
                    continue;
                }

                db.SyncOutboxEntries.Add(new SyncOutboxEntry
                {
                    EntityName = entry.Metadata.ClrType.Name,
                    EntityId = entityId.Value,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        /// <summary>
        /// Fires the background best-effort push for whatever's now pending - call after
        /// SaveChanges() has persisted the rows QueueOutboxEntries added.
        /// </summary>
        public static void TriggerBackgroundPush(TenantErpDbContext db)
        {
            var localConnectionString = db.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(localConnectionString))
            {
                return;
            }

            // Resolved once here (synchronously, off the DB) rather than inside the background
            // Task - ConnectionStrings reads straight from the already-loaded config, so this is
            // cheap and keeps TryPushPending itself not needing to know which tenant it's running
            // for.
            var remoteOverride = ConnectionStrings.TryResolveRemoteForLocal(localConnectionString);
            Task.Run(() => TryPushPending(localConnectionString, remoteOverride));
        }

        /// <summary>
        /// What a sync pass actually did, for a caller (the "Sync Now" endpoint) that needs to
        /// report a real answer rather than fire-and-forget into a background Task. RemoteConfigured
        /// is false when this tenant has no "..._Remote" entry at all - worth telling apart from
        /// "configured but unreachable," since the fix for each is completely different.
        /// RemoteReachable is whether the connection itself worked - kept separate from Synced so a
        /// row the remote *rejected* (e.g. a foreign key) is never reported as "unreachable", which
        /// sends whoever is looking at it off checking their internet. LastError is the remote's own
        /// message for the first rejected row, null when nothing was rejected.
        /// </summary>
        public sealed record SyncResult(bool RemoteConfigured, int TotalPending, int Synced, int StillPending,
                                        bool RemoteReachable = false, string? LastError = null);

        /// <summary>
        /// Fire-and-forget entry point SaveAndSync uses after every save - same work as
        /// PushPendingNow, just without a result anyone's waiting on.
        /// </summary>
        public static void TryPushPending(string localConnectionString, string? remoteConnectionStringOverride = null) =>
            PushPendingNow(localConnectionString, remoteConnectionStringOverride);

        /// <summary>
        /// Pushes every pending (not-yet-synced) outbox entry to this tenant's own remote and
        /// reports what happened - used by both the background fire-and-forget path (TryPushPending,
        /// via SaveAndSync after every save) and the "Sync Now" API endpoint, which actually waits
        /// on the result to tell the caller whether it worked. Safe to call repeatedly - already-
        /// synced rows are skipped, and any row that fails (unreachable remote, or a foreign key not
        /// synced yet) just stays pending and is retried on the next call.
        /// remoteConnectionStringOverride is what SaveAndSync passes - the specific remote paired
        /// with this local database (see ConnectionStrings.TryResolveRemoteForLocal); the
        /// environment variable is only consulted when no per-tenant pairing was found at all.
        /// </summary>
        public static SyncResult PushPendingNow(string localConnectionString, string? remoteConnectionStringOverride = null)
        {
            // Waits for any push already running for this tenant rather than skipping, so a save
            // made mid-pass still gets its own pass (the running one may have read the pending
            // list before that save's row existed) and "Sync Now" always reports a real result.
            var gate = GateFor(localConnectionString);
            gate.Wait();
            try
            {
                return PushPendingCore(localConnectionString, remoteConnectionStringOverride);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// The auto-sync path: same push as PushPendingNow, but returns null immediately instead
        /// of queueing behind a push that's already running for this tenant - a periodic retry
        /// has nothing to add while another pass is in flight, and must never pile up behind one.
        /// </summary>
        public static SyncResult? PushPendingIfIdle(string localConnectionString, string? remoteConnectionStringOverride = null)
        {
            var gate = GateFor(localConnectionString);
            if (!gate.Wait(0))
            {
                return null;
            }

            try
            {
                return PushPendingCore(localConnectionString, remoteConnectionStringOverride);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>True when this tenant has outbox rows still waiting - a cheap local-only check
        /// the auto-sync timer runs first, so it never touches the network when there's nothing
        /// to send.</summary>
        public static bool HasPending(string localConnectionString) => CountPending(localConnectionString) > 0;

        private static SyncResult PushPendingCore(string localConnectionString, string? remoteConnectionStringOverride)
        {
            var remoteConnectionString = remoteConnectionStringOverride
                ?? Environment.GetEnvironmentVariable(RemoteConnectionStringEnvVar);
            if (string.IsNullOrWhiteSpace(remoteConnectionString))
            {
                var pendingCount = CountPending(localConnectionString);
                return new SyncResult(RemoteConfigured: false, TotalPending: pendingCount, Synced: 0, StillPending: pendingCount);
            }

            remoteConnectionString = WithSyncConnectTimeout(remoteConnectionString);

            try
            {
                // No EnableRetryOnFailure on either context here, deliberately - PushEntity opens
                // its own manual transaction (BeginTransaction/Commit, needed so SET IDENTITY_INSERT
                // and the insert share one connection), and EF Core refuses to combine a retrying
                // execution strategy with a user-managed transaction ("does not support user-
                // initiated transactions"). Retry would need CreateExecutionStrategy().Execute(...)
                // wrapping the whole transaction to coexist, which isn't worth the complexity here:
                // this method already has its own outer retry story - a row that fails just stays
                // pending in the outbox and is retried whole on the next save, which is coarser but
                // equally safe (this method is idempotent per row; see PushEntity's remarks).
                using var localDb = new TenantErpDbContext(
                    new DbContextOptionsBuilder<TenantErpDbContext>()
                        .UseSqlServer(localConnectionString)
                        .Options);

                var pending = localDb.SyncOutboxEntries
                    .Where(x => x.SyncedAt == null)
                    .OrderBy(x => x.CreatedAt)
                    .ToList();

                if (pending.Count == 0)
                {
                    return new SyncResult(RemoteConfigured: true, TotalPending: 0, Synced: 0, StillPending: 0, RemoteReachable: true);
                }

                using var remoteDb = new TenantErpDbContext(
                    new DbContextOptionsBuilder<TenantErpDbContext>()
                        .UseSqlServer(remoteConnectionString)
                        .Options);

                // Reachability is checked once, up front. Otherwise an offline pass fails every
                // row separately inside the per-row catch below, each one waiting out its own
                // connect timeout - minutes of holding the push gate just to learn "no internet".
                if (!remoteDb.Database.CanConnect())
                {
                    return new SyncResult(RemoteConfigured: true, TotalPending: pending.Count, Synced: 0, StillPending: pending.Count,
                                          RemoteReachable: false, LastError: $"Could not connect to the remote within {RemoteConnectTimeoutSeconds}s.");
                }

                var synced = 0;
                string? lastError = null;
                foreach (var outboxEntry in pending)
                {
                    try
                    {
                        if (PushEntity(localDb, remoteDb, outboxEntry.EntityName, outboxEntry.EntityId, depth: 0))
                        {
                            outboxEntry.SyncedAt = DateTime.UtcNow;
                            localDb.SaveChanges();
                            synced++;
                        }
                    }
                    catch (Exception ex)
                    {
                        lastError ??= $"{outboxEntry.EntityName} #{outboxEntry.EntityId}: {ex.GetBaseException().Message}";

                        // Leave this row pending - it will be retried on the next sync pass. A
                        // common transient cause is a foreign key (e.g. a Booking's Customer)
                        // that hasn't synced yet; it resolves itself once that row lands.
                        //
                        // PushEntity only clears remoteDb's change tracker on its own success path
                        // (right before it returns true) - when it throws instead, whatever it had
                        // already added/attached to remoteDb stays tracked. Since remoteDb is
                        // reused across every row in this loop, that leftover state then rides
                        // along into the NEXT row's SaveChanges() too, producing a second failure
                        // that looks unrelated (e.g. an IDENTITY_INSERT error naming a completely
                        // different table than the one currently being pushed). Clearing here, on
                        // every failure, stops one bad row from corrupting every row after it.
                        remoteDb.ChangeTracker.Clear();
                    }
                }

                return new SyncResult(RemoteConfigured: true, TotalPending: pending.Count, Synced: synced,
                                      StillPending: pending.Count - synced, RemoteReachable: true, LastError: lastError);
            }
            catch (Exception ex)
            {
                // Remote unreachable (offline, DNS, timeout, etc.) - every row simply stays
                // pending in the local outbox and will be retried next time.
                var stillPending = CountPending(localConnectionString);
                return new SyncResult(RemoteConfigured: true, TotalPending: stillPending, Synced: 0, StillPending: stillPending,
                                      RemoteReachable: false, LastError: ex.GetBaseException().Message);
            }
        }

        /// <summary>
        /// Sets the remote's Connect Timeout to RemoteConnectTimeoutSeconds, overriding whatever
        /// the stored connection string carries, so every sync pass waits the same known amount.
        /// </summary>
        private static string WithSyncConnectTimeout(string connectionString)
        {
            try
            {
                var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
                builder.ConnectTimeout = RemoteConnectTimeoutSeconds;
                return builder.ConnectionString;
            }
            catch (ArgumentException)
            {
                // Unparseable - leave it exactly as configured; the push itself will report it.
                return connectionString;
            }
        }

        /// <summary>Best-effort count for reporting purposes only - swallows its own failures so a
        /// broken local connection (which would have already failed above) can't throw a second
        /// time out of a catch block.</summary>
        private static int CountPending(string localConnectionString)
        {
            try
            {
                using var db = new TenantErpDbContext(
                    new DbContextOptionsBuilder<TenantErpDbContext>()
                        .UseSqlServer(localConnectionString, sql => sql.EnableRetryOnFailure(3))
                        .Options);
                return db.SyncOutboxEntries.Count(x => x.SyncedAt == null);
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>How far EnsureParentsOnRemote follows a chain of missing parents (e.g. PO line →
        /// PO → Supplier) - well past this schema's deepest chain, and a hard stop on any cycle.</summary>
        private const int MaxParentDepth = 5;

        private static bool PushEntity(TenantErpDbContext localDb, TenantErpDbContext remoteDb, string entityName, int entityId, int depth)
        {
            if (!SyncableEntityTypes.TryGetValue(entityName, out var entityType))
            {
                return false;
            }

            var localEntity = localDb.Find(entityType, entityId);
            if (localEntity is null)
            {
                // Row was deleted locally after being queued - nothing to push.
                return true;
            }

            if (depth < MaxParentDepth)
            {
                EnsureParentsOnRemote(localDb, remoteDb, localEntity, entityType, entityId, depth);
            }

            var remoteEntityType = remoteDb.Model.FindEntityType(entityType)
                ?? throw new InvalidOperationException($"No entity type metadata for {entityName}.");
            var tableName = remoteEntityType.GetTableName();

            var existingRemote = remoteDb.Find(entityType, entityId);
            if (existingRemote is null)
            {
                // SET IDENTITY_INSERT is scoped to the connection/session it runs on, so the ON,
                // the insert, and the OFF must all share one connection - an explicit transaction
                // guarantees EF doesn't open a fresh connection between those commands.
                using var transaction = remoteDb.Database.BeginTransaction();
                remoteDb.Database.ExecuteSqlRaw($"SET IDENTITY_INSERT dbo.[{tableName}] ON");
                remoteDb.Add(Clone(localEntity, entityType));
                remoteDb.SaveChanges();
                remoteDb.Database.ExecuteSqlRaw($"SET IDENTITY_INSERT dbo.[{tableName}] OFF");
                transaction.Commit();
            }
            else
            {
                remoteDb.Entry(existingRemote).CurrentValues.SetValues(localEntity);
                remoteDb.SaveChanges();
            }

            // Detach so the next iteration's Find() for a different row of the same type doesn't
            // collide with an already-tracked instance.
            remoteDb.ChangeTracker.Clear();

            return true;
        }

        /// <summary>
        /// Pushes any row this one references (its Supplier, Customer, Branch, Inventory item...)
        /// that the remote doesn't have yet, before the row itself. Without this a single missing
        /// parent - one whose own outbox entry failed, predates the outbox, or was written
        /// straight to the local database - makes the remote reject the child with a foreign-key
        /// error on every pass, forever, and the row never leaves the "waiting" list.
        /// </summary>
        private static void EnsureParentsOnRemote(TenantErpDbContext localDb, TenantErpDbContext remoteDb,
                                                  object localEntity, Type entityType, int entityId, int depth)
        {
            var localEntityType = localDb.Model.FindEntityType(entityType);
            if (localEntityType is null) return;

            foreach (var foreignKey in localEntityType.GetForeignKeys())
            {
                var principalType = foreignKey.PrincipalEntityType.ClrType;
                if (foreignKey.Properties.Count != 1
                    || !SyncableEntityTypes.TryGetValue(principalType.Name, out var syncableType)
                    || syncableType != principalType)
                {
                    continue;
                }

                if (localDb.Entry(localEntity).Property(foreignKey.Properties[0].Name).CurrentValue is not int parentId
                    || (principalType == entityType && parentId == entityId))
                {
                    continue;
                }

                var parentOnRemote = remoteDb.Find(principalType, parentId) is not null;
                remoteDb.ChangeTracker.Clear();
                if (!parentOnRemote)
                {
                    PushEntity(localDb, remoteDb, principalType.Name, parentId, depth + 1);
                }
            }
        }

        private static object Clone(object source, Type entityType)
        {
            var clone = Activator.CreateInstance(entityType)!;
            foreach (var property in entityType.GetProperties())
            {
                if (!property.CanRead || !property.CanWrite || property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                // Skip navigation properties (Booking.Branch, Booking.Customer, etc.). If the
                // source entity came from a query where EF's change tracker had already fixed up
                // a navigation to an already-tracked related entity (no explicit Include()
                // needed - EF does this automatically), copying that reference onto the clone
                // makes remoteDb see an apparently-new related entity and cascade an extra insert
                // for it, which fails because IDENTITY_INSERT is only turned on for this entity's
                // own table. Only scalar columns (ids, strings, dates, decimals, enums) belong on
                // the clone - the FK id property (e.g. BranchId) already carries the relationship.
                var isNavigation = property.PropertyType.IsClass && property.PropertyType != typeof(string);
                if (isNavigation)
                {
                    continue;
                }

                property.SetValue(clone, property.GetValue(source));
            }

            return clone;
        }

        /// <summary>
        /// Generic "Created/Updated/Deleted {Entity}: {label}" summary for an audited entry that
        /// didn't get a richer, action-specific summary from the caller's summarySelector.
        /// </summary>
        private static string BuildDefaultSummary(EntityEntry entry, AuditAction action, int entityId)
        {
            var entityName = entry.Metadata.ClrType.Name;
            var verb = action switch
            {
                AuditAction.Create => "Created",
                AuditAction.Delete => "Deleted",
                _ => "Updated"
            };

            var label = entry.Entity switch
            {
                Equipment e => e.EquipmentName,
                Inventory i => i.ItemName,
                Supplier s => s.SupplierName,
                TenantUser u => u.FullName,
                Customer c => c.FullName,
                Booking b => $"{b.PackageNameSnapshot} on {b.SessionDate:MMM d, yyyy}",
                Payment p => $"₱{p.AmountPaid:N2} toward booking #{p.BookingId}",
                Expense ex => $"₱{ex.Amount:N2} ({ex.Category})",
                SalaryPayment sp => $"₱{sp.NetAmount:N2} net salary payment",
                ProductSale ps => $"{ps.Quantity:0.##} x item #{ps.InventoryId} (₱{ps.TotalAmount:N2})",
                PurchaseOrder po => $"PO for supplier #{po.SupplierId} ({po.Status})",
                PurchaseOrderLine pol => $"{pol.QuantityOrdered:0.##} x item #{pol.InventoryId} on PO #{pol.PurchaseOrderId}",
                OwnerTransaction ot => $"{ot.Type} of ₱{ot.Amount:N2}",
                _ => null
            };

            return label is null ? $"{verb} {entityName} #{entityId}" : $"{verb} {entityName}: {label}";
        }

        private static int? GetPrimaryKeyValue(EntityEntry entry)
        {
            var pkProperty = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault();
            if (pkProperty is null)
            {
                return null;
            }

            var value = entry.Property(pkProperty.Name).CurrentValue;
            return value is int intValue ? intValue : null;
        }

        /// <summary>One row still waiting to reach this tenant's remote, with a human-readable
        /// description instead of a bare EntityName/EntityId pair - what the Data Sync screen
        /// shows so a pending count isn't the only thing anyone sees.</summary>
        public sealed record PendingSyncRow(string EntityName, string Description, DateTime CreatedAt);

        /// <summary>
        /// Resolves every currently-pending outbox entry against its live local row and describes
        /// it the same way BuildDefaultSummary's audit labels do, just without the Created/
        /// Updated/Deleted verb (a pending row might be either, and "waiting to sync" already says
        /// what matters). A row whose entity was deleted locally after being queued shows as such
        /// rather than being silently skipped - it still occupies a slot in SyncOutboxEntries.
        /// </summary>
        public static List<PendingSyncRow> GetPendingDetails(TenantErpDbContext db)
        {
            var pending = db.SyncOutboxEntries
                .Where(x => x.SyncedAt == null)
                .OrderBy(x => x.CreatedAt)
                .ToList();

            var result = new List<PendingSyncRow>();
            foreach (var outboxEntry in pending)
            {
                if (!SyncableEntityTypes.TryGetValue(outboxEntry.EntityName, out var entityType))
                {
                    continue;
                }

                var entity = db.Find(entityType, outboxEntry.EntityId);
                var description = entity is null
                    ? $"{outboxEntry.EntityName} #{outboxEntry.EntityId} (deleted locally since)"
                    : DescribeForPendingList(outboxEntry.EntityName, outboxEntry.EntityId, entity);

                result.Add(new PendingSyncRow(outboxEntry.EntityName, description, outboxEntry.CreatedAt));
            }

            return result;
        }

        private static string DescribeForPendingList(string entityName, int entityId, object entity)
        {
            var label = entity switch
            {
                Equipment e => e.EquipmentName,
                Inventory i => i.ItemName,
                Supplier s => s.SupplierName,
                TenantUser u => u.FullName,
                Customer c => c.FullName,
                Booking b => $"{b.PackageNameSnapshot} on {b.SessionDate:MMM d, yyyy}",
                Payment p => $"₱{p.AmountPaid:N2} toward booking #{p.BookingId}",
                Expense ex => $"₱{ex.Amount:N2} ({ex.Category})",
                SalaryPayment sp => $"₱{sp.NetAmount:N2} net salary payment",
                ProductSale ps => $"{ps.Quantity:0.##} x item #{ps.InventoryId} (₱{ps.TotalAmount:N2})",
                PurchaseOrder po => $"PO for supplier #{po.SupplierId} ({po.Status})",
                PurchaseOrderLine pol => $"{pol.QuantityOrdered:0.##} x item #{pol.InventoryId} on PO #{pol.PurchaseOrderId}",
                OwnerTransaction ot => $"{ot.Type} of ₱{ot.Amount:N2}",
                PhotographyPackage pkg => pkg.PackageName,
                Branch br => br.BranchName,
                _ => null
            };

            return label is null ? $"{entityName} #{entityId}" : $"{entityName}: {label}";
        }
    }
}
