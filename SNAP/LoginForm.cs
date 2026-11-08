using System.Drawing;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.enums;
using SNAP.infrastructure.services;

namespace SNAP
{
    public class LoginForm : Form
    {
        private readonly RoundedTextBox txtCompanyCode;
        private readonly RoundedTextBox txtUsername;
        private readonly RoundedTextBox txtPassword;
        private readonly RoundedButton btnCompanyLogin;
        private readonly LinkLabel linkSystemAdmin;

        public LoginForm()
        {
            Text = "SNAP ERP";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = AppTheme.BackgroundColor;
            ClientSize = new Size(440, 560);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // Minimalist auth card: form fields live on a white RoundedPanel floating on the
            // app's light Background color, instead of the fields sitting directly on white.
            var card = new RoundedPanel
            {
                Width = 380,
                Height = 480,
                Location = new Point(30, 40),
                BackColor = AppTheme.WhiteColor,
                DrawShadow = false
            };

            // Logo is a 1500x500 (3:1) wordmark, not a square icon - sized to its own aspect
            // ratio (240x80) instead of being stretched/letterboxed into a mismatched box.
            var logo = new PictureBox
            {
                Image = Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.jpg")),
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = 240,
                Height = 80,
                Location = new Point(70, 32)
            };

            var subtitle = new Label
            {
                Text = "Company Login",
                Font = AppTheme.BodyFont,
                ForeColor = AppTheme.TextSecondaryColor,
                AutoSize = false,
                Width = 320,
                Height = 22,
                TextAlign = ContentAlignment.TopCenter,
                Location = new Point(30, 128)
            };

            txtCompanyCode = new RoundedTextBox
            {
                PlaceholderText = "Enter Company Code",
                Width = 320,
                Location = new Point(30, 172)
            };

            txtUsername = new RoundedTextBox
            {
                PlaceholderText = "Enter Username",
                Width = 320,
                Location = new Point(30, 231)
            };

            txtPassword = new RoundedTextBox
            {
                PlaceholderText = "Enter Password",
                UseSystemPasswordChar = true,
                Width = 320,
                Location = new Point(30, 290)
            };

            btnCompanyLogin = new RoundedButton
            {
                Text = "Sign In",
                Width = 320,
                Height = 46,
                Location = new Point(30, 361),
                Font = AppTheme.BodyFont
            };
            btnCompanyLogin.Click += OnCompanyLogin;

            linkSystemAdmin = new LinkLabel
            {
                Text = "System Admin Login",
                Font = AppTheme.SecondaryFont,
                AutoSize = false,
                Width = 320,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter,
                LinkColor = AppTheme.TextSecondaryColor,
                Location = new Point(30, 425)
            };
            linkSystemAdmin.Click += (_, _) =>
            {
                var adminLogin = new SystemAdminLoginForm();
                adminLogin.Show();
                Hide();
            };

            card.Controls.Add(logo);
            card.Controls.Add(subtitle);
            card.Controls.Add(txtCompanyCode);
            card.Controls.Add(txtUsername);
            card.Controls.Add(txtPassword);
            card.Controls.Add(btnCompanyLogin);
            card.Controls.Add(linkSystemAdmin);
            Controls.Add(card);
        }

