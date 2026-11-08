namespace SNAP.domain.enums
{
    /// <summary>
    /// Subscription tier a Company is on, matching SubscriptionPlan.PlanName / Tenant.BusinessSize.
    /// Micro and Small are fully built out. Medium adds multi-branch operation: every tier-gated
    /// module below (Equipment/Inventory/Finance/Reports) becomes branch-aware, and Branch
    /// Management itself is Medium-only, enforced server-side by RequirePlanTierAttribute.
    ///
    /// Declaration order is the comparison order - RequirePlanTierAttribute treats a tier as
    /// satisfying a requirement when it sorts at or above it, so new tiers must be appended, never
    /// inserted. The stored value is the underlying int, so renaming a member is safe for existing
    /// Company rows but reordering them is not.
    /// </summary>
    public enum PlanTier
    {
        Micro,
        Small,
        Medium
    }
}
