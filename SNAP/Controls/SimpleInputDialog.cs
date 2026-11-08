using System.Drawing;
using System.Windows.Forms;

namespace SNAP.Controls
{
    /// <summary>
    /// Small reusable "type one value" prompt - e.g. the "+ Category" button next to Add/Edit
    /// Item's Category dropdown, or any future spot that needs a single short text value without
    /// the overhead of a full dedicated dialog class.
    /// </summary>
    public class SimpleInputDialog : Form
    {
        private readonly RoundedTextBox _inputBox;
        private readonly RoundedButton _saveButton;
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public string Value { get; private set; } = string.Empty;

        public SimpleInputDialog(string title, string fieldLabel, string placeholder, string saveButtonText = "Save")
        {
            _inputBox = new RoundedTextBox { PlaceholderText = placeholder };
            _saveButton = new RoundedButton { Text = saveButtonText };

            Text = title;
            Width = 460;
            Height = 280;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 380;
            _inputBox.Width = width;
            _saveButton.Width = 170;
            _saveButton.Height = 44;
            _cancelButton.Width = 170;
            _cancelButton.Height = 44;

            Controls.Add(AppTheme.BuildDialogLayout(_saveButton, _cancelButton,
                AppTheme.BuildFieldGroup(fieldLabel, _inputBox)));

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_inputBox.Text))
            {
                MessageBox.Show("Enter a value.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Value = _inputBox.Text.Trim();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
