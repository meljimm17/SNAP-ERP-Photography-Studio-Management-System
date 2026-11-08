using SNAP.domain.enums;
using SNAP.infrastructure.Data;

namespace SNAP.api.Services
{
    /// <summary>
    /// Decides which branch's data the current request is allowed to touch.
    ///
    /// This is the enforcement point for branch isolation. A BranchManager, Staff or Photographer
    /// carries a "branchId" claim, set server-side at login from their TenantUser record; whatever
    /// branch their client asks for is ignored and their own is used instead. A company-wide role
    /// (Admin, FinanceManager, SuperAdmin) has no such claim and may ask for one branch or, by
    /// asking for none, all of them.
    ///
    /// Hiding a branch picker in the UI is not isolation - without this a branch-bound user could
    /// read or write another branch's data by calling the endpoint directly, the same hole
    /// RequirePlanTierAttribute exists to close for tier gating.
    /// </summary>
    public class BranchScope
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BranchScope(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// The branch this user is locked to, or null when they may see every branch.
        /// </summary>
        public int? RestrictedBranchId
        {
            get
            {
                var claim = _httpContextAccessor.HttpContext?.User.FindFirst("branchId")?.Value;
                return int.TryParse(claim, out var branchId) && branchId > 0 ? branchId : null;
            }
        }

        public bool IsRestricted => RestrictedBranchId is not null;

        /// <summary>
        /// Whether this tenant's plan tier makes branch selection a real choice at all. Mirrors
        /// SNAP's own AppSession.IsBranchAware (CurrentPlanTier >= Medium) - below Medium every
        /// tenant has exactly one Main Branch, so there is nothing to pick.
        /// </summary>
        private bool IsBranchAware
        {
            get
            {
                var claim = _httpContextAccessor.HttpContext?.User.FindFirst("planTier")?.Value;
                return Enum.TryParse<PlanTier>(claim, out var planTier) && planTier >= PlanTier.Medium;
            }
        }

        /// <summary>
        /// The branch a read should filter to: the caller's own branch if they're restricted,
        /// otherwise whatever they asked for. Null means "every branch" - only reachable by a
        /// company-wide role that asked for no particular branch.
        /// </summary>
        public int? ResolveForRead(int? requestedBranchId) =>
            RestrictedBranchId ?? requestedBranchId;

        /// <summary>
        /// The branch a new row must be written to. A restricted user always writes to their own
        /// branch whatever they sent. A company-wide role on a branch-aware (Medium+) tenant must
        /// name one, since there is no sensible "all branches" to create a booking or a purchase
        /// order against. Below Medium tier there is no branch picker to have chosen from at all -
        /// this transparently resolves the tenant's one Main Branch instead, mirroring
        /// BranchFilter.ResolveForWrite's rules on the WinForms side exactly.
        /// </summary>
        public int ResolveForWrite(TenantErpDbContext db, int? requestedBranchId)
        {
            if (RestrictedBranchId is { } restricted)
            {
                return restricted;
            }

            if (requestedBranchId is { } requested && requested > 0)
            {
                return requested;
            }

            if (!IsBranchAware)
            {
                var mainBranchId = db.Branches.Where(b => b.IsMainBranch).Select(b => (int?)b.BranchId).FirstOrDefault();
                if (mainBranchId is { } resolved)
                {
                    return resolved;
                }
            }

            throw new InvalidOperationException("Select a branch for this record.");
        }

        /// <summary>
        /// Guards any access to an existing row - read, edit or delete. A restricted user may only
        /// touch rows in their own branch. Call after fetching anything by id, or a branch-bound
        /// user could reach another branch's record just by guessing its id.
        /// </summary>
        public void EnsureCanAccess(int branchId)
        {
            if (RestrictedBranchId is { } restricted && restricted != branchId)
            {
                throw new UnauthorizedAccessException("This record belongs to another branch.");
            }
        }
    }
}
