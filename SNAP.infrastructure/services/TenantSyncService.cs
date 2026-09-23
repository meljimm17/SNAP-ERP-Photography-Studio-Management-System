using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SNAP.domain.entities;
using SNAP.infrastructure.Data;

namespace SNAP.infrastructure.services
{
    /// <summary>
    /// Local-first write path: every save goes to the local tenant database first (unchanged
    /// behavior), then this records which rows changed and best-effort-pushes them to the
    /// MonsterASP remote in the background. If the remote is unreachable, the row just stays
    /// pending in SyncOutboxEntries and gets retried on the next save or the next login - nothing
    /// is lost, and no local save is ever blocked or slowed down waiting on the remote.
    /// </summary>
    public static class TenantSyncService
    {
        private const string RemoteConnectionStringEnvVar = "SNAP_TENANTERP_REMOTE_CONNECTIONSTRING";

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
        };

        /// <summary>
        /// Drop-in replacement for a bare db.SaveChanges() call on a TenantErpDbContext. Saves
        /// locally exactly as before, then records the changed rows for background sync.
        /// </summary>
        public static void SaveAndSync(TenantErpDbContext db)
        {
            var changedEntries = db.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified)
                .Where(e => SyncableEntityTypes.ContainsKey(e.Metadata.ClrType.Name))
                .ToList();

            db.SaveChanges();

            if (changedEntries.Count == 0)
            {
                return;
            }

            var localConnectionString = db.Database.GetConnectionString();

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

            db.SaveChanges();

            if (!string.IsNullOrWhiteSpace(localConnectionString))
            {
                Task.Run(() => TryPushPending(localConnectionString));
            }
        }

        /// <summary>
        /// Pushes every pending (not-yet-synced) outbox entry to the MonsterASP remote. Safe to
        /// call repeatedly (e.g. on every save and again on login) - already-synced rows are
        /// skipped, and any row that fails (unreachable remote, or a foreign key not synced yet)
        /// just stays pending and is retried on the next call.
        /// </summary>
        public static void TryPushPending(string localConnectionString)
        {
            var remoteConnectionString = Environment.GetEnvironmentVariable(RemoteConnectionStringEnvVar);
            if (string.IsNullOrWhiteSpace(remoteConnectionString))
            {
                return;
            }

            if (!remoteConnectionString.Contains("Connect Timeout", StringComparison.OrdinalIgnoreCase))
            {
                remoteConnectionString += "Connect Timeout=5;";
            }

            try
            {
                using var localDb = new TenantErpDbContext(
                    new DbContextOptionsBuilder<TenantErpDbContext>().UseSqlServer(localConnectionString).Options);

                var pending = localDb.SyncOutboxEntries
                    .Where(x => x.SyncedAt == null)
                    .OrderBy(x => x.CreatedAt)
                    .ToList();

                if (pending.Count == 0)
                {
                    return;
                }

                using var remoteDb = new TenantErpDbContext(
                    new DbContextOptionsBuilder<TenantErpDbContext>().UseSqlServer(remoteConnectionString).Options);

                foreach (var outboxEntry in pending)
                {
                    try
                    {
                        if (PushEntity(localDb, remoteDb, outboxEntry.EntityName, outboxEntry.EntityId))
                        {
                            outboxEntry.SyncedAt = DateTime.UtcNow;
                            localDb.SaveChanges();
                        }
                    }
                    catch
                    {
                        // Leave this row pending - it will be retried on the next sync pass. A
                        // common transient cause is a foreign key (e.g. a Booking's Customer)
                        // that hasn't synced yet; it resolves itself once that row lands.
                    }
                }
            }
            catch
            {
                // Remote unreachable (offline, DNS, timeout, etc.) - every row simply stays
                // pending in the local outbox and will be retried next time.
            }
        }

        private static bool PushEntity(TenantErpDbContext localDb, TenantErpDbContext remoteDb, string entityName, int entityId)
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

        private static object Clone(object source, Type entityType)
        {
            var clone = Activator.CreateInstance(entityType)!;
            foreach (var property in entityType.GetProperties())
            {
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0)
                {
                    property.SetValue(clone, property.GetValue(source));
                }
            }

            return clone;
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
    }
}
