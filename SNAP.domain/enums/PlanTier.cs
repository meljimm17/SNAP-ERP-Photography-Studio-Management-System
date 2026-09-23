namespace SNAP.domain.enums
{
    /// <summary>
    /// Subscription tier a Company is on, matching SubscriptionPlan.PlanName / Tenant.BusinessSize.
    /// Only Micro is fully built out today; Small/Enterprise are reserved for future module gating
    /// (multi-branch, equipment/inventory, finance/payroll).
    /// </summary>
    public enum PlanTier
    {
        Micro,
        Small,
        Enterprise
    }
}
