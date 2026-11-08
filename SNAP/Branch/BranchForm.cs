using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.dtos;
using SNAP.domain.enums;

namespace SNAP
{
    /// <summary>
    /// Branch Management - Medium tier only. Lists every branch including retired ones so they can
    /// be reactivated, and is the one place the "exactly one main branch" rule is exercised.
    /// The View half of the MVP split: all server calls go through BranchPresenter.
    /// </summary>
    public class BranchForm : UserControl
    {
        private readonly BranchPresenter _presenter = new();

        private readonly RoundedTextBox _search = new() { PlaceholderText = "Search by branch name, address, or contact number..." };
        private readonly DataGridView _grid = new();
        private readonly RoundedButton _add = new();
        private readonly RoundedButton _edit = new() { IsPrimary = false };

        private List<BranchRow> _allBranches = new();

        public BranchForm(AccessLevel accessLevel = AccessLevel.Full)
        {
            Dock = DockStyle.Fill;
            BackColor = AppTheme.BackgroundColor;

            AppTheme.StyleGrid(_grid);
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = true;
            _grid.AutoGenerateColumns = false;
            _grid.AllowUserToAddRows = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;

            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "BranchName", DataPropertyName = "BranchName", HeaderText = "Branch", FillWeight = 150 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Address", DataPropertyName = "Address", HeaderText = "Address", FillWeight = 190 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "ContactNumber", DataPropertyName = "ContactNumber", HeaderText = "Contact Number", FillWeight = 120 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "StaffCount", DataPropertyName = "StaffCount", HeaderText = "Staff", FillWeight = 60 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Role", DataPropertyName = "Role", HeaderText = "Role", FillWeight = 90 });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", DataPropertyName = "Status", HeaderText = "Status", FillWeight = 90 });

            AppTheme.StyleStatusColumn(_grid, "Role", value =>
                value?.ToString() == "Main" ? ("Main", StatusKind.Info) : ("Branch", StatusKind.Neutral));
            AppTheme.StyleStatusColumn(_grid, "Status", value =>
                value?.ToString() == "Active" ? ("Active", StatusKind.Success) : ("Retired", StatusKind.Warning));

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 12, 0, 12),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.BackgroundColor
            };

            _search.Width = 320;
            _search.Margin = new Padding(0, 0, 16, 0);
            _add.Text = "+ Add Branch";
            _edit.Text = "Edit";
            _add.Width = 130;
            _edit.Width = 90;
            _add.Margin = new Padding(0, 0, 8, 0);

            _add.Click += (_, _) => AddBranch();
            _edit.Click += (_, _) => EditBranch();
            _search.TextChanged += (_, _) => ApplyFilter();

            toolbar.Controls.Add(_search);
            toolbar.Controls.Add(_add);
            toolbar.Controls.Add(_edit);

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

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                _allBranches = _presenter.GetBranches();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading branches:\n\n{ex.Message}", "Branch Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ApplyFilter()
        {
            var filter = _search.Text.Trim();
            var rows = string.IsNullOrWhiteSpace(filter)
                ? _allBranches
                : _allBranches.Where(b =>
                    b.BranchName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    b.Address.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    b.ContactNumber.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            _grid.DataSource = rows.Select(b => new BranchGridRow
            {
                BranchId = b.BranchId,
                BranchName = b.BranchName,
                Address = b.Address,
                ContactNumber = b.ContactNumber,
                StaffCount = b.StaffCount,
                Role = b.IsMainBranch ? "Main" : "Branch",
                Status = b.IsActive ? "Active" : "Retired"
            }).ToList();
        }

        private void AddBranch()
        {
            using var dialog = new BranchDialogForm(existing: null);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                _presenter.AddBranch(dialog.BranchName, dialog.Address, dialog.ContactNumber, dialog.IsMainBranch);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Add Branch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void EditBranch()
        {
            if (_grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Select a branch first.", "Edit Branch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (_grid.SelectedRows[0].DataBoundItem is not BranchGridRow selected) return;

            try
            {
                var detail = _presenter.GetBranch(selected.BranchId);
                using var dialog = new BranchDialogForm(detail);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                _presenter.UpdateBranch(selected.BranchId, dialog.BranchName, dialog.Address, dialog.ContactNumber, dialog.IsMainBranch, dialog.IsActive);
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Edit Branch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private sealed class BranchGridRow
        {
            public int BranchId { get; set; }
            public string BranchName { get; set; } = string.Empty;
            public string Address { get; set; } = string.Empty;
            public string ContactNumber { get; set; } = string.Empty;
            public int StaffCount { get; set; }
            public string Role { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
        }
    }
}
