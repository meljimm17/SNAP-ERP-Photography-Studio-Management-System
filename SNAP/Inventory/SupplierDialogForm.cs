using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;

namespace SNAP
{
    /// <summary>
    /// Add/Edit dialog for a Supplier. Suppliers are managed from within the Inventory Management
    /// screen rather than as their own sidebar module.
    /// </summary>
    public class SupplierDialogForm : Form
    {
        private readonly RoundedTextBox _nameBox = new() { PlaceholderText = "Supplier Name (text)" };
        private readonly RoundedTextBox _contactPersonBox = new() { PlaceholderText = "Contact Person (text, optional)" };
        private readonly RoundedTextBox _contactNumberBox = new() { PlaceholderText = "Contact Number (text, optional)" };
        private readonly RoundedTextBox _notesBox = new() { PlaceholderText = "Notes (text, optional)" };

        private readonly RoundedButton _saveButton = new() { Text = "Save Supplier" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public string SupplierName { get; private set; } = string.Empty;
        public string? ContactPerson { get; private set; }
        public string? ContactNumber { get; private set; }
        public string? Notes { get; private set; }

        public SupplierDialogForm(Supplier? existing)
        {
            Text = existing is null ? "Add Supplier" : "Edit Supplier";
            Width = 510;
            Height = 480;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 400;
            _nameBox.Width = width;
            _contactPersonBox.Width = width;
            _contactNumberBox.Width = width;
            _notesBox.Width = width;
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            Controls.Add(AppTheme.BuildDialogLayout(_saveButton, _cancelButton,
                AppTheme.BuildFieldGroup("Supplier Name", _nameBox),
                AppTheme.BuildFieldGroup("Contact Person", _contactPersonBox),
                AppTheme.BuildFieldGroup("Contact Number", _contactNumberBox),
                AppTheme.BuildFieldGroup("Notes", _notesBox)));

            if (existing is not null)
            {
                _nameBox.Text = existing.SupplierName;
                _contactPersonBox.Text = existing.ContactPerson ?? string.Empty;
                _contactNumberBox.Text = existing.ContactNumber ?? string.Empty;
                _notesBox.Text = existing.Notes ?? string.Empty;
            }

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show("Supplier Name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SupplierName = _nameBox.Text.Trim();
            ContactPerson = string.IsNullOrWhiteSpace(_contactPersonBox.Text) ? null : _contactPersonBox.Text.Trim();
            ContactNumber = string.IsNullOrWhiteSpace(_contactNumberBox.Text) ? null : _contactNumberBox.Text.Trim();
            Notes = string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text.Trim();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
