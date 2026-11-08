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

        /// <summary>When the payroll run that produced this payslip was dated - not when the money
        /// actually changed hands, which is PaidDate.</summary>
        public DateTime PaymentDate { get; set; }

        /// <summary>False until the employee is actually paid. A payslip is created owed, then
        /// settled from the Salary Payments list, the same two-step flow Expenses use.</summary>
        public bool IsPaid { get; set; }

        /// <summary>The day the employee was actually paid. Null while still owed.</summary>
        public DateTime? PaidDate { get; set; }

        public string? Notes { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
