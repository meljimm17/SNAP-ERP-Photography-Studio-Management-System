using System.Collections.Generic;
using System.Windows.Forms;
using SNAP.Controls;
using SNAP.domain.dtos;

namespace SNAP
{
    /// <summary>
    /// "Sync Now" - reports how many local-first writes are still waiting to reach this tenant's
    /// MonsterASP remote, and lets Admin/SuperAdmin force a flush attempt on demand instead of only
    /// ever finding out whether it worked by making an unrelated save and hoping (every write
    /// already triggers this same push in the background by itself - see TenantSyncService). The
    /// grid below the cards names each pending row in plain terms (e.g. "Inventory: Canon
    /// Battery") rather than leaving "6 pending" as the only thing anyone can see.
    /// </summary>
    public class DataSyncForm : UserControl
    {
        private readonly Label _pendingValue = BuildValueLabel();
        private readonly Label _remoteValue = BuildValueLabel();
        private readonly Label _resultValue = BuildValueLabel();
        private readonly RoundedButton _syncNow = new() { Text = "Sync Now" };
        private readonly DataGridView _pendingGrid = new();

        // The list is otherwise a snapshot from when this screen opened, so a row that the
        // background push (or AutoSyncService) sent seconds later would keep showing as waiting
        // even though it's already in MonsterASP. Ticks on the UI thread, so it can never overlap
        // a Sync Now in progress.
        private readonly System.Windows.Forms.Timer _autoRefresh = new() { Interval = 5000 };

        public DataSyncForm()
        {
            Dock = DockStyle.Fill;
            BackColor = AppTheme.WhiteColor;

            var note = new Label
            {
                Text = "Every save already tries this in the background by itself. Use this when you want to\n" +
                       "confirm it actually went through, rather than waiting on an unrelated save to trigger it.",
                ForeColor = AppTheme.TextSecondaryColor,
                Font = AppTheme.SecondaryFont,
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 12, 0, 0)
            };

