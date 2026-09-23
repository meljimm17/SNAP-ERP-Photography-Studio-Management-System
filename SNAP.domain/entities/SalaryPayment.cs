namespace SNAP.domain.entities
{
    public class SalaryPayment
    {
        public int SalaryPaymentId { get; set; }

        public int TenantUserId { get; set; }

        public TenantUser? TenantUser { get; set; }

        /// <summary>Renamed from Amount - the employee's gross pay for the period, before statutory
        /// deductions.</summary>
        public decimal GrossAmount { get; set; }

        public decimal SssDeduction { get; set; }

        public decimal PhilHealthDeduction { get; set; }

        public decimal PagIbigDeduction { get; set; }

        public decimal WithholdingTax { get; set; }

        /// <summary>What's actually paid out: GrossAmount minus every deduction above.</summary>
        public decimal NetAmount { get; set; }

        public DateTime PayPeriodStart { get; set; }

        public DateTime PayPeriodEnd { get; set; }

        public DateTime PaymentDate { get; set; }

        public string? Notes { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
