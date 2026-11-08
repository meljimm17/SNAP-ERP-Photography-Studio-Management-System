using System.Drawing;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.Controls;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP
{
    public class MainDashboardForm : Form
    {
        private readonly Panel _contentHost;
        private Control? _activeModuleControl;
        private SidebarNavItem? _activeSidebarItem;
        private readonly PermissionService _permissions = new();
        private readonly SystemRole _role = AppSession.CurrentRole;
        private readonly FlowLayoutPanel _sidebar;

        // Every module that made it into the sidebar (tier- and permission-visible), so the
        // Dashboard's KPI cards/charts can open one exactly as clicking its sidebar item would.
        private readonly Dictionary<SystemModule, (SidebarGroupHeader Header, SidebarNavItem Item, Action Open)> _navTargets = new();

        public MainDashboardForm()
        {
            Text = "SNAP ERP";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = AppTheme.BackgroundColor;
            Width = 1280;
            Height = 800;
            FormBorderStyle = FormBorderStyle.Sizable;

            _sidebar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.NavyColor,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                Padding = new Padding(15)
            };

            var title = new Label
            {
                Text = "SNAP",
                ForeColor = AppTheme.WhiteColor,
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                AutoSize = false,
                Width = 210,
                Height = 50,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(4, 0, 0, 16)
            };
            _sidebar.Controls.Add(title);

            var dashboardItem = new SidebarNavItem { Text = "Dashboard", Width = 210, Margin = new Padding(0, 0, 0, 4) };
            dashboardItem.Click += (_, _) => { SetActiveSidebarItem(dashboardItem); ShowHome(); };
            _sidebar.Controls.Add(dashboardItem);
            _activeSidebarItem = dashboardItem;
            dashboardItem.IsActive = true;

            AddGroup("Operations",
                (SystemModule.CustomerManagement, "Customers", (Func<AccessLevel, Control>)(level => new CustomerForm(level)), true),
                (SystemModule.PackageManagement, "Photography Packages", level => new PackageForm(level), true),
                (SystemModule.BookingScheduling, "Bookings & Scheduling", level => new BookingForm(level), true),
                (SystemModule.PaymentManagement, "Payments", level => new PaymentForm(level), true));

            // Equipment/Inventory/Finance are Small-tier-and-up additions - Micro companies keep
            // exactly the module set they always had, regardless of the logged-in role's permissions.
            var aboveMicroTier = AppSession.CurrentPlanTier != PlanTier.Micro;

            AddGroup("Resources",
                (SystemModule.InventoryManagement, "Inventory", (Func<AccessLevel, Control>)(level => new InventoryForm(level)), aboveMicroTier),
                (SystemModule.EquipmentManagement, "Equipment", level => new EquipmentForm(level), aboveMicroTier));

            AddGroup("Finance",
                (SystemModule.FinanceManagement, "Finance", (Func<AccessLevel, Control>)(level => new FinanceForm(level)), aboveMicroTier),
                (SystemModule.ReportsManagement, "Reports & Analytics", level => new ReportsForm(level), true));

            // Branch Management is the one module Medium tier adds outright. Below Medium a company
            // conceptually has a single branch, so the item never appears - and BranchController
            // carries [RequirePlanTier(PlanTier.Medium)] so hiding it here isn't the only defence.
            var mediumTier = AppSession.CurrentPlanTier >= PlanTier.Medium;

            // One combined sidebar entry rather than two - SystemAdministration is a pseudo-module
            // PermissionService resolves as the max of UserManagement/BranchManagement, so the
            // item shows for anyone who can reach at least one of the two tabs it now hosts (see
            // SystemAdministrationForm). Each tab still resolves its own module's real access
            // level here, so a BranchManager's Branches tab comes in as View (report-only) even
            // though the combined item itself is visible.
            AddGroup("Administration",
                (SystemModule.SystemAdministration, "System Administration",
                    (Func<AccessLevel, Control>)(_ => new SystemAdministrationForm(
                        _permissions.GetAccessLevel(_role, SystemModule.UserManagement),
                        _permissions.GetAccessLevel(_role, SystemModule.BranchManagement),
                        mediumTier)),
                    true),
                (SystemModule.SubscriptionManagement, "Subscription", level => new SubscriptionForm(level), true));

            // Logout sits in its own pinned row below the scrollable/collapsible nav area, so it
            // stays visible no matter how many groups are expanded or how far the nav list scrolls.
            var logoutBar = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.NavyDarkColor, Padding = new Padding(15, 8, 15, 8) };
            var logoutItem = new SidebarNavItem { Text = "Logout", Width = 210, Dock = DockStyle.Fill };
            logoutItem.Click += (_, _) => Logout();
            logoutBar.Controls.Add(logoutItem);

            var sidebarContainer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = AppTheme.NavyColor };
            sidebarContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sidebarContainer.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            sidebarContainer.Controls.Add(_sidebar, 0, 0);
            sidebarContainer.Controls.Add(logoutBar, 0, 1);

            // Main content area
            var content = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.BackgroundColor };

            var topBar = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = AppTheme.BackgroundColor, Padding = new Padding(24, 0, 24, 0) };

            var welcomeLabel = new Label
            {
                Text = $"Welcome back, {AppSession.CurrentDisplayName ?? AppSession.CurrentUsername}",
                Font = AppTheme.SecondaryFont,
                ForeColor = AppTheme.TextSecondaryColor,
                AutoSize = false,
                Dock = DockStyle.Right,
                Width = 320,
                TextAlign = ContentAlignment.MiddleRight
            };
            topBar.Controls.Add(welcomeLabel);

            _contentHost = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.BackgroundColor };

            // A plain Dock.Top + Dock.Fill sibling pair does not reserve space correctly on this
            // WinForms runtime - the Fill control ends up spanning the full parent area, painted
            // over by the Top control instead of being pushed below it (confirmed with an isolated
            // repro). TableLayoutPanel's row-based layout is unaffected, so it's used here instead
            // of raw Dock stacking.
            var contentLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            contentLayout.Controls.Add(topBar, 0, 0);
            contentLayout.Controls.Add(_contentHost, 0, 1);
            content.Controls.Add(contentLayout);

            // Same issue applies to Dock.Left + Dock.Fill (sidebar/content), so this also uses a
            // TableLayoutPanel instead of raw docking.
            var rootLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            rootLayout.Controls.Add(sidebarContainer, 0, 0);
            rootLayout.Controls.Add(content, 1, 0);
            Controls.Add(rootLayout);

            // Rows saved while offline go up to MonsterASP on their own once the connection is
            // back (see AutoSyncService). Stopped on close, which covers both Logout and exit.
            AutoSyncService.Start(AppSession.CurrentTenantConnectionString);
            FormClosed += (_, _) => AutoSyncService.Stop();

            ShowHome();
        }

        /// <summary>
        /// Clicks made while login/first load had the window frozen would otherwise land on the
        /// Dashboard's cards the instant it appears and open Payments - see InputQueue.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            InputQueue.DiscardPendingUserInput();
            base.OnShown(e);
        }

        /// <summary>
        /// Adds a collapsible sidebar section (OPERATIONS, RESOURCES, ...) followed by its nav
        /// items, but only if at least one item in the group is actually visible - a group whose
        /// only items are permission-hidden or tier-hidden gets no orphaned header. Starts
        /// collapsed: with every module module in the sidebar, showing all of them expanded by
        /// default is exactly the cramped, everything-at-once list this replaces.
        /// </summary>
        private void AddGroup(string label, params (SystemModule Module, string Text, Func<AccessLevel, Control> Factory, bool TierAllowed)[] items)
        {
            var visible = new System.Collections.Generic.List<(SystemModule Module, string Text, AccessLevel Level, Func<AccessLevel, Control> Factory)>();
            foreach (var item in items)
            {
                if (!item.TierAllowed) continue;
                var level = _permissions.GetAccessLevel(_role, item.Module);
                if (level == AccessLevel.None) continue;
                visible.Add((item.Module, item.Text, level, item.Factory));
            }

            if (visible.Count == 0) return;

            var groupHeader = new SidebarGroupHeader { Text = label, Width = 210, Margin = new Padding(4, 10, 0, 4) };
            _sidebar.Controls.Add(groupHeader);

            var navItems = new System.Collections.Generic.List<SidebarNavItem>();
            foreach (var (module, text, level, factory) in visible)
            {
                var navItem = new SidebarNavItem { Text = text, Width = 210, Margin = new Padding(0, 0, 0, 4), Visible = false };
                void Open() { SetActiveSidebarItem(navItem); ShowModule(() => factory(level)); }
                navItem.Click += (_, _) => Open();
                _sidebar.Controls.Add(navItem);
                navItems.Add(navItem);
                _navTargets[module] = (groupHeader, navItem, Open);
            }

            // FlowLayoutPanel skips invisible children when flowing, so toggling each item's
            // Visible flag reflows the rest of the sidebar automatically - no manual repositioning.
            groupHeader.ExpandedChanged += (_, _) =>
            {
                foreach (var item in navItems)
                {
                    item.Visible = groupHeader.Expanded;
                }
            };
        }

        /// <summary>
        /// Swaps the content area back to the dashboard stat cards (no separate window).
        /// </summary>
        private void ShowHome()
        {
            ClearContentHost();

            // Same KPIs and charts as Reports & Analytics, for the current month - each one opens
            // the module its numbers live in (see DashboardOverview).
            var overview = new DashboardOverview(module => _navTargets.ContainsKey(module), NavigateTo);

            // "All Branches" gives company-wide numbers; picking one narrows every card to it.
            // Only built when there's a choice to offer - a branch-bound user is pinned to their
            // own branch by AppSession.CurrentBranchId and a sub-Medium tenant has no branches.
            FlowLayoutPanel? toolbar = null;
            if (BranchFilter.ShowPicker)
            {
                var branchPicker = new RoundedComboBox { Width = 170, Margin = new Padding(0, 2, 0, 0) };
                BranchFilter.Populate(branchPicker);
                branchPicker.SelectedIndexChanged += (_, _) =>
                    overview.Render(BranchFilter.Resolve(BranchFilter.SelectedId(branchPicker)));

                toolbar = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    Padding = new Padding(0, 12, 0, 12),
                    FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false,
                    BackColor = AppTheme.BackgroundColor
                };
                toolbar.Controls.Add(branchPicker);
            }

            // Opens on "All Branches" for a company-wide role, or the user's own branch otherwise.
            overview.Render(AppSession.CurrentBranchId);

            var (monthStart, _) = DashboardOverview.CurrentMonth();
            var header = AppTheme.CreatePageHeader("Dashboard",
                $"{monthStart:MMMM yyyy} at a glance - click any card or chart to open its records.");
            _contentHost.Controls.Add(AppTheme.BuildPage(header, toolbar, overview));
            RefreshLayout();
        }

        /// <summary>
        /// Opens a module exactly as clicking its sidebar item would - expanding its group if
        /// collapsed so the highlighted item is actually visible. A module that isn't in the
        /// sidebar (tier- or permission-hidden) is ignored; the Dashboard never offers one anyway.
        /// </summary>
        private void NavigateTo(SystemModule module)
        {
            if (!_navTargets.TryGetValue(module, out var target)) return;

            if (!target.Header.Expanded) target.Header.Expanded = true;
            target.Open();
        }

        /// <summary>
        /// Embeds a module's UserControl directly into the dashboard's content area instead of
        /// opening it as a separate dialog window. Modules are plain UserControls (not Forms), which
        /// dock natively into a host Panel with none of the window-handle/StartPosition quirks that
        /// come from faking a Form into a child control.
        /// </summary>
        private void ShowModule(Func<Control> factory)
        {
            ClearContentHost();

            var moduleControl = factory();
            moduleControl.Dock = DockStyle.Fill;
            _activeModuleControl = moduleControl;

            _contentHost.Controls.Add(moduleControl);
            RefreshLayout();
        }

        private void ClearContentHost()
        {
            _contentHost.Controls.Clear();
            _activeModuleControl?.Dispose();
            _activeModuleControl = null;
        }

        /// <summary>
        /// Forces a full top-down layout + repaint after swapping the content area at runtime.
        /// Without this, the sidebar/content docking can render with stale, overlapping bounds
        /// since Controls.Clear()/Add() happen after the form is already shown and laid out.
        /// </summary>
        private void RefreshLayout()
        {
            PerformLayout();
            Invalidate(true);
        }

        private void Logout()
        {
            AppSession.CurrentCompanyCode = null;
            AppSession.CurrentCompanyId = 0;
            AppSession.CurrentUsername = null;
            AppSession.CurrentDisplayName = null;
            AppSession.CurrentRole = default;
            AppSession.CurrentTenantConnectionString = null;
            AppSession.CurrentUserId = 0;
            AppSession.CurrentApiToken = null;

            TenantSyncService.CurrentActorUserId = null;
            TenantSyncService.CurrentActorName = "Unknown";

            var login = new LoginForm();
            login.Show();
            Close();
        }

        /// <summary>
        /// Tracks which sidebar item is "on" via SidebarNavItem's own active-state paint,
        /// restoring the previously active item first.
        /// </summary>
        private void SetActiveSidebarItem(SidebarNavItem item)
        {
            if (_activeSidebarItem is not null)
            {
                _activeSidebarItem.IsActive = false;
            }

            item.IsActive = true;
            _activeSidebarItem = item;
        }
    }
}
