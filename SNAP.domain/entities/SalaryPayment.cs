namespace SNAP.domain.entities
{
    public class SalaryPayment
    {
        public int SalaryPaymentId { get; set; }

        public int TenantUserId { get; set; }

        public TenantUser? TenantUser { get; set; }

        public decimal Amount { get; set; }

        public DateTime PayPeriodStart { get; set; }

        public DateTime PayPeriodEnd { get; set; }

        public DateTime PaymentDate { get; set; }

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
