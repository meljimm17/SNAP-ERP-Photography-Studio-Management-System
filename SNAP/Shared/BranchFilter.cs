using System.Linq;
using SNAP.Controls;
using SNAP.infrastructure.Data;

namespace SNAP
{
    /// <summary>
    /// Shared rules for the branch picker that appears on every branch-aware screen, so the same
    /// three questions aren't answered slightly differently in six forms.
    ///
    /// A caveat worth knowing: for screens that still reach the database directly (Equipment,
    /// Finance, the Booking dialog), this filtering is presentation only. The API enforces branch
    /// isolation via BranchScope for the modules that go through it; these screens hold a
    /// connection string instead, so their filter keeps the UI honest rather than making the data
    /// unreachable. Moving them onto the API is what would close that gap.
    /// </summary>
    public static class BranchFilter
    {
        public const string AllBranchesLabel = "All Branches";

        /// <summary>
        /// A picker is only worth showing when the company has more than one branch AND the signed-in
        /// user isn't pinned to one. A branch-bound user has no choice to offer.
        /// </summary>
        public static bool ShowPicker => AppSession.IsBranchAware && AppSession.CurrentBranchId is null;

        /// <summary>
        /// The branch a screen should display. A branch-bound user always gets their own, whatever
        /// the picker says; everyone else gets their selection, where null means every branch.
        /// </summary>
        public static int? Resolve(int? selectedBranchId) =>
            AppSession.CurrentBranchId ?? selectedBranchId;

        /// <summary>
        /// Fills a combo with "All Branches" plus each active branch and selects the first entry.
        /// Hides the control entirely when there's no choice to make.
        /// </summary>
        public static void Populate(RoundedComboBox combo)
        {
            combo.Visible = ShowPicker;
            if (!ShowPicker) return;

            combo.Items.Clear();
            combo.Items.Add(AllBranchesLabel);
            foreach (var branch in new BranchPresenter().GetActiveBranches())
            {
                combo.Items.Add(new BranchFilterItem(branch.BranchId, branch.BranchName));
            }
            combo.SelectedIndex = 0;
        }

        /// <summary>The branch id currently chosen in a picker, or null for "All Branches".</summary>
        public static int? SelectedId(RoundedComboBox combo) =>
            combo.SelectedItem is BranchFilterItem item ? item.Id : null;

        /// <summary>
        /// The branch a NEW row being saved on one of these still-direct-DB screens should belong
        /// to. Mirrors BranchScope.ResolveForWrite's server-side rules exactly, since these screens
        /// don't go through the API: a branch-bound user always writes to their own branch; a
        /// company-wide role on a branch-aware (Medium) tenant must have picked one specific branch
        /// (returns null, not a bogus placeholder id, when they're looking at "All Branches"
        /// instead - callers must treat a null result as "ask the user to pick a branch" rather
        /// than saving anyway); below Medium tier there's no picker to have chosen from at all, so
        /// this transparently resolves the tenant's one Main Branch instead.
        ///
        /// A caller that saves whatever this returns without checking for null risks writing the
        /// literal value 0 into a required BranchId column - not a missing value SQL Server would
        /// reject outright, but a foreign key pointing at a branch that doesn't exist, which fails
        /// exactly the same way and is just as broken.
        /// </summary>
        public static int? ResolveForWrite(TenantErpDbContext db, int? selectedBranchId)
        {
            if (AppSession.CurrentBranchId is { } pinned) return pinned;
            if (AppSession.IsBranchAware) return selectedBranchId;
            return db.Branches.Where(b => b.IsMainBranch).Select(b => (int?)b.BranchId).FirstOrDefault();
        }
    }
}
