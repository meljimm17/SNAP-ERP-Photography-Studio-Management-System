using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.dtos;
using SNAP.domain.enums;

namespace SNAP
{
    /// <summary>
    /// Read-only view of the tenant's own subscription - plan tier, billing status, and next
    /// renewal date, so a company can monitor its own account without contacting SuperAdmin.
    /// There is nothing to edit here on purpose: changing plan or billing state stays SuperAdmin-
    /// only, through the Master Panel's Subscriptions tab (SystemAdminCompanyForm).
    /// </summary>
    public class SubscriptionForm : UserControl
    {
        private readonly FlowLayoutPanel _cards = new()
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = AppTheme.BackgroundColor
        };

        private readonly RoundedButton _upgradeButton = new() { Text = "Upgrade Tier", Width = 150 };
        private PlanTier _currentTier;

        public SubscriptionForm(AccessLevel accessLevel = AccessLevel.View)
        {
            Dock = DockStyle.Fill;
            BackColor = AppTheme.BackgroundColor;

            // Requesting an upgrade is an Admin-only business decision (the API enforces this too -
            // see SubscriptionController.RequestUpgrade) - everyone else who can reach this screen
            // at all (e.g. FinanceManager, who only has View) can see the plan but not request a
            // change to it.
            _upgradeButton.Visible = AppSession.CurrentRole == SystemRole.Admin;
            _upgradeButton.Click += (_, _) => RequestUpgrade();

            var header = AppTheme.CreatePageHeader("Subscription", "Your company's current plan and billing status.", _upgradeButton);
            Controls.Add(AppTheme.BuildPage(header, null, _cards));

            LoadData();
        }

        private void LoadData()
        {
            try
            {
                var sub = ApiClient.Get<CompanyRow>("/api/subscription/me");
                if (sub is null) return;

                _cards.Controls.Clear();

                var isSuspended = sub.SubscriptionStatus == nameof(SubscriptionStatus.Suspended);
                _currentTier = Enum.TryParse<PlanTier>(sub.PlanTier, out var tier) ? tier : PlanTier.Micro;
                _upgradeButton.Enabled = _currentTier != PlanTier.Medium;

                _cards.Controls.Add(AppTheme.CreateStatCard("Company", $"{sub.CompanyName}\n({sub.CompanyCode})"));
                _cards.Controls.Add(AppTheme.CreateStatCard("Plan Tier", sub.PlanTier));
                _cards.Controls.Add(AppTheme.CreateStatCard("Status", sub.SubscriptionStatus,
                    isSuspended ? AppTheme.DangerColor : AppTheme.SuccessColor));
                _cards.Controls.Add(AppTheme.CreateStatCard("Started", sub.SubscriptionStartDate.ToString("MMM d, yyyy")));
                _cards.Controls.Add(AppTheme.CreateStatCard("Next Renewal",
                    sub.SubscriptionRenewalDate?.ToString("MMM d, yyyy") ?? "Not set"));

                if (sub.RequestedPlanTier is not null)
                {
                    _cards.Controls.Add(AppTheme.CreateStatCard("Upgrade Requested",
                        $"{sub.RequestedPlanTier}\n(on {sub.UpgradeRequestedAt:MMM d, yyyy})", AppTheme.InfoColor));
                }

                if (isSuspended)
                {
                    var notice = new Label
                    {
                        Text = "Your subscription is suspended. Users will be unable to log in until it is reactivated - contact support to resolve this.",
                        ForeColor = AppTheme.DangerColor,
                        Font = AppTheme.SecondaryFont,
                        AutoSize = false,
                        Width = 620,
                        Height = 40,
                        Margin = new Padding(0, 4, 0, 0)
                    };
                    _cards.Controls.Add(notice);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading subscription:\n\n{ex.Message}", "Subscription", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RequestUpgrade()
        {
            try
            {
                using var dialog = new UpgradeTierDialogForm(_currentTier);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                ApiClient.Post($"/api/subscription/request-upgrade", new { RequestedTier = dialog.RequestedTier });
                LoadData();

                MessageBox.Show(
                    $"Upgrade to {dialog.RequestedTier} requested. A SuperAdmin will review and apply it.",
                    "Upgrade Requested", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not request this upgrade:\n\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