        private void OnCompanyLogin(object sender, EventArgs e)
        {
            var companyCode = txtCompanyCode.Text.Trim();
            var username = txtUsername.Text.Trim();
            var password = txtPassword.Text;

            if (string.IsNullOrWhiteSpace(companyCode) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Company Code, Username, and Password are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Authenticates against SNAP.api's /api/auth/company-login instead of calling
            // AuthenticationService in-process: the credential/tier check itself still runs
            // AuthenticationService.TryAuthenticateCompany, just server-side, which is what
            // actually decides success/failure here.
            ApiCompanyLoginResponse loginResponse;
            // The window is frozen until the API answers - the wait cursor says so, instead of
            // inviting a second click that would land on the Dashboard (see InputQueue).
            Cursor.Current = Cursors.WaitCursor;
            try
            {
                loginResponse = ApiClient.Post<ApiCompanyLoginResponse>("/api/auth/company-login",
                    new { companyCode, username, password });
            }
            catch (Exception ex)
            {
                Cursor.Current = Cursors.Default;
                MessageBox.Show(ex.Message, "Login Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            AppSession.CurrentCompanyCode = companyCode;
            AppSession.CurrentCompanyId = loginResponse.CompanyId;
            AppSession.CurrentUsername = username;
            AppSession.CurrentRole = Enum.Parse<SystemRole>(loginResponse.Role);
            AppSession.CurrentDisplayName = loginResponse.DisplayName;
            AppSession.CurrentPlanTier = Enum.Parse<PlanTier>(loginResponse.PlanTier);
            AppSession.CurrentUserId = loginResponse.UserId;
            AppSession.CurrentApiToken = loginResponse.Token;

            // Null for a company-wide role (Admin/FinanceManager) and for every company below
            // Medium tier. Only used to decide whether a screen shows a branch picker - the API
            // re-reads the same value from the signed token, so this can't be used to widen access.
            AppSession.CurrentBranchId = loginResponse.BranchId;

            // The API already decided this login is valid - this is a second, independent lookup
            // (not a credential check) purely so modules not yet converted to call SNAP.api know
            // which physical tenant database is *this* company's. Without it, AppSession.
            // CurrentTenantConnectionString would stay null and every legacy module would silently
            // fall back to the single default TenantErp connection string - meaning a company like
            // tenant_b would see whatever company that default happens to point at instead of its
            // own data (exactly the "tenant_b shows tenant_a's bookings/payments" bug). Best-effort:
            // if it fails, those modules just fall back the same way they already did before this.
            AppSession.CurrentTenantConnectionString = AuthenticationService.ResolveTenantConnectionString(companyCode);

            TenantSyncService.CurrentActorUserId = loginResponse.UserId > 0 ? loginResponse.UserId : null;
            TenantSyncService.CurrentActorName = loginResponse.DisplayName;

            var dashboard = new MainDashboardForm();
            dashboard.Show();
            Hide();
        }
    }

    public class SystemAdminLoginForm : Form
    {
        private readonly RoundedTextBox txtUsername;
        private readonly RoundedTextBox txtPassword;
        private readonly RoundedButton btnLogin;
        private readonly LinkLabel linkCompanyLogin;

        public SystemAdminLoginForm()
        {
            Text = "SNAP ERP";
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = AppTheme.BackgroundColor;
            ClientSize = new Size(440, 500);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var card = new RoundedPanel
            {
                Width = 380,
                Height = 420,
                Location = new Point(30, 40),
                BackColor = AppTheme.WhiteColor,
                DrawShadow = false
            };

            var logo = new PictureBox
            {
                Image = Image.FromFile(Path.Combine(AppContext.BaseDirectory, "Assets", "logo.jpg")),
                SizeMode = PictureBoxSizeMode.Zoom,
                Width = 240,
                Height = 80,
                Location = new Point(70, 32)
            };

            var subtitle = new Label
            {
                Text = "Administration Portal",
                Font = AppTheme.BodyFont,
                ForeColor = AppTheme.TextSecondaryColor,
                AutoSize = false,
                Width = 320,
                Height = 22,
                TextAlign = ContentAlignment.TopCenter,
                Location = new Point(30, 128)
            };

            txtUsername = new RoundedTextBox
            {
                PlaceholderText = "Enter Admin Username",
                Width = 320,
                Location = new Point(30, 172)
            };

            txtPassword = new RoundedTextBox
            {
                PlaceholderText = "Enter Password",
                UseSystemPasswordChar = true,
                Width = 320,
                Location = new Point(30, 231)
            };

            btnLogin = new RoundedButton
            {
                Text = "System Admin Sign In",
                Width = 320,
                Height = 46,
                Location = new Point(30, 302),
                Font = AppTheme.BodyFont
            };

            linkCompanyLogin = new LinkLabel
            {
                Text = "Back to Company Login",
                Font = AppTheme.SecondaryFont,
                AutoSize = false,
                Width = 320,
                Height = 24,
                TextAlign = ContentAlignment.MiddleCenter,
                LinkColor = AppTheme.TextSecondaryColor,
                Location = new Point(30, 366)
            };
            linkCompanyLogin.Click += (_, _) =>
            {
                var companyLogin = new LoginForm();
                companyLogin.Show();
                Hide();
            };

            btnLogin.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrWhiteSpace(txtPassword.Text))
                {
                    MessageBox.Show("Username and password are required for system administration.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var result = AuthenticationService.TryAuthenticateSystemAdmin(txtUsername.Text.Trim(), txtPassword.Text);
                if (!result.Success)
                {
                    MessageBox.Show(result.Message, "System Admin Login", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                AppSession.CurrentUsername = txtUsername.Text.Trim();
                AppSession.CurrentRole = result.Role;
                AppSession.CurrentDisplayName = result.DisplayName;

                var companyForm = new SystemAdminCompanyForm();
                companyForm.Show();
                Hide();
            };

            card.Controls.Add(logo);
            card.Controls.Add(subtitle);
            card.Controls.Add(txtUsername);
            card.Controls.Add(txtPassword);
            card.Controls.Add(btnLogin);
            card.Controls.Add(linkCompanyLogin);
            Controls.Add(card);
        }
    }
}
