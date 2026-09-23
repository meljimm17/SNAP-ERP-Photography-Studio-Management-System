using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;

namespace SNAP
{
    /// <summary>
    /// Add/Edit dialog for a piece of Equipment, with an optional "assigned to" TenantUser.
    /// </summary>
    public class EquipmentDialogForm : Form
    {
        private readonly RoundedTextBox _nameBox = new() { PlaceholderText = "Equipment Name (text, e.g. Canon R5)" };
        private readonly RoundedComboBox _statusCombo = new();
        private readonly RoundedComboBox _assignedToCombo = new();
        private readonly RoundedTextBox _notesBox = new() { PlaceholderText = "Notes (text, optional)" };

        private readonly RoundedButton _saveButton = new() { Text = "Save Equipment" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public string EquipmentName { get; private set; } = string.Empty;
        public EquipmentStatus Status { get; private set; }
        public int? AssignedToUserId { get; private set; }
        public string? Notes { get; private set; }

        public EquipmentDialogForm(Equipment? existing, List<TenantUser> assignableUsers)
        {
            Text = existing is null ? "Add Equipment" : "Edit Equipment";
            Width = 480;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 400;
            _nameBox.Width = width;
            _statusCombo.Width = width;
            _assignedToCombo.Width = width;
            _notesBox.Width = width;
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            foreach (var status in Enum.GetValues<EquipmentStatus>()) _statusCombo.Items.Add(status);

            _assignedToCombo.Items.Add(new ComboItem(0, "Unassigned"));
            foreach (var user in assignableUsers) _assignedToCombo.Items.Add(new ComboItem(user.UserId, user.FullName));

            Controls.Add(AppTheme.BuildDialogBody(
                AppTheme.BuildFieldGroup("Equipment Name", _nameBox),
                AppTheme.BuildFieldGroup("Status", _statusCombo),
                AppTheme.BuildFieldGroup("Assigned To", _assignedToCombo),
                AppTheme.BuildFieldGroup("Notes", _notesBox),
                AppTheme.BuildDialogButtonRow(_saveButton, _cancelButton)));

            if (existing is not null)
            {
                _nameBox.Text = existing.EquipmentName;
                _statusCombo.SelectedItem = existing.Status;
                _notesBox.Text = existing.Notes ?? string.Empty;

                var assignedId = existing.AssignedToUserId ?? 0;
                for (var i = 0; i < _assignedToCombo.Items.Count; i++)
                {
                    if (((ComboItem)_assignedToCombo.Items[i]!).Id == assignedId)
                    {
                        _assignedToCombo.SelectedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                _statusCombo.SelectedItem = EquipmentStatus.Available;
                _assignedToCombo.SelectedIndex = 0;
            }

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show("Equipment Name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_statusCombo.SelectedItem is not EquipmentStatus status)
            {
                MessageBox.Show("Select a status.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            EquipmentName = _nameBox.Text.Trim();
            Status = status;
            AssignedToUserId = _assignedToCombo.SelectedItem is ComboItem item && item.Id > 0 ? item.Id : null;
            Notes = string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim();

            DialogResult = DialogResult.OK;
            Close();
        }

        private sealed record ComboItem(int Id, string Display)
        {
            public override string ToString() => Display;
        }
    }
}