            var cards = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 8, 0, 0),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.WhiteColor
            };
            cards.Controls.Add(BuildCard("Pending Rows", _pendingValue));
            cards.Controls.Add(BuildCard("Remote", _remoteValue));
            cards.Controls.Add(BuildCard("Last Sync Result", _resultValue));

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(16, 8, 0, 8),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = AppTheme.WhiteColor
            };
            _syncNow.Width = 140;
            _syncNow.Click += (_, _) => SyncNow();
            toolbar.Controls.Add(_syncNow);

            var pendingLabel = new Label
            {
                Text = "Waiting to Reach MonsterASP",
                ForeColor = AppTheme.TextPrimaryColor,
                Font = new System.Drawing.Font("Segoe UI", 11, System.Drawing.FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(16, 6, 0, 0)
            };

            AppTheme.StyleGrid(_pendingGrid);
            _pendingGrid.Dock = DockStyle.Fill;
            _pendingGrid.ReadOnly = true;
            _pendingGrid.AutoGenerateColumns = false;
            _pendingGrid.AllowUserToAddRows = false;
            _pendingGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _pendingGrid.MultiSelect = false;
            _pendingGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "EntityType", DataPropertyName = "EntityType", HeaderText = "Type", FillWeight = 80 });
            _pendingGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Description", DataPropertyName = "Description", HeaderText = "What It Is", FillWeight = 200 });
            _pendingGrid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "CreatedAt",
                DataPropertyName = "CreatedAt",
                HeaderText = "Saved Locally At",
                FillWeight = 110,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "MMM d, yyyy h:mm tt" }
            });

            var pendingSection = new Panel { Dock = DockStyle.Fill, BackColor = AppTheme.WhiteColor, Padding = new Padding(16, 0, 16, 16) };
            var pendingLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = AppTheme.WhiteColor };
            pendingLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            pendingLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            pendingLayout.Controls.Add(pendingLabel, 0, 0);
            pendingLayout.Controls.Add(_pendingGrid, 0, 1);
            pendingSection.Controls.Add(pendingLayout);

            // Stacked via TableLayoutPanel, each row Fill within its own cell, rather than raw
            // Dock=Top siblings - this runtime's Dock/AutoSize interplay across plain sibling
            // stacking is unreliable (see AppTheme.BuildFieldGroup's remarks on the same quirk).
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = AppTheme.WhiteColor };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, AppTheme.ToolbarRowHeight));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 124));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(note, 0, 0);
            layout.Controls.Add(toolbar, 0, 1);
            layout.Controls.Add(cards, 0, 2);
            layout.Controls.Add(pendingSection, 0, 3);
            Controls.Add(layout);

            LoadStatus();

            _autoRefresh.Tick += (_, _) => RefreshSilently();
            _autoRefresh.Start();
            Disposed += (_, _) => _autoRefresh.Dispose();
        }

        /// <summary>
        /// The timer's refresh: same data as LoadStatus, but never raises a dialog - a dropped
        /// API connection shouldn't pop a message box every five seconds. Skipped while this tab
        /// isn't on screen, and leaves "Last Sync Result" alone so a Sync Now outcome stays put.
        /// </summary>
        private void RefreshSilently()
        {
            if (!Visible || !_syncNow.Enabled) return;

            try
            {
                var status = ApiClient.Get<SyncStatusDto>("/api/sync/status");
                if (status is null) return;
                _pendingValue.Text = status.StillPending.ToString();
                _remoteValue.Text = status.RemoteConfigured ? "Configured" : "Not set up";
                LoadPendingGrid();
            }
            catch
            {
                // Keep showing the last good values; the next tick tries again.
            }
        }

        private static Label BuildValueLabel() => new()
        {
            Text = "—",
            ForeColor = AppTheme.TextPrimaryColor,
            // Smaller than the card title's implied emphasis would suggest, but this label has to
            // fit two-line values like "Remote unreachable" or "No remote for this tenant" without
            // the second line getting silently clipped by the card's fixed height - a bug this
            // exact font/height pairing previously had.
            Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold),
            AutoSize = false,
            Width = 162,
            Height = 56,
            Location = new System.Drawing.Point(14, 34),
            TextAlign = System.Drawing.ContentAlignment.TopLeft
        };

        private static RoundedPanel BuildCard(string title, Label valueLabel)
        {
            var card = new RoundedPanel
            {
                Width = 190,
                Height = 106,
                BackColor = AppTheme.WhiteColor,
                Padding = new Padding(14),
                Margin = new Padding(0, 8, 16, 8)
            };

            var titleLabel = new Label
            {
                Text = title,
                ForeColor = AppTheme.TextSecondaryColor,
                Font = AppTheme.SecondaryFont,
                AutoSize = false,
                Width = 162,
                Height = 20,
                Location = new System.Drawing.Point(14, 12)
            };

            card.Controls.Add(titleLabel);
            card.Controls.Add(valueLabel);
            return card;
        }

        private void LoadStatus()
        {
            try
            {
                var status = ApiClient.Get<SyncStatusDto>("/api/sync/status");
                ApplyStatus(status, isResultOfSync: false);
                LoadPendingGrid();
            }
            catch (System.Exception ex)
            {
                _pendingValue.Text = "—";
                _remoteValue.Text = "—";
                _resultValue.Text = "Error";
                MessageBox.Show($"Could not check sync status:\n\n{ex.Message}", "Data Sync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void LoadPendingGrid()
        {
            try
            {
                _pendingGrid.DataSource = ApiClient.Get<List<SyncPendingRowDto>>("/api/sync/pending");
            }
            catch
            {
                // Status cards already report the pending count and any connection error - the
                // grid just stays at whatever it last showed rather than raising a second dialog.
            }
        }

        private void SyncNow()
        {
            _syncNow.Enabled = false;
            _resultValue.Text = "Syncing…";
            try
            {
                var status = ApiClient.Post<SyncStatusDto>("/api/sync/now");
                ApplyStatus(status, isResultOfSync: true);
                LoadPendingGrid();

                // The card only has room for a two-word verdict - the remote's own message is what
                // actually says which row is stuck and why.
                if (status is { StillPending: > 0, LastError: { Length: > 0 } error })
                {
                    MessageBox.Show($"{status.StillPending} row(s) could not be synced yet:\n\n{error}",
                        "Data Sync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (System.Exception ex)
            {
                _resultValue.Text = "Failed";
                MessageBox.Show($"Sync attempt failed:\n\n{ex.Message}", "Data Sync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _syncNow.Enabled = true;
            }
        }

        private void ApplyStatus(SyncStatusDto? status, bool isResultOfSync)
        {
            if (status is null) return;

            _pendingValue.Text = status.StillPending.ToString();
            _remoteValue.Text = status.RemoteConfigured ? "Configured" : "Not set up";

            _resultValue.Text = !status.RemoteConfigured
                ? "No remote for this tenant"
                : !isResultOfSync
                    ? (status.TotalPending == 0 ? "Nothing pending" : $"{status.TotalPending} waiting")
                    : status.TotalPending == 0
                        ? "Nothing to sync"
                        : status.Synced == status.TotalPending
                            ? $"Synced all {status.Synced}"
                            : !status.RemoteReachable
                                ? "Remote unreachable"
                                : status.Synced == 0
                                    ? "Rejected by remote"
                                    : $"Synced {status.Synced} of {status.TotalPending}";
        }
    }
}
