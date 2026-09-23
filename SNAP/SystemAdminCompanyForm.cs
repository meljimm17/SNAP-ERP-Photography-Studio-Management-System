using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.config;
using SNAP.infrastructure.Data;

namespace SNAP
{
    public class SystemAdminCompanyForm : Form
    {
        private readonly DataGridView _grid = new();
        private readonly RoundedButton _add = new();
        private readonly RoundedButton _edit = new() { IsPrimary = false };
        private readonly RoundedButton _toggle = new() { IsPrimary = false };

        public SystemAdminCompanyForm()
        {
            Text = "SNAP ERP - System Administration";
            Width = 1000;
            Height = 600;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.BackgroundColor;

            AppTheme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AutoGenerateColumns = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            AppTheme.StyleStatusColumn(_grid, "IsActive", value =>
                value is true ? ("Active", StatusKind.Success) : ("Inactive", StatusKind.Neutral));

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.BackgroundColor
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

            var header = AppTheme.CreatePageHeader("Company Management", "Manage companies and their subscription tiers.", _add);
            Controls.Add(AppTheme.BuildPage(header, toolbar, AppTheme.WrapContent(_grid)));
            LoadData();
        }

        private void LoadData()
        {
            using var db = CreateDbContext();
            _grid.DataSource = db.Companies
                .OrderBy(x => x.CompanyName)
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

        private static MasterErpDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<MasterErpDbContext>()
                .UseSqlServer(AppDbConfig.MasterErpConnectionString)
                .Options;

            return new MasterErpDbContext(options);
        }

        private void AddCompany()
        {
            using var dialog = new CompanyDialogForm(existingCompany: null, existingDatabase: null);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            using var db = CreateDbContext();
            if (db.Companies.Any(x => x.CompanyCode == dialog.CompanyCode))
            {
                MessageBox.Show("That Company Code is already in use.", "Add Company", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var company = new Company
            {
                CompanyCode = dialog.CompanyCode,
                CompanyName = dialog.CompanyDisplayName,
                PlanTier = dialog.PlanTier,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Companies.Add(company);
            db.SaveChanges();

            var companyDatabase = new CompanyDatabase
            {
                CompanyId = company.CompanyId,
                ServerName = dialog.ServerName,
                DatabaseName = dialog.DatabaseName,
                CredentialKey = dialog.CredentialKey,
                IsActive = true
            };
            db.CompanyDatabases.Add(companyDatabase);
            db.SaveChanges();

            WriteCompanyAuditLog(company, companyDatabase, AuditAction.Create, $"Created company: {company.CompanyName} ({company.PlanTier} tier)");

            LoadData();

            MessageBox.Show("Company added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditCompany()
        {
            if (_grid.SelectedRows.Count == 0) return;

            var row = (CompanyRow)_grid.SelectedRows[0].DataBoundItem;
            using var db = CreateDbContext();
            var entity = db.Companies.Find(row.CompanyId);
            if (entity is null) return;

            var companyDatabase = db.CompanyDatabases.FirstOrDefault(x => x.CompanyId == entity.CompanyId && x.IsActive);

            using var dialog = new CompanyDialogForm(entity, companyDatabase);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            if (db.Companies.Any(x => x.CompanyCode == dialog.CompanyCode && x.CompanyId != entity.CompanyId))
            {
                MessageBox.Show("That Company Code is already in use.", "Edit Company", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                companyDatabase.CredentialKey = dialog.CredentialKey;
            }
            else
            {
                companyDatabase = new CompanyDatabase
                {
                    CompanyId = entity.CompanyId,
                    ServerName = dialog.ServerName,
                    DatabaseName = dialog.DatabaseName,
                    CredentialKey = dialog.CredentialKey,
                    IsActive = true
                };
                db.CompanyDatabases.Add(companyDatabase);
            }

            db.SaveChanges();

            var summary = previousTier == entity.PlanTier
                ? $"Updated company: {entity.CompanyName}"
                : $"Updated company: {entity.CompanyName} (plan tier changed: {previousTier} -> {entity.PlanTier})";
            WriteCompanyAuditLog(entity, companyDatabase, AuditAction.Update, summary);

            LoadData();
        }

        private void ToggleCompany()
        {
            if (_grid.SelectedRows.Count == 0) return;

            var row = (CompanyRow)_grid.SelectedRows[0].DataBoundItem;
            using var db = CreateDbContext();
            var entity = db.Companies.Find(row.CompanyId);
            if (entity is null) return;

            entity.IsActive = !entity.IsActive;
            db.SaveChanges();

            var companyDatabase = db.CompanyDatabases.FirstOrDefault(x => x.CompanyId == entity.CompanyId && x.IsActive);
            WriteCompanyAuditLog(entity, companyDatabase, AuditAction.Update, $"{(entity.IsActive ? "Activated" : "Deactivated")} company: {entity.CompanyName}");

            LoadData();
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
        private static void WriteCompanyAuditLog(Company company, CompanyDatabase? companyDatabase, AuditAction action, string summary)
        {
            if (companyDatabase is null || string.IsNullOrWhiteSpace(companyDatabase.ServerName) || string.IsNullOrWhiteSpace(companyDatabase.DatabaseName))
            {
                return;
            }

            try
            {
                var tenantConnectionString = ConnectionStrings.BuildTenantConnectionString(companyDatabase.ServerName, companyDatabase.DatabaseName, companyDatabase.CredentialKey);
                var tenantOptions = new DbContextOptionsBuilder<TenantErpDbContext>()
                    .UseSqlServer(tenantConnectionString)
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
                // Tenant database unreachable or not yet provisioned - the Company change itself
                // already succeeded against the Master database, so this is skipped rather than
                // surfaced as an error.
            }
        }

        private sealed class CompanyRow
        {
            public int CompanyId { get; set; }
            public string CompanyCode { get; set; } = string.Empty;
            public string CompanyName { get; set; } = string.Empty;
            public string PlanTier { get; set; } = string.Empty;
            public bool IsActive { get; set; }
        }
    }
}
