using SNAP.domain.enums;

namespace SNAP.domain.entities
{
    /// <summary>
    /// Money the owner puts into or takes out of the business outside of their own salary (e.g.
    /// a personal cash injection, or a profit withdrawal) - needed so the Balance Sheet still
    /// balances when that happens; without it, Cash would silently be wrong.
    /// </summary>
    public class OwnerTransaction
    {
        public int OwnerTransactionId { get; set; }

        public OwnerTransactionType Type { get; set; }

        public decimal Amount { get; set; }

        public DateTime TransactionDate { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
