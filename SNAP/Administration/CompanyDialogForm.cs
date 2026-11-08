using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.entities;
using SNAP.domain.enums;

namespace SNAP
{
    /// <summary>
    /// Add/Edit dialog for provisioning a Company together with its CompanyDatabase record
    /// (server/database name for the tenant's own database), including the subscription PlanTier -
    /// picked from the three-card TierPickerControl (what each plan includes, side by side) rather
    /// than a bare combo box entry.
    /// </summary>
    public class CompanyDialogForm : Form
    {
        private const string TermsAndConditionsText =
            "SNAP ERP Subscription Terms & Conditions (draft)\n\n" +
            "1. Billing is monthly in advance, based on the plan tier selected above. Prices shown " +
            "on the plan cards are estimates and subject to change before a formal rate card is " +
            "published.\n\n" +
            "2. Upgrading or downgrading a plan tier takes effect on the next billing cycle. " +
            "Downgrading to a tier that no longer includes a module (e.g. moving a Small-tier " +
            "company that uses Inventory down to Micro) does not delete that module's existing " +
            "data - it simply becomes inaccessible until the account is upgraded again.\n\n" +
            "3. The company is responsible for the accuracy of data entered into the system. SNAP " +
            "ERP provides local-first storage with best-effort background sync to the company's own " +
            "hosted database; it is not a substitute for the company's own backup practices.\n\n" +
            "4. An account may be suspended for non-payment or for violating acceptable-use terms. " +
            "A suspended account's data is retained, not deleted, pending reactivation.\n\n" +
            "5. This is placeholder text for the Admin Panel's company provisioning flow and does " +
            "not constitute a binding agreement until replaced with final legal terms.";

        private readonly RoundedTextBox _companyCodeBox = new() { PlaceholderText = "Company Code (text, e.g. tenant_a)" };
        private readonly RoundedTextBox _companyNameBox = new() { PlaceholderText = "Company Name (text)" };
        private readonly TierPickerControl _tierPicker = new();
        private readonly RoundedTextBox _serverNameBox = new() { PlaceholderText = "Local Database Server (e.g. .\\SNAPERP)" };
        private readonly RoundedTextBox _databaseNameBox = new() { PlaceholderText = "Local Database Name (e.g. TenantErp_TenantD)" };

        // Optional - a company with none of these filled in simply doesn't sync anywhere, the
        // same meaning a tenant with no "..._Remote" appsettings.json entry used to have. Filling
        // all four in is what gives a company real local+MonsterASP dual hosting without anyone
        // hand-editing a config file (see ConnectionStrings.TryResolveRemoteFromCompanyDatabase).
        private readonly RoundedTextBox _remoteServerBox = new() { PlaceholderText = "Remote Server (e.g. dbXXXXX.public.databaseasp.net)" };
        private readonly RoundedTextBox _remoteDatabaseBox = new() { PlaceholderText = "Remote Database Name (e.g. dbXXXXX)" };
        private readonly RoundedTextBox _remoteUsernameBox = new() { PlaceholderText = "Remote Username" };
        private readonly RoundedTextBox _remotePasswordBox = new() { PlaceholderText = "Remote Password", UseSystemPasswordChar = true };

        // Only shown when adding a new company - an existing one already has its own users.
        // Pre-filled with a usable default so leaving them as-is also works ("just a default user
        // and password"); the new company's Admin is expected to change this password after
        // logging in for the first time.
        private readonly RoundedTextBox _adminUsernameBox = new() { PlaceholderText = "Admin Username" };
        private readonly RoundedTextBox _adminPasswordBox = new() { PlaceholderText = "Admin Password", UseSystemPasswordChar = true };

        private readonly CheckBox _termsCheckBox = new() { Text = "I agree to the Terms & Conditions", AutoSize = true };
        private readonly LinkLabel _termsLink = new() { Text = "View Terms & Conditions", AutoSize = true };

        private readonly RoundedButton _saveButton = new() { Text = "Save Company" };
        private readonly RoundedButton _cancelButton = new() { Text = "Cancel" };

