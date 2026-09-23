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
    }
}

