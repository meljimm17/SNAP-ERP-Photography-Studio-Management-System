using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.enums;
using static SNAP.EquipmentForm;

namespace SNAP
{
    /// <summary>
    /// Add/Edit dialog for a piece of Equipment.
    /// Decoupled from TenantErpDbContext - all data goes to the API via EquipmentForm.
    /// </summary>
    public class EquipmentDialogForm : Form
    {
        private readonly RoundedTextBox _nameBox = new() { PlaceholderText = "Equipment Name (text, e.g. Canon R5)" };
        private readonly RoundedComboBox _categoryCombo = new();
        private readonly RoundedTextBox _acquisitionCostBox = new() { PlaceholderText = "Acquisition Cost (decimal number, optional)" };
        private readonly RoundedComboBox _statusCombo = new();

        private readonly RoundedButton _saveButton = new() { Text = "Save Equipment" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public string EquipmentName { get; private set; } = string.Empty;
        public EquipmentCategory Category { get; private set; }
        public decimal? AcquisitionCost { get; private set; }
        public EquipmentStatus Status { get; private set; }

        internal EquipmentDialogForm(EquipmentRowForDialog? existing, int? branchId)
        {
            Text = existing is null ? "Add Equipment" : "Edit Equipment";
            Width = 510;
            Height = 480;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 400;
            _nameBox.Width = width;
            _categoryCombo.Width = width;
            _acquisitionCostBox.Width = width;
            _statusCombo.Width = width;
            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            foreach (var category in Enum.GetValues<EquipmentCategory>()) _categoryCombo.Items.Add(category);
            foreach (var status in Enum.GetValues<EquipmentStatus>()) _statusCombo.Items.Add(status);

            Controls.Add(AppTheme.BuildDialogLayout(_saveButton, _cancelButton,
                AppTheme.BuildFieldGroup("Equipment Name", _nameBox),
                AppTheme.BuildFieldGroup("Category", _categoryCombo),
                AppTheme.BuildFieldGroup("Acquisition Cost (₱, optional)", _acquisitionCostBox),
                AppTheme.BuildFieldGroup("Status", _statusCombo)));

            if (existing is not null)
            {
                _nameBox.Text = existing.EquipmentName;
                _categoryCombo.SelectedItem = Enum.TryParse<EquipmentCategory>(existing.Category, out var cat) ? cat : EquipmentCategory.PhotographyGear;
                _acquisitionCostBox.Text = existing.AcquisitionCost is null ? string.Empty : existing.AcquisitionCost.Value.ToString("0.##");
                _statusCombo.SelectedItem = Enum.TryParse<EquipmentStatus>(existing.Status, out var stat) ? stat : EquipmentStatus.Available;
            }
            else
            {
                _categoryCombo.SelectedItem = EquipmentCategory.PhotographyGear;
                _statusCombo.SelectedItem = EquipmentStatus.Available;
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

            if (_categoryCombo.SelectedItem is not EquipmentCategory category)
            {
                MessageBox.Show("Select a category.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_acquisitionCostBox.Text) &&
                (!decimal.TryParse(_acquisitionCostBox.Text, out var cost) || cost < 0))
            {
                MessageBox.Show("Enter a valid, non-negative Acquisition Cost, or leave it blank.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            EquipmentName = _nameBox.Text.Trim();
            Category = category;
            AcquisitionCost = string.IsNullOrWhiteSpace(_acquisitionCostBox.Text) ? null : decimal.Parse(_acquisitionCostBox.Text);
            Status = status;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
