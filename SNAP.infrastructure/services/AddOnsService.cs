using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;

namespace SNAP.infrastructure.services
{
    /// <summary>
    /// Add-ons reconciliation logic - matches desired add-ons against existing product sales
    /// and updates inventory accordingly.
    /// </summary>
    public static class AddOnsService
    {
        public sealed record AddOnSelection(int InventoryId, decimal Quantity, decimal UnitPrice);

        public static void ApplyAddOns(TenantErpDbContext db, int bookingId, int? customerId, IReadOnlyList<AddOnSelection> desired)
        {
            // Required column - a booking add-on's ProductSale always inherits the linked
            // Booking's branch (see Branch scoping remarks: "ProductSale inherited from the linked
            // Booking"). Missing this was a guaranteed crash the moment anyone actually added a
            // paid add-on to a booking.
            var branchId = db.Bookings
                .Where(b => b.BookingId == bookingId)
                .Select(b => (int?)b.BranchId)
                .FirstOrDefault();

            var existing = db.ProductSales
                .Where(p => p.BookingId == bookingId && !p.IsDeleted)
                .ToList();

            var desiredByItem = desired.ToDictionary(d => d.InventoryId, d => d.Quantity);

            foreach (var group in existing.GroupBy(p => p.InventoryId))
            {
                var currentQty = group.Sum(p => p.Quantity);
                var desiredQty = desiredByItem.GetValueOrDefault(group.Key, 0m);

                if (desiredQty < currentQty)
                {
                    ReverseQuantity(db, group.Key, group.OrderByDescending(p => p.CreatedAt).ToList(), currentQty - desiredQty);
                }
            }

            foreach (var item in desired)
            {
                if (item.Quantity <= 0) continue;

                var currentQty = existing.Where(p => p.InventoryId == item.InventoryId).Sum(p => p.Quantity);
                var delta = item.Quantity - currentQty;
                if (delta <= 0) continue;

                var inventory = db.InventoryItems.Find(item.InventoryId);
                if (inventory is null) continue;

                if (branchId is null)
                {
                    throw new InvalidOperationException($"Booking #{bookingId} was not found, so its add-ons have no branch to belong to.");
                }

                db.ProductSales.Add(new ProductSale
                {
                    InventoryId = item.InventoryId,
                    CustomerId = customerId,
                    BookingId = bookingId,
                    Quantity = delta,
                    UnitPrice = item.UnitPrice,
                    TotalAmount = delta * item.UnitPrice,
                    SaleDate = DateTime.Today,
                    PaymentMethod = PaymentMethod.Cash,
                    CreatedAt = DateTime.UtcNow,
                    BranchId = branchId.Value
                });

                inventory.QuantityOnHand -= delta;
                inventory.LastUpdatedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Add-ons are billed on top of the package: a booking's amount due is PriceSnapshot plus
        /// this. Every balance calculation (Payments, receipts, Outstanding, receivables) must use
        /// it, or a booking reads "Paid" while its add-ons are still uncollected.
        /// </summary>
        public static decimal GetAddOnsTotal(TenantErpDbContext db, int bookingId) =>
            db.ProductSales
                .Where(p => p.BookingId == bookingId && !p.IsDeleted)
                .Sum(p => (decimal?)p.TotalAmount) ?? 0m;

        /// <summary>Batch form of GetAddOnsTotal, keyed by BookingId - bookings without add-ons
        /// are absent, so read with GetValueOrDefault.</summary>
        public static Dictionary<int, decimal> GetAddOnsTotals(TenantErpDbContext db, IReadOnlyCollection<int> bookingIds) =>
            db.ProductSales
                .Where(p => !p.IsDeleted && p.BookingId != null && bookingIds.Contains(p.BookingId.Value))
                .GroupBy(p => p.BookingId!.Value)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.TotalAmount));

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
