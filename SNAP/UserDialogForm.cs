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

        private readonly bool _isEdit;
        private readonly RoundedTextBox _fullNameBox = new() { PlaceholderText = "Full Name (text, e.g. Juan Dela Cruz)" };
        private readonly RoundedTextBox _usernameBox = new() { PlaceholderText = "Username (text, no spaces)" };
        private readonly RoundedTextBox _passwordBox = new() { PlaceholderText = "Password (text)", UseSystemPasswordChar = true };
        private readonly RoundedComboBox _roleCombo = new();
        private readonly RoundedComboBox _statusCombo = new();
        private readonly RoundedTextBox _monthlySalaryBox = new() { PlaceholderText = "Monthly Salary (decimal number, optional)" };

        private readonly RoundedButton _saveButton = new() { Text = "Save User" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public string FullName { get; private set; } = string.Empty;
        public string Username { get; private set; } = string.Empty;
        public string? PasswordHash { get; private set; }
        public SystemRole Role { get; private set; }
        public UserStatus Status { get; private set; }
        public decimal? MonthlySalary { get; private set; }

        public UserDialogForm(TenantUser? existing)
        {
            _isEdit = existing is not null;

            Text = existing is null ? "Add User" : $"Edit User - {existing.Username}";
            Width = 480;
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
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            foreach (var role in TenantRoles) _roleCombo.Items.Add(role);
            foreach (var status in Enum.GetValues<UserStatus>()) _statusCombo.Items.Add(status);

            Controls.Add(AppTheme.BuildDialogBody(
                AppTheme.BuildFieldGroup("Full Name", _fullNameBox),
                AppTheme.BuildFieldGroup("Username", _usernameBox),
                AppTheme.BuildFieldGroup(existing is null ? "Password" : "New Password (leave blank to keep current)", _passwordBox),
                AppTheme.BuildFieldGroup("Role", _roleCombo),
                AppTheme.BuildFieldGroup("Status", _statusCombo),
                AppTheme.BuildFieldGroup("Monthly Salary (₱, optional - enables payroll)", _monthlySalaryBox),
                AppTheme.BuildDialogButtonRow(_saveButton, _cancelButton)));

            if (existing is not null)
            {
                _fullNameBox.Text = existing.FullName;
                _usernameBox.Text = existing.Username;
                _roleCombo.SelectedItem = existing.Role;
                _statusCombo.SelectedItem = existing.Status;
                _monthlySalaryBox.Text = existing.MonthlySalary is null ? string.Empty : existing.MonthlySalary.Value.ToString("0.##");
            }
            else
            {
                _roleCombo.SelectedIndex = 1; // Staff by default
                _statusCombo.SelectedItem = UserStatus.Active;
            }

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
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
