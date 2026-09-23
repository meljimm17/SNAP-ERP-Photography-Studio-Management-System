using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;

namespace SNAP.infrastructure.services
{
    /// <summary>
    /// Server-side counterpart to TenantSyncService's audit-logging half, for use by SNAP.api
    /// controllers once they own a save directly (no local-first sync outbox needed server-side -
    /// the API database write already *is* the single source of truth). TenantSyncService itself
    /// stays in place and unchanged for the WinForms screens not yet converted to call the API;
    /// once every screen is converted, TenantSyncService/SyncOutboxEntry can be removed and this
    /// becomes the only audit-writing path.
    /// </summary>
    public static class AuditLogWriter
    {
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
        };

        /// <summary>
        /// Call immediately before db.SaveChanges() - queues one AuditLog row per currently-tracked
        /// Added/Modified/Deleted change on an auditable entity type, then the caller's own
        /// SaveChanges() persists both the change and its audit row in the same call.
        /// </summary>
        public static void QueueAuditEntries(TenantErpDbContext db, int? actorUserId, string actorName, Func<EntityEntry, string?>? summarySelector = null)
        {
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
                    TenantUserId = actorUserId,
                    ActorName = actorName,
                    EntityName = entry.Metadata.ClrType.Name,
                    EntityId = entityId.Value,
                    Action = action,
                    Summary = summarySelector?.Invoke(entry) ?? BuildDefaultSummary(entry, action, entityId.Value),
                    Timestamp = DateTime.UtcNow
                });
            }
        }

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
    }
}
