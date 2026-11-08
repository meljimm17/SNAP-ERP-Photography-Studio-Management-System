using System;
using System.Collections.Generic;
using System.Text;
using SNAP.domain.enums;

namespace SNAP.domain.entities
{
    public class Company
    {
        public int CompanyId { get; set; }

        public string CompanyCode { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public PlanTier PlanTier { get; set; } = PlanTier.Micro;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Billing state, independent of IsActive. IsActive decides whether the company can log in
        /// at all; this tracks whether their plan is current. No payment is collected anywhere -
        /// this records which plan they're on and when it renews, nothing more.
        /// </summary>
        public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Active;

        public DateTime SubscriptionStartDate { get; set; } = DateTime.UtcNow;

        public DateTime? SubscriptionRenewalDate { get; set; }

        /// <summary>
        /// A tenant-initiated "please move us to this tier" request, sitting alongside the current
        /// PlanTier rather than replacing it - nothing about the company's actual access changes
        /// until a SuperAdmin reviews it and applies the change through the Master Panel's existing
        /// Change Plan / Renewal flow (which also clears these two fields once actioned, whether
        /// approved as requested or given a different tier). Both null means no request pending.
        /// </summary>
        public PlanTier? RequestedPlanTier { get; set; }
        public DateTime? UpgradeRequestedAt { get; set; }
    }
}

