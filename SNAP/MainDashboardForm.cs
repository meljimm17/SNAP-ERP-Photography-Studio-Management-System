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

            AddGroup("Administration",
                (SystemModule.UserManagement, "System Administration", (Func<AccessLevel, Control>)(level => new UserManagementForm(level)), true));

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

            ShowHome();
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
            var visible = new System.Collections.Generic.List<(string Text, AccessLevel Level, Func<AccessLevel, Control> Factory)>();
            foreach (var item in items)
            {
                if (!item.TierAllowed) continue;
                var level = _permissions.GetAccessLevel(_role, item.Module);
                if (level == AccessLevel.None) continue;
                visible.Add((item.Text, level, item.Factory));
            }

            if (visible.Count == 0) return;

            var groupHeader = new SidebarGroupHeader { Text = label, Width = 210, Margin = new Padding(4, 10, 0, 4) };
            _sidebar.Controls.Add(groupHeader);

            var navItems = new System.Collections.Generic.List<SidebarNavItem>();
            foreach (var (text, level, factory) in visible)
            {
                var navItem = new SidebarNavItem { Text = text, Width = 210, Margin = new Padding(0, 0, 0, 4), Visible = false };
                navItem.Click += (_, _) => { SetActiveSidebarItem(navItem); ShowModule(() => factory(level)); };
                _sidebar.Controls.Add(navItem);
                navItems.Add(navItem);
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

            var cardsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                BackColor = AppTheme.BackgroundColor,
                Padding = new Padding(0, 8, 0, 0)
            };

            var stats = LoadDashboardStats();
            cardsPanel.Controls.Add(CreateStatCard("Upcoming Sessions", stats.UpcomingSessions));
            cardsPanel.Controls.Add(CreateStatCard("Bookings this month", stats.BookingsThisMonth));
            cardsPanel.Controls.Add(CreateStatCard("Revenue this month", stats.RevenueThisMonth));
            cardsPanel.Controls.Add(CreateStatCard("Pending payments", stats.PendingPayments));

            var header = AppTheme.CreatePageHeader("Dashboard", "Overview of today's bookings, revenue, and pending items.");
            _contentHost.Controls.Add(AppTheme.BuildPage(header, null, cardsPanel));
            RefreshLayout();
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

            TenantSyncService.CurrentActorUserId = null;
            TenantSyncService.CurrentActorName = "Unknown";

            var login = new LoginForm();
            login.Show();
            Close();
        }

        private sealed record DashboardStats(string UpcomingSessions, string BookingsThisMonth, string RevenueThisMonth, string PendingPayments);

        private static DashboardStats LoadDashboardStats()
        {
            const string na = "—";
            try
            {
                var connectionString = AppSession.CurrentTenantConnectionString ?? AppDbConfig.TenantErpConnectionString;
                var options = new DbContextOptionsBuilder<TenantErpDbContext>()
                    .UseSqlServer(connectionString)
                    .Options;

                using var db = new TenantErpDbContext(options);

                var today = DateTime.Today;
                var monthStart = new DateTime(today.Year, today.Month, 1);
                var monthEnd = monthStart.AddMonths(1);

                var upcomingSessions = db.Bookings.Count(b =>
                    b.SessionDate.Date >= today && b.Status != BookingStatus.Cancelled);

                var bookingsThisMonth = db.Bookings.Count(b =>
                    b.SessionDate >= monthStart && b.SessionDate < monthEnd && b.Status != BookingStatus.Cancelled);

                // Payments + Product Sales, so this never silently undercounts real revenue now
                // that Product Sales exist as a second revenue stream (matches Reports' Total
                // Income stat card, which was updated for the same reason).
                var paymentRevenue = db.Payments
                    .Where(p => !p.IsDeleted && p.PaymentDate >= monthStart && p.PaymentDate < monthEnd)
                    .Sum(p => (decimal?)p.AmountPaid) ?? 0m;
                var productSaleRevenue = db.ProductSales
                    .Where(p => !p.IsDeleted && p.SaleDate >= monthStart && p.SaleDate < monthEnd)
                    .Sum(p => (decimal?)p.TotalAmount) ?? 0m;
                var revenueThisMonth = paymentRevenue + productSaleRevenue;

                var totalsByBooking = db.Payments
                    .GroupBy(p => p.BookingId)
                    .Select(g => new { BookingId = g.Key, Total = g.Sum(p => p.AmountPaid) })
                    .ToDictionary(x => x.BookingId, x => x.Total);

                var pendingPayments = db.Bookings
                    .Where(b => b.Status != BookingStatus.Cancelled)
                    .AsEnumerable()
                    .Count(b => b.PriceSnapshot - totalsByBooking.GetValueOrDefault(b.BookingId) > 0);

                return new DashboardStats(
                    upcomingSessions.ToString(),
                    bookingsThisMonth.ToString(),
                    $"₱{revenueThisMonth:N0}",
                    pendingPayments.ToString());
            }
            catch
            {
                return new DashboardStats(na, na, na, na);
            }
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

        private static RoundedPanel CreateStatCard(string title, string value)
        {
            var card = new RoundedPanel
            {
                Width = 220,
                Height = 110,
                BackColor = AppTheme.WhiteColor,
                Padding = new Padding(18),
                Margin = new Padding(0, 0, 16, 16)
            };

            var labelTitle = new Label
            {
                Text = title,
                ForeColor = AppTheme.TextSecondaryColor,
                Font = AppTheme.SecondaryFont,
                AutoSize = false,
                Width = 184,
                Height = 24,
                Location = new Point(18, 18)
            };

            var valueLabel = new Label
            {
                Text = value,
                ForeColor = AppTheme.TextPrimaryColor,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                AutoSize = false,
                Width = 184,
                Height = 56,
                Location = new Point(18, 46),
                TextAlign = ContentAlignment.TopLeft
            };

            card.Controls.Add(labelTitle);
            card.Controls.Add(valueLabel);
            return card;
        }
    }
}
