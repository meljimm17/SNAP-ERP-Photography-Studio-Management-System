namespace SNAP.domain.enums
{
    /// <summary>
    /// Billing state of a Company's subscription, deliberately separate from Company.IsActive:
    /// IsActive is the soft-delete flag (the company exists at all), while this says whether their
    /// plan is currently paid up. A suspended company is still a real company with real data.
    /// </summary>
    public enum SubscriptionStatus
    {
        Active,
        Suspended
    }
}
