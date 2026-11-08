using System.Windows.Forms;
using SNAP.domain.enums;

namespace SNAP
{
    /// <summary>
    /// Single "System Administration" sidebar entry hosting up to three tabs:
    ///   User Management: add/edit/deactivate TenantUsers - Admin/SuperAdmin only.
    ///   Branches:        Medium tier only. A full editor for Admin/SuperAdmin, a read-only
    ///                    report for BranchManager (they run one location, not the company's
    ///                    branch list - see PermissionService's remarks on BranchManagement).
    ///   Data Sync:       Admin/SuperAdmin only (same gate as User Management) - check/force this
    ///                    tenant's local-first-write outbox flush to MonsterASP (see
    ///                    TenantSyncService and DataSyncForm).
    ///
    /// Each tab is only added when the caller actually has some access to it, so this collapses
    /// naturally per role: Admin/SuperAdmin get all three tabs, BranchManager gets just the
    /// Branches report, and a sub-Medium tenant never has a Branches tab to show regardless of role.
    ///
    /// This control owns the one page header for the whole screen; UserManagementForm and
    /// BranchForm no longer draw their own, so nesting them here doesn't double up the title.
    /// </summary>
    public class SystemAdministrationForm : UserControl
    {
        public SystemAdministrationForm(AccessLevel userManagementLevel, AccessLevel branchLevel, bool showBranchTab)
        {
            Dock = DockStyle.Fill;
            BackColor = AppTheme.BackgroundColor;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            AppTheme.StyleTabControl(tabs);

            var hasUserManagement = userManagementLevel != AccessLevel.None;
            var hasBranches = showBranchTab && branchLevel != AccessLevel.None;
            // Data Sync is an Admin-only operational concern, same gate as User Management rather
            // than its own module - a BranchManager running one location has no reason to care
            // about the company's whole MonsterASP backup.
            var hasDataSync = userManagementLevel == AccessLevel.Full;

            if (hasUserManagement)
            {
                var userTab = new TabPage("User Management") { BackColor = AppTheme.WhiteColor };
                userTab.Controls.Add(new UserManagementForm(userManagementLevel) { Dock = DockStyle.Fill });
                tabs.TabPages.Add(userTab);
            }

            if (hasBranches)
            {
                var branchTab = new TabPage("Branches") { BackColor = AppTheme.WhiteColor };
                branchTab.Controls.Add(new BranchForm(branchLevel) { Dock = DockStyle.Fill });
                tabs.TabPages.Add(branchTab);
            }

            if (hasDataSync)
            {
                var syncTab = new TabPage("Data Sync") { BackColor = AppTheme.WhiteColor };
                syncTab.Controls.Add(new DataSyncForm { Dock = DockStyle.Fill });
                tabs.TabPages.Add(syncTab);
            }

            var subtitle = hasUserManagement && hasBranches
                ? "Manage user accounts and studio branches."
                : hasBranches
                    ? "Studio branches for this company."
                    : "Manage user accounts.";

            var header = AppTheme.CreatePageHeader("System Administration", subtitle);
            Controls.Add(AppTheme.BuildPage(header, null, AppTheme.WrapContent(tabs)));
        }
    }
}
