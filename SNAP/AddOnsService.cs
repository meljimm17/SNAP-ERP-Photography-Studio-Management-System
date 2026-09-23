using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;

namespace SNAP
{
    /// <summary>
    /// Single place that turns a booking's desired add-on selections (from the shared Add-ons
    /// dialog, opened either from the Booking Dialog or from Payment Management) into ProductSale
    /// rows and Inventory stock changes - and the reverse, when an item is unchecked/reduced on
    /// edit or a booking is cancelled. Both callers, and the Booking-cancellation code path, share
    /// this one method rather than each re-implementing the diff/reversal logic.
    /// </summary>
    public static class AddOnsService
    {
        public sealed record AddOnSelection(int InventoryId, decimal Quantity, decimal UnitPrice);

        /// <summary>
        /// Reconciles a booking's ProductSale rows against a newly-selected set of add-ons:
        /// increases/new items become new (or larger) ProductSale rows and decrement stock;
        /// removed/reduced items get soft-deleted (or shrunk) and restore stock. Call with an empty
        /// list to reverse every existing add-on for the booking (e.g. on cancellation).
        /// </summary>
        public static void ApplyAddOns(TenantErpDbContext db, int bookingId, int? customerId, IReadOnlyList<AddOnSelection> desired)
        {
            var existing = db.ProductSales
                .Where(p => p.BookingId == bookingId && !p.IsDeleted)
                .ToList();

            var desiredByItem = desired.ToDictionary(d => d.InventoryId, d => d.Quantity);

            // Reverse rows for items that are no longer desired, or whose quantity dropped.
            foreach (var group in existing.GroupBy(p => p.InventoryId))
            {
                var currentQty = group.Sum(p => p.Quantity);
                var desiredQty = desiredByItem.GetValueOrDefault(group.Key, 0m);

                if (desiredQty < currentQty)
                {
                    ReverseQuantity(db, group.Key, group.OrderByDescending(p => p.CreatedAt).ToList(), currentQty - desiredQty);
                }
            }

            // Create/increase rows for desired items.
            foreach (var item in desired)
            {
                if (item.Quantity <= 0) continue;

                var currentQty = existing.Where(p => p.InventoryId == item.InventoryId).Sum(p => p.Quantity);
                var delta = item.Quantity - currentQty;
                if (delta <= 0) continue;

                var inventory = db.InventoryItems.Find(item.InventoryId);
                if (inventory is null) continue;

                db.ProductSales.Add(new ProductSale
                {
                    InventoryId = item.InventoryId,
                    CustomerId = customerId,
                    BookingId = bookingId,
                    Quantity = delta,
                    UnitPrice = item.UnitPrice,
                    TotalAmount = delta * item.UnitPrice,
                    SaleDate = DateTime.Today,
                    // Add-ons ride the booking's own payment (collected separately via Payment
                    // Management) rather than a payment method chosen per add-on line - Cash is a
                    // placeholder to satisfy ProductSale's required field, not a real transaction.
                    PaymentMethod = PaymentMethod.Cash,
                    CreatedAt = DateTime.UtcNow
                });

                inventory.QuantityOnHand -= delta;
                inventory.LastUpdatedAt = DateTime.UtcNow;
            }
        }

        /// <summary>Reverses every existing add-on for a booking - used when a booking is cancelled.</summary>
        public static void ReverseAllAddOns(TenantErpDbContext db, int bookingId)
        {
            ApplyAddOns(db, bookingId, customerId: null, desired: Array.Empty<AddOnSelection>());
        }

        private static void ReverseQuantity(TenantErpDbContext db, int inventoryId, List<ProductSale> rowsNewestFirst, decimal quantityToReverse)
        {
            var inventory = db.InventoryItems.Find(inventoryId);
            var remaining = quantityToReverse;

            foreach (var row in rowsNewestFirst)
            {
                if (remaining <= 0) break;

                if (row.Quantity <= remaining)
                {
                    remaining -= row.Quantity;
                    row.IsDeleted = true;
                    if (inventory is not null) inventory.QuantityOnHand += row.Quantity;
                }
                else
                {
                    row.Quantity -= remaining;
                    row.TotalAmount = row.Quantity * row.UnitPrice;
                    if (inventory is not null) inventory.QuantityOnHand += remaining;
                    remaining = 0;
                }
            }

            if (inventory is not null) inventory.LastUpdatedAt = DateTime.UtcNow;
        }
    }
}
