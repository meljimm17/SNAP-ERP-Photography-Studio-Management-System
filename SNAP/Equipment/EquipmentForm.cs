using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.dtos;
using SNAP.domain.enums;

namespace SNAP
{
    public class EquipmentForm : UserControl
    {
        private readonly DataGridView _grid = new();
        private readonly RoundedButton _add = new();
        private readonly RoundedButton _edit = new() { IsPrimary = false };
        private readonly RoundedButton _delete = new() { IsPrimary = false };
        private readonly RoundedComboBox _categoryFilter = new();
        private readonly RoundedComboBox _branchPicker = new();

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

            BranchFilter.Populate(_branchPicker);
            _branchPicker.Width = 170;
            _branchPicker.Margin = new Padding(8, 0, 0, 0);
            _branchPicker.SelectedIndexChanged += (_, _) => LoadData();

            _add.Click += (_, _) => AddEquipment();
            _edit.Click += (_, _) => EditEquipment();
            _delete.Click += (_, _) => DeleteEquipment();

            toolbar.Controls.Add(_edit);
            toolbar.Controls.Add(_delete);
            toolbar.Controls.Add(_categoryFilter);
            toolbar.Controls.Add(_branchPicker);

            var header = AppTheme.CreatePageHeader("Equipment", "Manage photography equipment and availability.", _add);
            Controls.Add(AppTheme.BuildPage(header, toolbar, AppTheme.WrapContent(_grid)));

            var canEdit = accessLevel is AccessLevel.Manage or AccessLevel.Full;
            _add.Enabled = canEdit;
            _edit.Enabled = canEdit;
            _delete.Enabled = canEdit;

            LoadData();
        }

        private int? SelectedBranchId => BranchFilter.SelectedId(_branchPicker);

        private void LoadData()
        {
            try
            {
                var branchId = BranchFilter.Resolve(SelectedBranchId);
                var queryString = branchId is not null ? $"?branchId={branchId}" : string.Empty;
                var rows = ApiClient.Get<List<EquipmentRow>>($"/api/equipment{queryString}") ?? new();

                // Apply client-side category filter (category is not a query param on this endpoint)
                if (_categoryFilter.SelectedItem is EquipmentCategory category)
                {
                    rows = rows.Where(r => r.Category == category.ToString()).ToList();
                }

                // Show Branch column only when viewing All Branches (picker is visible and null selected)
                var showBranch = BranchFilter.ShowPicker && SelectedBranchId is null;
                _grid.DataSource = rows.Select(r => showBranch
                    ? (object)new EquipmentGridRowWithBranch
                    {
                        EquipmentId = r.EquipmentId, EquipmentName = r.EquipmentName,
                        Category = r.Category, Status = r.Status,
                        AcquisitionCost = r.AcquisitionCost, Branch = r.BranchName
                    }
                    : new EquipmentGridRow
                    {
                        EquipmentId = r.EquipmentId, EquipmentName = r.EquipmentName,
                        Category = r.Category, Status = r.Status,
                        AcquisitionCost = r.AcquisitionCost
                    }).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading equipment:\n\n{ex.Message}", "Equipment Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void AddEquipment()
        {
            var branchId = BranchFilter.Resolve(SelectedBranchId);

            using var dialog = new EquipmentDialogForm(existing: null, branchId: branchId);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                ApiClient.Post<int>("/api/equipment", new
                {
                    dialog.EquipmentName, dialog.Category,
                    dialog.AcquisitionCost, dialog.Status,
                    BranchId = branchId
                });
                LoadData();
                MessageBox.Show("Equipment added successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding equipment:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void EditEquipment()
        {
            if (_grid.SelectedRows.Count == 0) return;
            var selectedId = GetSelectedEquipmentId();
            if (selectedId is null) return;

            try
            {
                var row = GetSelectedRow();
                using var dialog = new EquipmentDialogForm(existing: new EquipmentRowForDialog
                {
                    EquipmentId = selectedId.Value,
                    EquipmentName = row.EquipmentName,
                    Category = row.Category,
                    Status = row.Status,
                    AcquisitionCost = row.AcquisitionCost
                }, branchId: null);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                ApiClient.Put($"/api/equipment/{selectedId}", new
                {
                    dialog.EquipmentName, dialog.Category,
                    dialog.AcquisitionCost, dialog.Status
                });
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating equipment:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void DeleteEquipment()
        {
            if (_grid.SelectedRows.Count == 0) return;
            var row = GetSelectedRow();
            var selectedId = GetSelectedEquipmentId();
            if (selectedId is null) return;

            if (MessageBox.Show(
                    $"Delete '{row.EquipmentName}'? It will no longer appear in this list.",
                    "Delete Equipment", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            try
            {
                ApiClient.Delete($"/api/equipment/{selectedId}");
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting equipment:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private int? GetSelectedEquipmentId()
        {
            if (_grid.SelectedRows.Count == 0) return null;
            var item = _grid.SelectedRows[0].DataBoundItem;
            return item switch
            {
                EquipmentGridRow r => r.EquipmentId,
                EquipmentGridRowWithBranch r => r.EquipmentId,
                _ => null
            };
        }

        private (string EquipmentName, string Category, string Status, decimal? AcquisitionCost) GetSelectedRow()
        {
            var item = _grid.SelectedRows[0].DataBoundItem;
            return item switch
            {
                EquipmentGridRow r => (r.EquipmentName, r.Category, r.Status, r.AcquisitionCost),
                EquipmentGridRowWithBranch r => (r.EquipmentName, r.Category, r.Status, r.AcquisitionCost),
                _ => (string.Empty, string.Empty, string.Empty, null)
            };
        }

        // Two anonymous-shaped rows: one with Branch column (All-Branches view) and one without.
        private sealed class EquipmentGridRow
        {
            public int EquipmentId { get; set; }
            public string EquipmentName { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public decimal? AcquisitionCost { get; set; }
            public string Status { get; set; } = string.Empty;
        }

        private sealed class EquipmentGridRowWithBranch
        {
            public int EquipmentId { get; set; }
            public string EquipmentName { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public decimal? AcquisitionCost { get; set; }
            public string Status { get; set; } = string.Empty;
            public string Branch { get; set; } = string.Empty;
        }

        // Thin holder so EquipmentDialogForm can be passed "existing" data from the API row
        // without re-fetching from the database — the form needs the editable field values only.
        internal sealed class EquipmentRowForDialog
        {
            public int EquipmentId { get; set; }
            public string EquipmentName { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public decimal? AcquisitionCost { get; set; }
        }
    }
}
