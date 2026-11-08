using System;
using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.enums;

namespace SNAP
{
    /// <summary>
    /// Dialog for updating a company's subscription plan tier and renewal date.
    /// </summary>
    public class SubscriptionDialogForm : Form
    {
        private readonly RoundedTextBox _companyNameBox = new() { ReadOnly = true, Enabled = false };
        private readonly TierPickerControl _tierPicker = new();
        private readonly CheckBox _hasRenewalCheck = new()
        {
            Text = "Enable renewal date",
            ForeColor = AppTheme.TextPrimaryColor,
            Font = AppTheme.BodyFont,
            AutoSize = false,
            UseMnemonic = false
        };
        private readonly DateTimePicker _renewalPicker = new()
        {
            Format = DateTimePickerFormat.Short,
            Font = AppTheme.BodyFont
        };

        private readonly RoundedButton _saveButton = new() { Text = "Update Subscription" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public PlanTier PlanTier { get; private set; }
        public DateTime? RenewalDate { get; private set; }

        public SubscriptionDialogForm(string companyName, string currentPlanTier, DateTime? currentRenewalDate)
        {
            Text = $"Manage Subscription - {companyName}";
            Width = 700;
            Height = 620;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 410;
            _companyNameBox.Width = width;
            _companyNameBox.Text = companyName;

            _tierPicker.Width = 596;
            _tierPicker.Height = 300;
            _tierPicker.SetInitialTier(
                Enum.TryParse<PlanTier>(currentPlanTier, true, out var parsedTier) ? parsedTier : PlanTier.Micro);

            _hasRenewalCheck.Width = width;
            _hasRenewalCheck.Height = 26;
            _hasRenewalCheck.Margin = new Padding(0, 0, 0, 8);
            _hasRenewalCheck.Checked = currentRenewalDate.HasValue;

            _renewalPicker.Width = width;
            _renewalPicker.Height = 32;
            _renewalPicker.Value = currentRenewalDate ?? DateTime.Today.AddYears(1);
            _renewalPicker.Enabled = currentRenewalDate.HasValue;

            _hasRenewalCheck.CheckedChanged += (_, _) =>
            {
                _renewalPicker.Enabled = _hasRenewalCheck.Checked;
            };

            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            Controls.Add(AppTheme.BuildDialogLayout(_saveButton, _cancelButton,
                AppTheme.BuildFieldGroup("Company", _companyNameBox),
                AppTheme.BuildFieldGroup("Plan Tier - choose what this company can access", _tierPicker),
                _hasRenewalCheck,
                AppTheme.BuildFieldGroup("Renewal Date", _renewalPicker)));

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void Save()
        {
            PlanTier = _tierPicker.SelectedTier;
            RenewalDate = _hasRenewalCheck.Checked ? _renewalPicker.Value.Date : null;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
