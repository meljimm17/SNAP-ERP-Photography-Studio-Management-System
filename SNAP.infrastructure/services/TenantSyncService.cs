using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SNAP.domain.entities;
using SNAP.domain.enums;
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
            var changedEntries = db.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified)
                .Where(e => SyncableEntityTypes.ContainsKey(e.Metadata.ClrType.Name))
                .ToList();

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
                SalaryPayment sp => $"₱{sp.Amount:N2} salary payment",
                ProductSale ps => $"{ps.Quantity:0.##} x item #{ps.InventoryId} (₱{ps.TotalAmount:N2})",
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
    }
}
