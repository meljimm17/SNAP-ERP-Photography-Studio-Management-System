using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;

namespace SNAP
{
    /// <summary>
    /// Add/Edit dialog for provisioning a Company together with its CompanyDatabase record
    /// (server/database name for the tenant's own database), including the subscription PlanTier.
    /// </summary>
    public class CompanyDialogForm : Form
    {
        private readonly RoundedTextBox _companyCodeBox = new() { PlaceholderText = "Company Code (text, e.g. tenant_a)" };
        private readonly RoundedTextBox _companyNameBox = new() { PlaceholderText = "Company Name (text)" };
        private readonly RoundedComboBox _planTierCombo = new();
        private readonly RoundedTextBox _serverNameBox = new() { PlaceholderText = "Database Server (text, e.g. dbXXXXX.public.databaseasp.net)" };
        private readonly RoundedTextBox _databaseNameBox = new() { PlaceholderText = "Database Name (text, e.g. dbXXXXX)" };
        private readonly RoundedTextBox _credentialKeyBox = new() { PlaceholderText = "Credential Key (optional, e.g. tenant_a)" };

        private readonly RoundedButton _saveButton = new() { Text = "Save Company" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        public string CompanyCode { get; private set; } = string.Empty;
        public string CompanyDisplayName { get; private set; } = string.Empty;
        public PlanTier PlanTier { get; private set; }
        public string ServerName { get; private set; } = string.Empty;
        public string DatabaseName { get; private set; } = string.Empty;
        public string? CredentialKey { get; private set; }

        public CompanyDialogForm(Company? existingCompany, CompanyDatabase? existingDatabase)
        {
            Text = existingCompany is null ? "Add Company" : $"Edit Company - {existingCompany.CompanyName}";
            Width = 500;
            Height = 640;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 420;
            _companyCodeBox.Width = width;
            _companyNameBox.Width = width;
            _planTierCombo.Width = width;
            _serverNameBox.Width = width;
            _databaseNameBox.Width = width;
            _credentialKeyBox.Width = width;
            _saveButton.Width = 200;
            _saveButton.Height = 44;
            _cancelButton.Width = 200;
            _cancelButton.Height = 44;

            foreach (var tier in Enum.GetValues<PlanTier>()) _planTierCombo.Items.Add(tier);

            Controls.Add(AppTheme.BuildDialogBody(
                AppTheme.BuildFieldGroup("Company Code", _companyCodeBox),
                AppTheme.BuildFieldGroup("Company Name", _companyNameBox),
                AppTheme.BuildFieldGroup("Plan Tier", _planTierCombo),
                AppTheme.BuildFieldGroup("Tenant Database Server", _serverNameBox),
                AppTheme.BuildFieldGroup("Tenant Database Name", _databaseNameBox),
                AppTheme.BuildFieldGroup("Credential Key (optional)", _credentialKeyBox),
                AppTheme.BuildDialogButtonRow(_saveButton, _cancelButton)));

            if (existingCompany is not null)
            {
                _companyCodeBox.Text = existingCompany.CompanyCode;
                _companyNameBox.Text = existingCompany.CompanyName;
                _planTierCombo.SelectedItem = existingCompany.PlanTier;
            }
            else
            {
                _planTierCombo.SelectedItem = PlanTier.Micro;
            }

            if (existingDatabase is not null)
            {
                _serverNameBox.Text = existingDatabase.ServerName;
                _databaseNameBox.Text = existingDatabase.DatabaseName;
                _credentialKeyBox.Text = existingDatabase.CredentialKey;
            }

            _cancelButton.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
            _saveButton.Click += (_, _) => Save();
        }

        private void Save()
        {
            if (string.IsNullOrWhiteSpace(_companyCodeBox.Text) || string.IsNullOrWhiteSpace(_companyNameBox.Text))
            {
                MessageBox.Show("Company Code and Company Name are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(_serverNameBox.Text) || string.IsNullOrWhiteSpace(_databaseNameBox.Text))
            {
                MessageBox.Show("Tenant Database Server and Database Name are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_planTierCombo.SelectedItem is not PlanTier tier)
            {
                MessageBox.Show("Select a plan tier.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CompanyCode = _companyCodeBox.Text.Trim();
            CompanyDisplayName = _companyNameBox.Text.Trim();
            PlanTier = tier;
            ServerName = _serverNameBox.Text.Trim();
            DatabaseName = _databaseNameBox.Text.Trim();
            CredentialKey = string.IsNullOrWhiteSpace(_credentialKeyBox.Text) ? null : _credentialKeyBox.Text.Trim();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
