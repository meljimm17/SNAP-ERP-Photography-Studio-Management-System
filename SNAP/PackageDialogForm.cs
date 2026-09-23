using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;

namespace SNAP
{
    /// <summary>
    /// Add/Edit dialog for a Photography Package with validated numeric Price/DurationHours fields.
    /// </summary>
    public class PackageDialogForm : Form
    {
        private readonly RoundedTextBox _nameBox = new() { PlaceholderText = "Package Name (text, e.g. Wedding Package)" };
        private readonly RoundedTextBox _descriptionBox = new() { PlaceholderText = "Description (text, optional)" };
        private readonly RoundedTextBox _priceBox = new() { PlaceholderText = "Price (decimal number, e.g. 2500.00)" };
        private readonly RoundedTextBox _durationBox = new() { PlaceholderText = "Duration Hours (decimal number, e.g. 1.5)" };

        private readonly RoundedButton _saveButton = new() { Text = "Save Package" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public string PackageName { get; private set; } = string.Empty;
        public string? PackageDescription { get; private set; }
        public decimal Price { get; private set; }
        public decimal DurationHours { get; private set; }

        public PackageDialogForm(PhotographyPackage? existing)
        {
            Text = existing is null ? "Add Package" : "Edit Package";
            Width = 480;
            Height = 480;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 400;
            _nameBox.Width = width;
            _descriptionBox.Width = width;
            _priceBox.Width = width;
            _durationBox.Width = width;
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            Controls.Add(AppTheme.BuildDialogBody(
                AppTheme.BuildFieldGroup("Package Name", _nameBox),
                AppTheme.BuildFieldGroup("Description", _descriptionBox),
                AppTheme.BuildFieldGroup("Price (₱)", _priceBox),
                AppTheme.BuildFieldGroup("Duration (hours)", _durationBox),
                AppTheme.BuildDialogButtonRow(_saveButton, _cancelButton)));

            if (existing is not null)
            {
                _nameBox.Text = existing.PackageName;
                _descriptionBox.Text = existing.Description ?? string.Empty;
                _priceBox.Text = existing.Price.ToString("0.##");
                _durationBox.Text = existing.DurationHours.ToString("0.##");
            }

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                MessageBox.Show("Package Name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(_priceBox.Text, out var price) || price < 0)
            {
                MessageBox.Show("Enter a valid, non-negative Price.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!decimal.TryParse(_durationBox.Text, out var duration) || duration <= 0)
            {
                MessageBox.Show("Enter a valid Duration Hours greater than zero.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            PackageName = _nameBox.Text.Trim();
            PackageDescription = string.IsNullOrWhiteSpace(_descriptionBox.Text) ? null : _descriptionBox.Text.Trim();
            Price = price;
            DurationHours = duration;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
