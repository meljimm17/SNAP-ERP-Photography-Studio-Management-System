using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.Data;
using SNAP.infrastructure.services;

namespace SNAP
{
    public class EquipmentForm : UserControl
    {
        private readonly DataGridView _grid = new();
        private readonly RoundedButton _add = new();
        private readonly RoundedButton _edit = new() { IsPrimary = false };
        private readonly RoundedButton _delete = new() { IsPrimary = false };
        private readonly RoundedComboBox _categoryFilter = new();

        public EquipmentForm(AccessLevel accessLevel = AccessLevel.Full)
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
                "Available" => ("Available", StatusKind.Success),
                "InUse" => ("In Use", StatusKind.Info),
                "Maintenance" => ("Maintenance", StatusKind.Warning),
                "Retired" => ("Retired", StatusKind.Danger),
                var other => (other, StatusKind.Neutral)
            });

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.BackgroundColor
            };

            _add.Text = "+ Add Equipment";
            _edit.Text = "Edit";
            _delete.Text = "Delete";
            _add.Width = 150;
            _edit.Width = 90;
            _delete.Width = 90;
            _edit.Margin = new Padding(0, 0, 8, 0);
            _delete.Margin = new Padding(0, 0, 8, 0);

            _categoryFilter.Width = 180;
            _categoryFilter.Items.Add("All Categories");
            foreach (var category in Enum.GetValues<EquipmentCategory>()) _categoryFilter.Items.Add(category);
            _categoryFilter.SelectedIndex = 0;
            _categoryFilter.SelectedIndexChanged += (_, _) => LoadData();

            _add.Click += (_, _) => AddEquipment();
            _edit.Click += (_, _) => EditEquipment();
            _delete.Click += (_, _) => DeleteEquipment();

            toolbar.Controls.Add(_edit);
            toolbar.Controls.Add(_delete);
            toolbar.Controls.Add(_categoryFilter);

            var header = AppTheme.CreatePageHeader("Equipment", "Manage photography equipment and availability.", _add);
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
                var query = db.Equipment
                    .Include(x => x.AssignedToUser)
                    .Where(x => x.IsActive);

                if (_categoryFilter.SelectedItem is EquipmentCategory category)
                {
                    query = query.Where(x => x.Category == category);
                }

                _grid.DataSource = query
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => new EquipmentRow
                    {
                        EquipmentId = x.EquipmentId,
                        EquipmentName = x.EquipmentName,
                        Category = x.Category.ToString(),
                        AcquisitionCost = x.AcquisitionCost,
                        Status = x.Status.ToString(),
                        AssignedTo = x.AssignedToUser != null ? x.AssignedToUser.FullName : "Unassigned",
                        Notes = x.Notes ?? string.Empty
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

        private void AddEquipment()
        {
            using var db = CreateDbContext();
            var users = db.TenantUsers.Where(x => x.Status == UserStatus.Active).OrderBy(x => x.FullName).ToList();

            using var dialog = new EquipmentDialogForm(existing: null, users);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            db.Equipment.Add(new Equipment
            {
                EquipmentName = dialog.EquipmentName,
                Category = dialog.Category,
                AcquisitionCost = dialog.AcquisitionCost,
                Status = dialog.Status,
                AssignedToUserId = dialog.AssignedToUserId,
                Notes = dialog.Notes,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            TenantSyncService.SaveAndSync(db);
            LoadData();

            MessageBox.Show("Equipment added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void EditEquipment()
        {
            if (_grid.SelectedRows.Count == 0) return;

            var selected = (EquipmentRow)_grid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            using var db = CreateDbContext();
            var entity = db.Equipment.Find(selected.EquipmentId);
            if (entity is null) return;

            var users = db.TenantUsers.Where(x => x.Status == UserStatus.Active).OrderBy(x => x.FullName).ToList();

            using var dialog = new EquipmentDialogForm(entity, users);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            entity.EquipmentName = dialog.EquipmentName;
            entity.Category = dialog.Category;
            entity.AcquisitionCost = dialog.AcquisitionCost;
            entity.Status = dialog.Status;
            entity.AssignedToUserId = dialog.AssignedToUserId;
            entity.Notes = dialog.Notes;

            TenantSyncService.SaveAndSync(db);
            LoadData();
        }

        private void DeleteEquipment()
        {
            if (_grid.SelectedRows.Count == 0) return;

            var selected = (EquipmentRow)_grid.SelectedRows[0].DataBoundItem;
            if (selected is null) return;

            if (MessageBox.Show(
                    $"Delete '{selected.EquipmentName}'? It will no longer appear in this list.",
                    "Delete Equipment", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            using var db = CreateDbContext();
            var entity = db.Equipment.Find(selected.EquipmentId);
            if (entity is null) return;

            entity.IsActive = false;
            TenantSyncService.SaveAndSync(db);
            LoadData();
        }

        private sealed class EquipmentRow
        {
            public int EquipmentId { get; set; }
            public string EquipmentName { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public decimal? AcquisitionCost { get; set; }
            public string Status { get; set; } = string.Empty;
            public string AssignedTo { get; set; } = string.Empty;
            public string Notes { get; set; } = string.Empty;
        }
    }
}
