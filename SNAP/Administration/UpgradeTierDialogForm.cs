using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.enums;

namespace SNAP
{
    /// <summary>
    /// Lets a tenant Admin pick a higher tier and submit an upgrade request - the same three-card
    /// picker Add Company and Change Plan / Renewal use, so "what do I get" looks identical
    /// everywhere in the app. This only records a request (see Company.RequestedPlanTier's
    /// remarks); nothing about the company's actual access changes until a SuperAdmin reviews and
    /// applies it. Each tier has its own Terms & Conditions text, shown for whichever card is
    /// currently selected - unlike Add Company's picker, which only ever shows one company-wide
    /// agreement since the tier isn't the thing changing there.
    /// </summary>
    public class UpgradeTierDialogForm : Form
    {
        private static readonly Dictionary<PlanTier, string> TierTerms = new()
        {
            [PlanTier.Micro] =
                "Micro Tier Terms\n\n" +
                "1. Single branch only - Branch Management isn't available at this tier, and " +
                "Inventory/Equipment/Finance modules are not included.\n\n" +
                "2. Estimated at ~₱999/month. Downgrading to Micro from a higher tier hides " +
                "the modules above rather than deleting their data - it becomes accessible again " +
                "if you upgrade back.\n\n" +
                "3. Billing changes take effect on the next cycle after a SuperAdmin approves the change.",

            [PlanTier.Small] =
                "Small Tier Terms\n\n" +
                "1. Everything in Micro, plus Inventory Management, Equipment Management, and " +
                "Finance Management become available company-wide.\n\n" +
                "2. Still single-branch - Branch Management is not included at this tier.\n\n" +
                "3. Estimated at ~₱1,999/month. Billing changes take effect on the next cycle " +
                "after a SuperAdmin approves the change.",

            [PlanTier.Medium] =
                "Medium Tier Terms\n\n" +
                "1. Everything in Small, plus Branch Management - add, edit and run multiple " +
                "physical locations under one account, each with its own Inventory, Equipment and " +
                "branch-scoped staff.\n\n" +
                "2. Existing single-branch data is automatically attributed to one Main Branch on " +
                "upgrade - nothing is lost or needs re-entering.\n\n" +
                "3. Estimated at ~₱3,499/month. Billing changes take effect on the next cycle " +
                "after a SuperAdmin approves the change."
        };

        private readonly TierPickerControl _tierPicker = new();
        private readonly TextBox _termsBox = new()
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            ForeColor = AppTheme.TextPrimaryColor,
            Font = AppTheme.SecondaryFont,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = AppTheme.BackgroundColor
        };
        private readonly CheckBox _termsCheckBox = new() { Text = "I agree to the selected tier's Terms & Conditions", AutoSize = true };

        private readonly RoundedButton _saveButton = new() { Text = "Request Upgrade" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public PlanTier RequestedTier { get; private set; }

        public UpgradeTierDialogForm(PlanTier currentTier)
        {
            Text = "Request a Tier Upgrade";
            Width = 700;
            Height = 920;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            _tierPicker.Width = 596;
            _tierPicker.Height = 300;

            _termsBox.Width = 596;
            _termsBox.Height = 180;

            _saveButton.Width = 190;
            _saveButton.Height = 44;
            _cancelButton.Width = 190;
            _cancelButton.Height = 44;

            _tierPicker.SelectionChanged += (_, _) =>
            {
                _termsBox.Text = TierTerms.TryGetValue(_tierPicker.SelectedTier, out var terms) ? terms : string.Empty;
                _termsCheckBox.Checked = false;
            };

            Controls.Add(AppTheme.BuildDialogLayout(_saveButton, _cancelButton,
                AppTheme.BuildFieldGroup("Choose a tier to upgrade to", _tierPicker),
                AppTheme.BuildFieldGroup("Terms & Conditions for the selected tier", _termsBox),
                _termsCheckBox));

            // Start on whichever tier sits just above the current one - the common case ("the
            // next tier up") needs no extra clicking, but every card stays selectable in case the
            // company wants to jump straight to Medium from Micro.
            var startingTier = currentTier switch
            {
                PlanTier.Micro => PlanTier.Small,
                _ => PlanTier.Medium
            };
            _tierPicker.SetInitialTier(startingTier);

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save(currentTier);
        }

        private void Save(PlanTier currentTier)
        {
            if (_tierPicker.SelectedTier <= currentTier)
            {
                MessageBox.Show($"You're already on {currentTier} - pick a tier above your current plan.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_termsCheckBox.Checked)
            {
                MessageBox.Show("You must agree to the selected tier's Terms & Conditions before requesting it.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RequestedTier = _tierPicker.SelectedTier;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
