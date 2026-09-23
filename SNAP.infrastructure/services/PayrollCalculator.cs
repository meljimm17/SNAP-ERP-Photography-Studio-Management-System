namespace SNAP.infrastructure.services
{
    /// <summary>
    /// Philippine statutory payroll deductions for one monthly pay run (SSS, PhilHealth, Pag-IBIG,
    /// BIR withholding tax). Rates are current as of this writing (cross-checked against Sprout
    /// Solutions, PIA, and KamiWorkforce) but are revised periodically by law - verify against
    /// official sources before relying on this for real disbursement.
    /// </summary>
    public static class PayrollCalculator
    {
        public sealed record PayrollBreakdown(
            decimal Gross,
            decimal SssDeduction,
            decimal PhilHealthDeduction,
            decimal PagIbigDeduction,
            decimal TaxableIncome,
            decimal WithholdingTax,
            decimal NetAmount);

        public static PayrollBreakdown Compute(decimal grossMonthlySalary)
        {
            var sss = ComputeSss(grossMonthlySalary);
            var philHealth = ComputePhilHealth(grossMonthlySalary);
            var pagIbig = ComputePagIbig(grossMonthlySalary);

            var taxableIncome = Math.Max(0m, grossMonthlySalary - sss - philHealth - pagIbig);
            var withholdingTax = ComputeWithholdingTax(taxableIncome);

            var net = grossMonthlySalary - sss - philHealth - pagIbig - withholdingTax;

            return new PayrollBreakdown(grossMonthlySalary, sss, philHealth, pagIbig, taxableIncome, withholdingTax, net);
        }

        /// <summary>5% (employee share) of the Monthly Salary Credit - gross salary bracketed into
        /// the official ₱500-increment MSC steps, floored at ₱5,000 and capped at ₱35,000.</summary>
        private static decimal ComputeSss(decimal gross)
        {
            var msc = Math.Round(gross / 500m, MidpointRounding.AwayFromZero) * 500m;
            msc = Math.Clamp(msc, 5000m, 35000m);
            return Math.Round(msc * 0.05m, 2);
        }

        /// <summary>2.5% of Gross, floored at ₱250 (for Gross ≤ ₱10,000) and capped at ₱2,500 (for
        /// Gross ≥ ₱100,000).</summary>
        private static decimal ComputePhilHealth(decimal gross)
        {
            if (gross <= 10000m) return 250m;
            if (gross >= 100000m) return 2500m;
            return Math.Round(gross * 0.025m, 2);
        }

        /// <summary>1% up to ₱1,500, 2% from ₱1,501-₱10,000, capped at ₱200 above ₱10,000.</summary>
        private static decimal ComputePagIbig(decimal gross)
        {
            if (gross <= 1500m) return Math.Round(gross * 0.01m, 2);
            if (gross <= 10000m) return Math.Round(gross * 0.02m, 2);
            return 200m;
        }

        /// <summary>BIR monthly withholding tax table.</summary>
        private static decimal ComputeWithholdingTax(decimal taxableIncome)
        {
            return taxableIncome switch
            {
                <= 20833m => 0m,
                <= 33333m => Math.Round((taxableIncome - 20833m) * 0.15m, 2),
                <= 66667m => Math.Round(1875m + (taxableIncome - 33333m) * 0.20m, 2),
                <= 166667m => Math.Round(8541.80m + (taxableIncome - 66667m) * 0.25m, 2),
                <= 666667m => Math.Round(33541.80m + (taxableIncome - 166667m) * 0.30m, 2),
                _ => Math.Round(183541.80m + (taxableIncome - 666667m) * 0.35m, 2)
            };
        }
    }
}
