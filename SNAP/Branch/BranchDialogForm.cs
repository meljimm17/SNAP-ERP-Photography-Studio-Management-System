using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.dtos;

namespace SNAP
{
    /// <summary>
    /// Add/edit one branch. The main-branch and active flags are deliberately editable here but
    /// enforced server-side: BranchController rejects demoting or retiring the last main branch,
    /// and retiring a branch that still has staff assigned.
    /// </summary>
    public class BranchDialogForm : Form
    {
        private readonly RoundedTextBox _nameBox = new() { PlaceholderText = "Branch name" };
        private readonly RoundedTextBox _addressBox = new() { PlaceholderText = "Address (optional)" };
        private readonly RoundedTextBox _contactBox = new() { PlaceholderText = "Contact number (optional)" };
        private readonly CheckBox _mainBranchCheck = new() { Text = "Main branch", ForeColor = AppTheme.TextPrimaryColor, Font = AppTheme.BodyFont, AutoSize = false, UseMnemonic = false };
        private readonly CheckBox _activeCheck = new() { Text = "Active", ForeColor = AppTheme.TextPrimaryColor, Font = AppTheme.BodyFont, AutoSize = false, UseMnemonic = false };

        private readonly RoundedButton _saveButton = new() { Text = "Save Branch" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public string BranchName { get; private set; } = string.Empty;
        public string? Address { get; private set; }
        public string? ContactNumber { get; private set; }
        public bool IsMainBranch { get; private set; }
        public bool IsActive { get; private set; } = true;

        public BranchDialogForm(BranchDetailDto? existing)
        {
            Text = existing is null ? "New Branch" : "Edit Branch";
            Width = 520;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 410;
            _nameBox.Width = width;
            _addressBox.Width = width;
            _contactBox.Width = width;
            _mainBranchCheck.Width = width;
            _mainBranchCheck.Height = 24;
            _mainBranchCheck.Margin = new Padding(0, 0, 0, 10);
            _activeCheck.Width = width;
            _activeCheck.Height = 24;
            _activeCheck.Margin = new Padding(0, 0, 0, 10);
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            _activeCheck.Checked = true;

            if (existing is not null)
            {
                _nameBox.Text = existing.BranchName;
                _addressBox.Text = existing.Address ?? string.Empty;
                _contactBox.Text = existing.ContactNumber ?? string.Empty;
                _mainBranchCheck.Checked = existing.IsMainBranch;
                _activeCheck.Checked = existing.IsActive;
            }

            Controls.Add(AppTheme.BuildDialogLayout(_saveButton, _cancelButton,
                AppTheme.BuildFieldGroup("Branch Name", _nameBox),
                AppTheme.BuildFieldGroup("Address", _addressBox),
                AppTheme.BuildFieldGroup("Contact Number", _contactBox),
                _mainBranchCheck,
                _activeCheck));

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show("Enter a branch name.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            BranchName = _nameBox.Text.Trim();
            Address = string.IsNullOrWhiteSpace(_addressBox.Text) ? null : _addressBox.Text.Trim();
            ContactNumber = string.IsNullOrWhiteSpace(_contactBox.Text) ? null : _contactBox.Text.Trim();
            IsMainBranch = _mainBranchCheck.Checked;
            IsActive = _activeCheck.Checked;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
