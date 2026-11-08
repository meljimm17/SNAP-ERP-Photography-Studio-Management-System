using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP
{
    public class UserManagementForm : UserControl
    {
        private readonly DataGridView _grid = new();
        private readonly RoundedButton _add = new();
        private readonly RoundedButton _edit = new() { IsPrimary = false };
        private readonly RoundedButton _delete = new() { IsPrimary = false };

        public UserManagementForm(AccessLevel accessLevel = AccessLevel.Full)
        {
            Dock = DockStyle.Fill;
            BackColor = AppTheme.BackgroundColor;

            AppTheme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AutoGenerateColumns = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            AppTheme.StyleStatusColumn(_grid, "Status", value => (value?.ToString() ?? string.Empty) switch
            {
                "Active" => ("Active", StatusKind.Success),
                "Suspended" => ("Suspended", StatusKind.Danger),
                var other => (other, StatusKind.Neutral)
            });

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.BackgroundColor
            };

            _add.Text = "+ Add User";
            _edit.Text = "Edit";
            _delete.Text = "Delete";
            _add.Width = 120;
            _edit.Width = 90;
            _delete.Width = 90;
            _edit.Margin = new Padding(0, 0, 8, 0);

            _add.Click += (_, _) => AddUser();
            _edit.Click += (_, _) => EditUser();
            _delete.Click += (_, _) => DeleteUser();

            toolbar.Controls.Add(_edit);
            toolbar.Controls.Add(_delete);
            toolbar.Controls.Add(_add);

            // No page header here - this control is only ever embedded as a tab inside
            // SystemAdministrationForm, which owns the single outer header for the whole screen.
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(toolbar, 0, 0);
            layout.Controls.Add(AppTheme.WrapContent(_grid), 0, 1);
            Controls.Add(layout);

            var canEdit = accessLevel is AccessLevel.Manage or AccessLevel.Full;
            _add.Enabled = canEdit;
            _edit.Enabled = canEdit;
            _delete.Enabled = canEdit;

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using var db = CreateDbContext();
                _grid.DataSource = db.TenantUsers
                    .Include(x => x.Branch)
                    .Where(x => x.Status != UserStatus.Inactive)
                    .OrderByDescending(x => x.CreatedAt)
                    .ThenByDescending(x => x.UserId)
                    .Select(x => new UserRow
                    {
                        UserId = x.UserId,
                        FullName = x.FullName,
                        Username = x.Username,
                        Role = x.Role.ToString(),
                        // Company-wide roles are deliberately unassigned, so an empty cell here
                        // reads correctly as "every branch" rather than as missing data.
                        BranchName = x.Branch != null ? x.Branch.BranchName : string.Empty,
                        Status = x.Status.ToString()
                    })
                    .ToList();

                AppTheme.ApplyBranchColumns(_grid, AppSession.IsBranchAware);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Database Connection Error\n\n{ex.Message}\n\nMake sure the databases are created and configured.", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static TenantErpDbContext CreateDbContext()
        {
            var connectionString = AppSession.CurrentTenantConnectionString
                ?? AppDbConfig.TenantErpConnectionString;

            var options = new DbContextOptionsBuilder<TenantErpDbContext>()
                .UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3))
                .Options;

            return new TenantErpDbContext(options);
        }

        private void AddUser()
        {
            try
            {
                AddUserCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not add this user:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddUserCore()
        {
            using var dialog = new UserDialogForm(existing: null);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            using var db = CreateDbContext();
            if (db.TenantUsers.Any(x => x.Username == dialog.Username))
            {
                MessageBox.Show("That username is already taken.", "Add User", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            db.TenantUsers.Add(new TenantUser
            {
                FullName = dialog.FullName,
                Username = dialog.Username,
                PasswordHash = dialog.PasswordHash!,
                Role = dialog.Role,
                Status = dialog.Status,
                MonthlySalary = dialog.MonthlySalary,
                BranchId = dialog.BranchId,
                CreatedAt = DateTime.UtcNow
            });
            TenantSyncService.SaveAndSync(db);
            LoadData();

            MessageBox.Show("User added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditUser()
        {
            try
            {
                EditUserCore();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not update this user:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EditUserCore()
        {
            if (_grid.SelectedRows.Count == 0) return;

            var row = (UserRow)_grid.SelectedRows[0].DataBoundItem;
            using var db = CreateDbContext();
            var entity = db.TenantUsers.Find(row.UserId);
            if (entity is null) return;

            using var dialog = new UserDialogForm(entity);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            if (db.TenantUsers.Any(x => x.Username == dialog.Username && x.UserId != entity.UserId))
            {
                MessageBox.Show("That username is already taken.", "Edit User", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            entity.FullName = dialog.FullName;
            entity.Username = dialog.Username;
            entity.Role = dialog.Role;
            entity.Status = dialog.Status;
            entity.MonthlySalary = dialog.MonthlySalary;
            entity.BranchId = dialog.BranchId;
            if (dialog.PasswordHash is not null)
            {
                entity.PasswordHash = dialog.PasswordHash;
            }

            TenantSyncService.SaveAndSync(db);
            LoadData();
        }

        private void DeleteUser()
        {
            if (_grid.SelectedRows.Count == 0) return;

            var row = (UserRow)_grid.SelectedRows[0].DataBoundItem;
            if (row is null) return;

            if (MessageBox.Show(
                    $"Delete '{row.FullName}'? They will no longer be able to log in, but their history is preserved.",
                    "Delete User", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                using var db = CreateDbContext();
                var entity = db.TenantUsers.Find(row.UserId);
                if (entity is null) return;

                entity.Status = UserStatus.Inactive;
                TenantSyncService.SaveAndSync(db);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not delete this user:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private sealed class UserRow
        {
            public int UserId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public string BranchName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }
    }
}
