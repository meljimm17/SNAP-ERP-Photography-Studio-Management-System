using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.config;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP
{
    /// <summary>
    /// Master Admin Panel - the three items the exam slide lists under Master (Super Admin),
    /// as one tabbed screen:
    ///   Admin Panel:          provision/edit companies and their tenant databases.
    ///   Business Intelligence: cross-tenant charts (plan tier mix, active vs. suspended, growth).
    ///   Subscriptions:        view billing state, change plan tier and renewal dates,
    ///                         suspend/reactivate accounts.
    ///
    /// All writes go against MasterErpDbContext; a best-effort audit entry is also
    /// written to the affected company's tenant database (see WriteCompanyAuditLog).
    /// </summary>
    public class SystemAdminCompanyForm : Form
    {
        // ─── Companies tab ───────────────────────────────────────────────────────
        private readonly DataGridView _companyGrid = new();
        private readonly RoundedButton _add = new();
        private readonly RoundedButton _edit = new() { IsPrimary = false };
        private readonly RoundedButton _toggle = new() { IsPrimary = false };

        // ─── Subscriptions tab ───────────────────────────────────────────────────
        private readonly DataGridView _subGrid = new();
        private readonly RoundedButton _updateSub = new() { IsPrimary = false };
        private readonly RoundedButton _suspend = new() { IsPrimary = false };
        private readonly RoundedButton _reactivate = new() { IsPrimary = false };

        // ─── Business Intelligence tab ───────────────────────────────────────────
        private readonly ChartPanel _planTierChart = new() { Title = "Companies by Plan Tier", Kind = ChartKind.Bar, UseCategoricalColors = true, ValueFormat = "N0" };
        private readonly ChartPanel _statusChart = new() { Title = "Companies by Subscription Status", Kind = ChartKind.Pie, UseCategoricalColors = true, ValueFormat = "N0" };
        private readonly ChartPanel _growthChart = new() { Title = "Company Growth Over Time", Kind = ChartKind.Line, ValueFormat = "N0" };

        private readonly RoundedButton _logout = new() { Text = "Logout", IsPrimary = false };

        public SystemAdminCompanyForm()
        {
            Text = "SNAP ERP - System Administration";
            Width = 1100;
            Height = 660;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = AppTheme.BackgroundColor;

            // This form has no owner (opened via Show(), not ShowDialog) and isn't built through
            // AppTheme.BuildDialogLayout, so it never got the same off-screen/taskbar-overlap
            // protection every other dialog has - nothing clamped it to the working area before.
            Load += (_, _) => AppTheme.ClampDialogToScreen(this);
            Shown += (_, _) => AppTheme.ClampDialogToScreen(this);

            var tabs = new TabControl { Dock = DockStyle.Fill };
            AppTheme.StyleTabControl(tabs);
            // Order matches the exam slide's own listing under Master (Super Admin): Admin Panel,
            // Business Intelligence, Subscriptions.
            tabs.TabPages.Add(BuildCompaniesTab());
            tabs.TabPages.Add(BuildBusinessIntelligenceTab());
            tabs.TabPages.Add(BuildSubscriptionsTab());

            _logout.Width = 110;
            _logout.Height = 36;
            _logout.Click += (_, _) => Logout();

            var header = AppTheme.CreatePageHeader(
                "System Administration",
                "Manage companies, tenant databases, and subscription plans.",
                _logout);
            Controls.Add(AppTheme.BuildPage(header, null, AppTheme.WrapContent(tabs)));

            LoadCompanies();
            LoadSubscriptions();
            LoadAnalytics();
        }

        /// <summary>
        /// Mirrors MainDashboardForm.Logout exactly - this screen is SuperAdmin's own shell (no
        /// tenant dashboard sits above it), so it needs the same session-clearing exit rather than
        /// relying on one being reachable elsewhere.
        /// </summary>
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

        // ═══════════════════════════════════════════════════════════════════════
        // Companies tab
        // ═══════════════════════════════════════════════════════════════════════

        private TabPage BuildCompaniesTab()
        {
            // Named "Admin Panel" to match the exam slide's wording exactly - this is the same
            // company-provisioning screen that was always here, not a new one.
            var page = new TabPage("Admin Panel") { BackColor = AppTheme.WhiteColor };

            AppTheme.StyleGrid(_companyGrid);
            _companyGrid.Dock = DockStyle.Fill;
            _companyGrid.ReadOnly = true;
            _companyGrid.AutoGenerateColumns = true;
            _companyGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _companyGrid.MultiSelect = false;
            AppTheme.StyleStatusColumn(_companyGrid, "IsActive", value =>
                value is true ? ("Active", StatusKind.Success) : ("Inactive", StatusKind.Neutral));

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.WhiteColor
            };

            _add.Text = "+ Add Company";
            _edit.Text = "Edit";
            _toggle.Text = "Toggle Active";
            _add.Width = 140;
            _edit.Width = 90;
            _toggle.Width = 120;
            _edit.Margin = new Padding(0, 0, 8, 0);

            _add.Click += (_, _) => AddCompany();
            _edit.Click += (_, _) => EditCompany();
            _toggle.Click += (_, _) => ToggleCompany();

            toolbar.Controls.Add(_edit);
            toolbar.Controls.Add(_toggle);

            var header = AppTheme.CreatePageHeader(
                "Company Management", "Provision companies and their tenant databases.", _add);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, AppTheme.ToolbarRowHeight));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(_companyGrid, 0, 1);

            // AppTheme.BuildPage needs a container; nest it in the tab page directly.
            page.Controls.Add(AppTheme.BuildPage(header, toolbar, AppTheme.WrapContent(_companyGrid)));
            return page;
        }

        private void LoadCompanies()
        {
            using var db = CreateDbContext();
            _companyGrid.DataSource = db.Companies
                .OrderByDescending(x => x.CreatedAt)
                .ThenBy(x => x.CompanyName)
                .Select(c => new CompanyRow
                {
                    CompanyId = c.CompanyId,
                    CompanyCode = c.CompanyCode,
                    CompanyName = c.CompanyName,
                    PlanTier = c.PlanTier.ToString(),
                    IsActive = c.IsActive
                })
                .ToList();
        }

        private void AddCompany()
        {
            try
            {
                AddCompanyCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not add this company:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddCompanyCore()
        {
            using var dialog = new CompanyDialogForm(existingCompany: null, existingDatabase: null);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            using var db = CreateDbContext();
            if (db.Companies.Any(x => x.CompanyCode == dialog.CompanyCode))
            {
                MessageBox.Show("That Company Code is already in use.", "Add Company",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var company = new Company();
            var companyDatabase = new CompanyDatabase();

            // CreateExecutionStrategy().Execute(...) rather than a bare BeginTransaction - EF Core
            // forbids combining a retrying execution strategy (CreateDbContext enables one) with a
            // user-managed transaction, so a bare transaction throws the moment this path actually
            // runs. Both rows saved as one retriable unit: a Company that saved with no
            // CompanyDatabase row is one that can never be logged into.
            var strategy = db.Database.CreateExecutionStrategy();
            strategy.Execute(() =>
            {
                using var transaction = db.Database.BeginTransaction();

                company.CompanyCode = dialog.CompanyCode;
                company.CompanyName = dialog.CompanyDisplayName;
                company.PlanTier = dialog.PlanTier;
                company.IsActive = true;
                company.CreatedAt = DateTime.UtcNow;
                db.Companies.Add(company);
                db.SaveChanges();

                companyDatabase.CompanyId = company.CompanyId;
                companyDatabase.ServerName = dialog.ServerName;
                companyDatabase.DatabaseName = dialog.DatabaseName;
                companyDatabase.RemoteServerName = dialog.RemoteServerName;
                companyDatabase.RemoteDatabaseName = dialog.RemoteDatabaseName;
                companyDatabase.RemoteUsername = dialog.RemoteUsername;
                companyDatabase.RemotePassword = dialog.RemotePassword;
                companyDatabase.IsActive = true;
                db.CompanyDatabases.Add(companyDatabase);
                db.SaveChanges();

                transaction.Commit();
            });

            WriteCompanyAuditLog(company, companyDatabase, AuditAction.Create,
                $"Created company: {company.CompanyName} ({company.PlanTier} tier)");

            var adminResult = AuthenticationService.ProvisionAdminUser(companyDatabase, dialog.AdminUsername, dialog.AdminPassword);
            var remoteResult = AuthenticationService.ProvisionRemoteDatabase(companyDatabase);

            LoadCompanies();
            LoadSubscriptions();

            var problems = new List<string>();
            if (!adminResult.Success)
                problems.Add($"Admin account could not be created yet:\n{adminResult.Error}");
            if (!remoteResult.Success)
                problems.Add($"Remote (MonsterASP) database could not be set up yet:\n{remoteResult.Error}");

            if (problems.Count == 0)
            {
                var remoteNote = companyDatabase.RemoteServerName is null
                    ? string.Empty
                    : "\n\nIts MonsterASP database has also been set up and is ready to sync.";
                MessageBox.Show(
                    $"Company added successfully.\n\nFirst login for {company.CompanyName}:\n" +
                    $"Company Code: {company.CompanyCode}\nUsername: {dialog.AdminUsername}\nPassword: {dialog.AdminPassword}\n\n" +
                    $"Share these with the company - they should change the password after signing in.{remoteNote}",
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Company added, but not everything could be set up yet:\n\n{string.Join("\n\n", problems)}\n\n" +
                    "The company record and database link were still saved - fix the issue(s) above (e.g. a " +
                    "database isn't reachable yet). Re-saving via Edit Company retries the remote database setup " +
                    "automatically; if the admin account specifically failed, provision it directly once that " +
                    "tenant database is reachable.",
                    "Company Added - Setup Incomplete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void EditCompany()
        {
            try
            {
                EditCompanyCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not update this company:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EditCompanyCore()
        {
            if (_companyGrid.SelectedRows.Count == 0) return;

            var row = (CompanyRow)_companyGrid.SelectedRows[0].DataBoundItem;
            using var db = CreateDbContext();
            var entity = db.Companies.Find(row.CompanyId);
            if (entity is null) return;

            var companyDatabase = db.CompanyDatabases.FirstOrDefault(
                x => x.CompanyId == entity.CompanyId && x.IsActive);

            using var dialog = new CompanyDialogForm(entity, companyDatabase);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            if (db.Companies.Any(x => x.CompanyCode == dialog.CompanyCode && x.CompanyId != entity.CompanyId))
            {
                MessageBox.Show("That Company Code is already in use.", "Edit Company",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var previousTier = entity.PlanTier;
            entity.CompanyCode = dialog.CompanyCode;
            entity.CompanyName = dialog.CompanyDisplayName;
            entity.PlanTier = dialog.PlanTier;

            if (companyDatabase is not null)
            {
                companyDatabase.ServerName = dialog.ServerName;
                companyDatabase.DatabaseName = dialog.DatabaseName;
                companyDatabase.RemoteServerName = dialog.RemoteServerName;
                companyDatabase.RemoteDatabaseName = dialog.RemoteDatabaseName;
                companyDatabase.RemoteUsername = dialog.RemoteUsername;
                companyDatabase.RemotePassword = dialog.RemotePassword;
            }
            else
            {
                companyDatabase = new CompanyDatabase
                {
                    CompanyId = entity.CompanyId,
                    ServerName = dialog.ServerName,
                    DatabaseName = dialog.DatabaseName,
                    RemoteServerName = dialog.RemoteServerName,
                    RemoteDatabaseName = dialog.RemoteDatabaseName,
                    RemoteUsername = dialog.RemoteUsername,
                    RemotePassword = dialog.RemotePassword,
                    IsActive = true
                };
                db.CompanyDatabases.Add(companyDatabase);
            }

            db.SaveChanges();

            var summary = previousTier == entity.PlanTier
                ? $"Updated company: {entity.CompanyName}"
                : $"Updated company: {entity.CompanyName} (plan tier: {previousTier} → {entity.PlanTier})";
            WriteCompanyAuditLog(entity, companyDatabase, AuditAction.Update, summary);

            // Covers the "added Remote fields after the fact" case (e.g. the company was created
            // local-only, then a MonsterASP database was provisioned and wired up later via this
            // same Edit screen) - without this, a brand new remote database just sits there with
            // zero tables forever, since nothing else ever runs Migrate() against it. Safe to call
            // on every edit, remote-configured or not: a no-op when the four Remote fields are
            // empty, and a no-op success when the remote is already migrated.
            var remoteResult = AuthenticationService.ProvisionRemoteDatabase(companyDatabase);
            if (!remoteResult.Success)
            {
                MessageBox.Show(
                    $"Company updated, but the remote (MonsterASP) database could not be set up yet:\n\n{remoteResult.Error}\n\n" +
                    "The company's saved details are unaffected - fix the issue above (e.g. the remote server isn't " +
                    "reachable yet) and save this screen again to retry.",
                    "Company Updated - Remote Setup Incomplete", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            LoadCompanies();
            LoadSubscriptions();
        }

        private void ToggleCompany()
        {
            try
            {
                ToggleCompanyCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not change this company's status:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToggleCompanyCore()
        {
            if (_companyGrid.SelectedRows.Count == 0) return;

            var row = (CompanyRow)_companyGrid.SelectedRows[0].DataBoundItem;
            using var db = CreateDbContext();
            var entity = db.Companies.Find(row.CompanyId);
            if (entity is null) return;

            entity.IsActive = !entity.IsActive;
            db.SaveChanges();

            var companyDatabase = db.CompanyDatabases.FirstOrDefault(
                x => x.CompanyId == entity.CompanyId && x.IsActive);
            WriteCompanyAuditLog(entity, companyDatabase, AuditAction.Update,
                $"{(entity.IsActive ? "Activated" : "Deactivated")} company: {entity.CompanyName}");

            LoadCompanies();
            LoadSubscriptions();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Subscriptions tab
        // ═══════════════════════════════════════════════════════════════════════

        private TabPage BuildSubscriptionsTab()
        {
            var page = new TabPage("Subscriptions") { BackColor = AppTheme.WhiteColor };

            AppTheme.StyleGrid(_subGrid);
            _subGrid.Dock = DockStyle.Fill;
            _subGrid.ReadOnly = true;
            _subGrid.AutoGenerateColumns = true;
            _subGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _subGrid.MultiSelect = false;
            AppTheme.StyleStatusColumn(_subGrid, "Status", value =>
                (value?.ToString() ?? string.Empty) switch
                {
                    "Active" => ("Active", StatusKind.Success),
                    "Suspended" => ("Suspended", StatusKind.Danger),
                    "Trial" => ("Trial", StatusKind.Warning),
                    "Cancelled" => ("Cancelled", StatusKind.Neutral),
                    var other => (other, StatusKind.Neutral)
                });

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.WhiteColor
            };

            _updateSub.Text = "Change Plan / Renewal";
            _suspend.Text = "Suspend";
            _reactivate.Text = "Reactivate";
            _updateSub.Width = 170;
            _suspend.Width = 100;
            _reactivate.Width = 110;
            _updateSub.Margin = new Padding(0, 0, 8, 0);
            _suspend.Margin = new Padding(0, 0, 8, 0);

            _updateSub.Click += (_, _) => UpdateSubscription();
            _suspend.Click += (_, _) => SetSubscriptionStatus(SubscriptionStatus.Suspended);
            _reactivate.Click += (_, _) => SetSubscriptionStatus(SubscriptionStatus.Active);

            toolbar.Controls.Add(_updateSub);
            toolbar.Controls.Add(_suspend);
            toolbar.Controls.Add(_reactivate);

            var header = AppTheme.CreatePageHeader(
                "Subscriptions",
                "Manage plan tiers, renewal dates, and billing status for each company.");

            page.Controls.Add(AppTheme.BuildPage(header, toolbar, AppTheme.WrapContent(_subGrid)));
            return page;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Business Intelligence tab
        // ═══════════════════════════════════════════════════════════════════════

        private TabPage BuildBusinessIntelligenceTab()
        {
            var page = new TabPage("Business Intelligence") { BackColor = AppTheme.WhiteColor };

            var stack = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(16),
                BackColor = AppTheme.WhiteColor
            };

            foreach (var chart in new[] { _planTierChart, _statusChart, _growthChart })
            {
                chart.Width = 900;
                chart.Height = 260;
                chart.Margin = new Padding(0, 0, 0, 24);
                stack.Controls.Add(chart);
            }

            var header = AppTheme.CreatePageHeader(
                "Business Intelligence",
                "Cross-tenant metrics on subscription tiers, account status, and platform growth.");

            page.Controls.Add(AppTheme.BuildPage(header, null, stack));
            return page;
        }

        private void LoadAnalytics()
        {
            try
            {
                using var db = CreateDbContext();
                var companies = db.Companies.ToList();

                var byPlanTier = Enum.GetValues<PlanTier>()
                    .Select(tier => new ChartPoint(tier.ToString(), companies.Count(c => c.PlanTier == tier)))
                    .ToList();
                _planTierChart.SetData(byPlanTier);

                var byStatus = Enum.GetValues<SubscriptionStatus>()
                    .Select(status => new ChartPoint(status.ToString(), companies.Count(c => c.SubscriptionStatus == status)))
                    .ToList();
                _statusChart.SetData(byStatus);

                var growth = companies
                    .GroupBy(c => new DateTime(c.CreatedAt.Year, c.CreatedAt.Month, 1))
                    .OrderBy(g => g.Key)
                    .Select(g => new ChartPoint(g.Key.ToString("MMM yy"), g.Count()))
                    .ToList();
                _growthChart.SetData(growth);
            }
            catch
            {
                // Master database error
            }
        }

        private void LoadSubscriptions()
        {
            try
            {
                using var db = CreateDbContext();
                _subGrid.DataSource = db.Companies
                    .OrderBy(x => x.CompanyName)
                    .Select(c => new SubscriptionRow
                    {
                        CompanyId = c.CompanyId,
                        CompanyName = c.CompanyName,
                        PlanTier = c.PlanTier.ToString(),
                        Status = c.SubscriptionStatus.ToString(),
                        StartDate = c.SubscriptionStartDate,
                        RenewalDate = c.SubscriptionRenewalDate,
                        RequestedTier = c.RequestedPlanTier == null ? string.Empty
                            : $"{c.RequestedPlanTier} (requested {c.UpgradeRequestedAt:MMM d})"
                    })
                    .ToList();
                LoadAnalytics();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateSubscription()
        {
            try
            {
                UpdateSubscriptionCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not update this subscription:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateSubscriptionCore()
        {
            if (_subGrid.SelectedRows.Count == 0) return;
            var row = (SubscriptionRow)_subGrid.SelectedRows[0].DataBoundItem;

            using var db = CreateDbContext();
            var entity = db.Companies.Find(row.CompanyId);
            if (entity is null) return;

            // Pre-select whatever the tenant actually asked for, if anything - saves a click on
            // the common "approve exactly as requested" path while still letting a SuperAdmin pick
            // a different tier entirely (the picker starts there, not locked to it).
            var startingTier = (entity.RequestedPlanTier ?? entity.PlanTier).ToString();

            using var dialog = new SubscriptionDialogForm(row.CompanyName, startingTier, row.RenewalDate);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var prev = entity.PlanTier;
            entity.PlanTier = dialog.PlanTier;
            entity.SubscriptionRenewalDate = dialog.RenewalDate;
            // Whatever request existed has now been reviewed and actioned, whether the tier ended
            // up matching what was asked for or not - clear it either way.
            entity.RequestedPlanTier = null;
            entity.UpgradeRequestedAt = null;
            db.SaveChanges();

            var companyDb = db.CompanyDatabases.FirstOrDefault(x => x.CompanyId == entity.CompanyId && x.IsActive);
            WriteCompanyAuditLog(entity, companyDb, AuditAction.Update,
                prev == entity.PlanTier
                    ? $"Updated renewal date for {entity.CompanyName}"
                    : $"Changed plan for {entity.CompanyName}: {prev} → {entity.PlanTier}");

            LoadCompanies();
            LoadSubscriptions();
        }

        private void SetSubscriptionStatus(SubscriptionStatus status)
        {
            if (_subGrid.SelectedRows.Count == 0) return;
            var row = (SubscriptionRow)_subGrid.SelectedRows[0].DataBoundItem;

            var verb = status == SubscriptionStatus.Suspended ? "Suspend" : "Reactivate";
            if (MessageBox.Show(
                    $"{verb} subscription for {row.CompanyName}?",
                    verb, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                using var db = CreateDbContext();
                var entity = db.Companies.Find(row.CompanyId);
                if (entity is null) return;

                entity.SubscriptionStatus = status;
                db.SaveChanges();

                var companyDb = db.CompanyDatabases.FirstOrDefault(x => x.CompanyId == entity.CompanyId && x.IsActive);
                WriteCompanyAuditLog(entity, companyDb, AuditAction.Update,
                    $"Subscription {verb.ToLowerInvariant()}d: {entity.CompanyName}");

                LoadSubscriptions();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not {verb.ToLowerInvariant()} this subscription:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Shared helpers
        // ═══════════════════════════════════════════════════════════════════════

        private static MasterErpDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MasterErpDbContext>()
                .UseSqlServer(AppDbConfig.MasterErpConnectionString, sql => sql.EnableRetryOnFailure(3))
                .Options;
            return new MasterErpDbContext(options);
        }

        /// <summary>
        /// Company lives in the Master database, but AuditLog is a per-tenant table (see
        /// AuditLog's design note) - so a Company change made here by a SuperAdmin who isn't
        /// scoped to any one tenant gets written directly into that company's own
        /// TenantErpDbContext instead of going through TenantSyncService.SaveAndSync. Best-effort:
        /// the Company change itself has already been saved to the Master database by the time
        /// this runs, so a tenant database that's unreachable (e.g. brand new, not yet
        /// provisioned) just means the audit entry is skipped rather than blocking the workflow.
        /// </summary>
        private static void WriteCompanyAuditLog(Company company, CompanyDatabase? companyDatabase,
            AuditAction action, string summary)
        {
            if (companyDatabase is null
                || string.IsNullOrWhiteSpace(companyDatabase.ServerName)
                || string.IsNullOrWhiteSpace(companyDatabase.DatabaseName))
            {
                return;
            }

            try
            {
                var tenantConnectionString = ConnectionStrings.BuildTenantConnectionString(
                    companyDatabase.ServerName, companyDatabase.DatabaseName, companyDatabase.CredentialKey);
                var tenantOptions = new DbContextOptionsBuilder<TenantErpDbContext>()
                    .UseSqlServer(tenantConnectionString, sql => sql.EnableRetryOnFailure(3))
                    .Options;

                using var tenantDb = new TenantErpDbContext(tenantOptions);
                tenantDb.AuditLogs.Add(new AuditLog
                {
                    TenantUserId = null,
                    ActorName = AppSession.CurrentDisplayName ?? AppSession.CurrentUsername ?? "System Admin",
                    EntityName = nameof(Company),
                    EntityId = company.CompanyId,
                    Action = action,
                    Summary = summary,
                    Timestamp = DateTime.UtcNow
                });
                tenantDb.SaveChanges();
            }
            catch
            {
                // Tenant database unreachable or not yet provisioned — skipped rather than surfaced.
            }
        }

        // ─── Row types ───────────────────────────────────────────────────────────

        private sealed class CompanyRow
        {
            public int CompanyId { get; set; }
            public string CompanyCode { get; set; } = string.Empty;
            public string CompanyName { get; set; } = string.Empty;
            public string PlanTier { get; set; } = string.Empty;
            public bool IsActive { get; set; }
        }

        private sealed class SubscriptionRow
        {
            public int CompanyId { get; set; }
            public string CompanyName { get; set; } = string.Empty;
            public string PlanTier { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public DateTime StartDate { get; set; }
            public DateTime? RenewalDate { get; set; }

            /// <summary>Blank when no upgrade request is pending for this company.</summary>
            public string RequestedTier { get; set; } = string.Empty;
        }
    }
}
