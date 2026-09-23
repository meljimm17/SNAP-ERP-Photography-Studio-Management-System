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

            var header = AppTheme.CreatePageHeader("System Administration", "Manage system configuration and administrative functions.", _add);
            Controls.Add(AppTheme.BuildPage(header, toolbar, AppTheme.WrapContent(_grid)));

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
                    .Where(x => x.Status != UserStatus.Inactive)
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new UserRow
                    {
                        UserId = x.UserId,
                        FullName = x.FullName,
                        Username = x.Username,
                        Role = x.Role.ToString(),
                        Status = x.Status.ToString()
                    })
                    .ToList();
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
                .UseSqlServer(connectionString)
                .Options;

            return new TenantErpDbContext(options);
        }

        private void AddUser()
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
                CreatedAt = DateTime.UtcNow
            });
            TenantSyncService.SaveAndSync(db);
            LoadData();

            MessageBox.Show("User added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditUser()
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

            using var db = CreateDbContext();
            var entity = db.TenantUsers.Find(row.UserId);
            if (entity is null) return;

            entity.Status = UserStatus.Inactive;
            TenantSyncService.SaveAndSync(db);
            LoadData();
        }

        private sealed class UserRow
        {
            public int UserId { get; set; }
            public string FullName { get; set; } = string.Empty;
            public string Username { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }
    }
}