        private readonly bool _isAddMode;

        public string CompanyCode { get; private set; } = string.Empty;
        public string CompanyDisplayName { get; private set; } = string.Empty;
        public PlanTier PlanTier { get; private set; }
        public string ServerName { get; private set; } = string.Empty;
        public string DatabaseName { get; private set; } = string.Empty;

        /// <summary>All four empty means "this company doesn't sync anywhere" - a valid, normal state.</summary>
        public string? RemoteServerName { get; private set; }
        public string? RemoteDatabaseName { get; private set; }
        public string? RemoteUsername { get; private set; }
        public string? RemotePassword { get; private set; }

        /// <summary>Only meaningful when adding a new company - empty on an edit.</summary>
        public string AdminUsername { get; private set; } = string.Empty;
        public string AdminPassword { get; private set; } = string.Empty;

        public CompanyDialogForm(Company? existingCompany, CompanyDatabase? existingDatabase)
        {
            _isAddMode = existingCompany is null;

            Text = existingCompany is null ? "Add Company" : $"Edit Company - {existingCompany.CompanyName}";
            Width = 700;
            Height = _isAddMode ? 1040 : 960;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = AppTheme.WhiteColor;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            const int width = 420;
            _companyCodeBox.Width = width;
            _companyNameBox.Width = width;
            _tierPicker.Width = 596;
            _tierPicker.Height = 300;
            _serverNameBox.Width = width;
            _databaseNameBox.Width = width;
            _remoteServerBox.Width = width;
            _remoteDatabaseBox.Width = width;
            _remoteUsernameBox.Width = width;
            _remotePasswordBox.Width = width;
            _adminUsernameBox.Width = width;
            _adminPasswordBox.Width = width;
            _saveButton.Width = 200;
            _saveButton.Height = 44;
            _cancelButton.Width = 200;
            _cancelButton.Height = 44;

            _termsLink.LinkClicked += (_, _) => MessageBox.Show(TermsAndConditionsText, "Terms & Conditions", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Fixed Width/Height rather than AutoSize - a FlowLayoutPanel nested inside the dialog
            // body's own FlowLayoutPanel (BuildDialogBody) doesn't reliably self-measure on this
            // runtime (see AppTheme.BuildFieldGroup's remarks on the same Dock/AutoSize quirk),
            // which was letting the scrollable area under-reserve space for this row - it rendered,
            // but right at the very edge of the scroll extent, reading as "cut off".
            var termsRow = new FlowLayoutPanel
            {
                Width = 560,
                Height = 30,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0, 0, 0, 14)
            };
            _termsCheckBox.Margin = new Padding(0, 4, 16, 0);
            _termsLink.Margin = new Padding(0, 6, 0, 0);
            termsRow.Controls.Add(_termsCheckBox);
            termsRow.Controls.Add(_termsLink);

            // Trailing spacer so the last real control (the terms row, or the admin password field
            // when not in add mode) always has visible breathing room above the scroll extent's
            // true end, rather than sitting flush against it.
            var bottomSpacer = new Panel { Width = 560, Height = 24 };

            var sections = new System.Collections.Generic.List<Control>
            {
                AppTheme.BuildFieldGroup("Company Code", _companyCodeBox),
                AppTheme.BuildFieldGroup("Company Name", _companyNameBox),
                AppTheme.BuildFieldGroup("Plan Tier - choose what this company can access", _tierPicker),
                AppTheme.BuildFieldGroup("Local Database Server", _serverNameBox),
                AppTheme.BuildFieldGroup("Local Database Name", _databaseNameBox),
                AppTheme.BuildFieldGroup("Remote (MonsterASP) Server - optional, leave blank to skip online sync for this company", _remoteServerBox),
                AppTheme.BuildFieldGroup("Remote Database Name", _remoteDatabaseBox),
                AppTheme.BuildFieldGroup("Remote Username", _remoteUsernameBox),
                AppTheme.BuildFieldGroup("Remote Password", _remotePasswordBox),
            };

            if (_isAddMode)
            {
                // A freshly provisioned company has no way to log in otherwise - its tenant
                // database starts with zero TenantUsers. These defaults are usable as-is; the new
                // company's Admin is expected to change the password after signing in once.
                _adminUsernameBox.Text = "admin";
                _adminPasswordBox.Text = "Welcome123!";
                sections.Add(AppTheme.BuildFieldGroup("Admin Username (for the company's first login)", _adminUsernameBox));
                sections.Add(AppTheme.BuildFieldGroup("Admin Password", _adminPasswordBox));
            }

            sections.Add(termsRow);
            sections.Add(bottomSpacer);

            Controls.Add(AppTheme.BuildDialogLayout(_saveButton, _cancelButton, sections.ToArray()));

            if (existingCompany is not null)
            {
                _companyCodeBox.Text = existingCompany.CompanyCode;
                _companyNameBox.Text = existingCompany.CompanyName;
                _tierPicker.SetInitialTier(existingCompany.PlanTier);
            }
            else
            {
                _tierPicker.SetInitialTier(PlanTier.Micro);
            }

            if (existingDatabase is not null)
            {
                _serverNameBox.Text = existingDatabase.ServerName;
                _databaseNameBox.Text = existingDatabase.DatabaseName;
                _remoteServerBox.Text = existingDatabase.RemoteServerName;
                _remoteDatabaseBox.Text = existingDatabase.RemoteDatabaseName;
                _remoteUsernameBox.Text = existingDatabase.RemoteUsername;
                _remotePasswordBox.Text = existingDatabase.RemotePassword;
            }
            else
            {
                // Every tenant database in this project lives on the same local named instance -
                // Tenant A/B/C all use this exact value. Pre-filling it removes the one typo that
                // actually broke a real attempt (someone typed "SNAPERP" without the leading ".\",
                // which isn't a resolvable hostname and failed to connect) - left editable in case
                // a future setup genuinely needs a different server.
                _serverNameBox.Text = @".\SNAPERP";
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
                MessageBox.Show("Local Database Server and Database Name are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var remoteFields = new[] { _remoteServerBox.Text, _remoteDatabaseBox.Text, _remoteUsernameBox.Text, _remotePasswordBox.Text };
            var remoteFieldsFilled = remoteFields.Count(f => !string.IsNullOrWhiteSpace(f));
            if (remoteFieldsFilled > 0 && remoteFieldsFilled < remoteFields.Length)
            {
                MessageBox.Show("Fill in all four Remote fields to enable sync for this company, or leave all four blank to skip it.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_isAddMode && (string.IsNullOrWhiteSpace(_adminUsernameBox.Text) || string.IsNullOrWhiteSpace(_adminPasswordBox.Text)))
            {
                MessageBox.Show("Admin Username and Admin Password are required so the company has a way to log in.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_termsCheckBox.Checked)
            {
                MessageBox.Show("You must agree to the Terms & Conditions before saving.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            CompanyCode = _companyCodeBox.Text.Trim();
            CompanyDisplayName = _companyNameBox.Text.Trim();
            PlanTier = _tierPicker.SelectedTier;
            ServerName = _serverNameBox.Text.Trim();
            DatabaseName = _databaseNameBox.Text.Trim();
            RemoteServerName = string.IsNullOrWhiteSpace(_remoteServerBox.Text) ? null : _remoteServerBox.Text.Trim();
            RemoteDatabaseName = string.IsNullOrWhiteSpace(_remoteDatabaseBox.Text) ? null : _remoteDatabaseBox.Text.Trim();
            RemoteUsername = string.IsNullOrWhiteSpace(_remoteUsernameBox.Text) ? null : _remoteUsernameBox.Text.Trim();
            RemotePassword = string.IsNullOrWhiteSpace(_remotePasswordBox.Text) ? null : _remotePasswordBox.Text;
            AdminUsername = _adminUsernameBox.Text.Trim();
            AdminPassword = _adminPasswordBox.Text;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
