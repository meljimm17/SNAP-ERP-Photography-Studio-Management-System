using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;
using SNAP.infrastructure.services;

namespace SNAP
{
    /// <summary>
    /// Add/Edit dialog for a tenant user (Admin/Staff/Photographer), with a Role dropdown and a
    /// Status dropdown (Active/Inactive/Suspended). Accounts are suspended/deactivated, never
    /// hard-deleted, since they may be referenced by historical bookings/payments.
    /// </summary>
    public class UserDialogForm : Form
    {
        private static readonly SystemRole[] TenantRoles = { SystemRole.Admin, SystemRole.Staff, SystemRole.Photographer };

        /// <summary>
        /// BranchManager and FinanceManager only mean something once a company runs more than one
        /// branch, so they're offered from Medium tier up and withheld below it.
        /// </summary>
        private static readonly SystemRole[] MediumTierRoles =
            { SystemRole.Admin, SystemRole.Staff, SystemRole.Photographer, SystemRole.BranchManager, SystemRole.FinanceManager };

        /// <summary>Roles tied to one physical location, and so required to name a branch.</summary>
        private static bool RoleNeedsBranch(SystemRole role) => BranchScoping.RoleIsBranchBound(role);

        private readonly bool _isEdit;
        private readonly RoundedTextBox _fullNameBox = new() { PlaceholderText = "Full Name (text, e.g. Juan Dela Cruz)" };
        private readonly RoundedTextBox _usernameBox = new() { PlaceholderText = "Username (text, no spaces)" };
        private readonly RoundedTextBox _passwordBox = new() { PlaceholderText = "Password (text)", UseSystemPasswordChar = true };
        private readonly RoundedComboBox _roleCombo = new();
        private readonly RoundedComboBox _statusCombo = new();
        private readonly RoundedTextBox _monthlySalaryBox = new() { PlaceholderText = "Monthly Salary (decimal number, optional)" };
        private readonly RoundedComboBox _branchCombo = new();
        private Control? _branchGroup;

        private readonly RoundedButton _saveButton = new() { Text = "Save User" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public string FullName { get; private set; } = string.Empty;
        public string Username { get; private set; } = string.Empty;
        public string? PasswordHash { get; private set; }
        public SystemRole Role { get; private set; }
        public UserStatus Status { get; private set; }
        public decimal? MonthlySalary { get; private set; }

        /// <summary>Null for the company-wide roles, which aren't tied to a location.</summary>
        public int? BranchId { get; private set; }

        public UserDialogForm(TenantUser? existing)
        {
            _isEdit = existing is not null;

            Text = existing is null ? "Add User" : $"Edit User - {existing.Username}";
            Width = 510;
            Height = 640;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 400;
            _fullNameBox.Width = width;
            _usernameBox.Width = width;
            _passwordBox.Width = width;
            _roleCombo.Width = width;
            _statusCombo.Width = width;
            _monthlySalaryBox.Width = width;
            _branchCombo.Width = width;
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            var selectableRoles = AppSession.IsBranchAware ? MediumTierRoles : TenantRoles;
            foreach (var role in selectableRoles) _roleCombo.Items.Add(role);
            foreach (var status in Enum.GetValues<UserStatus>()) _statusCombo.Items.Add(status);
            LoadBranches();

            Controls.Add(AppTheme.BuildDialogLayout(_saveButton, _cancelButton,
                AppTheme.BuildFieldGroup("Full Name", _fullNameBox),
                AppTheme.BuildFieldGroup("Username", _usernameBox),
                AppTheme.BuildFieldGroup(existing is null ? "Password" : "New Password (leave blank to keep current)", _passwordBox),
                AppTheme.BuildFieldGroup("Role", _roleCombo),
                AppTheme.BuildFieldGroup("Status", _statusCombo),
                AppTheme.BuildFieldGroup("Monthly Salary (₱, optional - enables payroll)", _monthlySalaryBox),
                _branchGroup = AppTheme.BuildFieldGroup("Branch", _branchCombo)));

            if (existing is not null)
            {
                _fullNameBox.Text = existing.FullName;
                _usernameBox.Text = existing.Username;
                _roleCombo.SelectedItem = existing.Role;
                _statusCombo.SelectedItem = existing.Status;
                _monthlySalaryBox.Text = existing.MonthlySalary is null ? string.Empty : existing.MonthlySalary.Value.ToString("0.##");
                SelectBranch(existing.BranchId);
            }
            else
            {
                _roleCombo.SelectedIndex = 1; // Staff by default
                _statusCombo.SelectedItem = UserStatus.Active;
            }

            _roleCombo.SelectedIndexChanged += (_, _) => ApplyBranchVisibility();
            ApplyBranchVisibility();

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void LoadBranches()
        {
            if (!AppSession.IsBranchAware) return;

            try
            {
                foreach (var branch in new BranchPresenter().GetActiveBranches())
                {
                    _branchCombo.Items.Add(new BranchItem(branch.BranchId, branch.BranchName));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not load branches:\n\n{ex.Message}", "Branches", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SelectBranch(int? branchId)
        {
            if (branchId is null) return;

            for (var i = 0; i < _branchCombo.Items.Count; i++)
            {
                if (_branchCombo.Items[i] is BranchItem item && item.Id == branchId)
                {
                    _branchCombo.SelectedIndex = i;
                    return;
                }
            }
        }

        /// <summary>
        /// The branch picker only appears for roles that work at one location. Admin and
        /// FinanceManager are company-wide, so showing them a branch would imply a restriction
        /// that doesn't exist. Hidden entirely below Medium tier, where there is only one branch.
        /// </summary>
        private void ApplyBranchVisibility()
        {
            if (_branchGroup is null) return;

            var needsBranch = AppSession.IsBranchAware
                && _roleCombo.SelectedItem is SystemRole role
                && RoleNeedsBranch(role);

            _branchGroup.Visible = needsBranch;
        }

        private sealed record BranchItem(int Id, string Display)
        {
            public override string ToString() => Display;
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_fullNameBox.Text) || string.IsNullOrWhiteSpace(_usernameBox.Text))
            {
                MessageBox.Show("Full Name and Username are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_isEdit && string.IsNullOrWhiteSpace(_passwordBox.Text))
            {
                MessageBox.Show("Password is required for a new user.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_roleCombo.SelectedItem is not SystemRole role)
            {
                MessageBox.Show("Select a role.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_statusCombo.SelectedItem is not UserStatus status)
            {
                MessageBox.Show("Select a status.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_monthlySalaryBox.Text) && (!decimal.TryParse(_monthlySalaryBox.Text, out var salary) || salary < 0))
            {
                MessageBox.Show("Enter a valid, non-negative Monthly Salary, or leave it blank.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            FullName = _fullNameBox.Text.Trim();
            Username = _usernameBox.Text.Trim();
            var needsBranch = AppSession.IsBranchAware && RoleNeedsBranch(role);
            if (needsBranch && _branchCombo.SelectedItem is not BranchItem)
            {
                MessageBox.Show($"Select the branch this {role} works at.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Cleared rather than kept when the role isn't branch-bound, so an Admin promoted from
            // Staff doesn't silently stay filtered to their old branch.
            BranchId = needsBranch && _branchCombo.SelectedItem is BranchItem branch ? branch.Id : null;

            Role = role;
            Status = status;
            MonthlySalary = string.IsNullOrWhiteSpace(_monthlySalaryBox.Text) ? null : decimal.Parse(_monthlySalaryBox.Text);
            PasswordHash = string.IsNullOrWhiteSpace(_passwordBox.Text)
                ? null
                : AuthenticationService.HashPassword(_passwordBox.Text);

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
