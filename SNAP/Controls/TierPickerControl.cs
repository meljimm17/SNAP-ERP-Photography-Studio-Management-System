using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SNAP.domain.enums;

namespace SNAP.Controls
{
    /// <summary>
    /// Netflix-style plan picker for Add/Edit Company - three side-by-side cards (Micro/Small/
    /// Medium), each naming that tier's included modules, a short description, and an estimated
    /// monthly price, so a SuperAdmin picks a plan by seeing what it actually includes rather than
    /// a bare combo box entry. Module lists mirror MainDashboardForm's own tier gating exactly
    /// (Inventory/Equipment/Finance unlock at Small+, Branch Management at Medium+) - keep both in
    /// sync if that gating ever changes. Pricing is a placeholder estimate, not a real rate card.
    /// </summary>
    public class TierPickerControl : UserControl
    {
        private static readonly (PlanTier Tier, string Price, string Description, string[] Modules)[] TierInfo =
        {
            (PlanTier.Micro, "~₱999/mo", "For solo photographers and small studios just getting started.",
                new[] { "Customers", "Photography Packages", "Bookings & Scheduling", "Payments", "Reports & Analytics", "User Management" }),
            (PlanTier.Small, "~₱1,999/mo", "For growing studios that need to track stock, gear, and finances.",
                new[] { "Everything in Micro, plus:", "Inventory Management", "Equipment Management", "Finance Management" }),
            (PlanTier.Medium, "~₱3,499/mo", "For multi-branch studios running several locations under one account.",
                new[] { "Everything in Small, plus:", "Branch Management", "Multi-branch operations" }),
        };

        private readonly Dictionary<PlanTier, TierCardPanel> _cards = new();

        /// <summary>Fired whenever the selected card changes, including the initial default
        /// selection made by the constructor.</summary>
        public event EventHandler? SelectionChanged;

        public PlanTier SelectedTier { get; private set; } = PlanTier.Micro;

        public TierPickerControl()
        {
            AutoSize = false;
            BackColor = AppTheme.WhiteColor;

            var row = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = AppTheme.WhiteColor
            };

            foreach (var info in TierInfo)
            {
                var tier = info.Tier;
                var card = new TierCardPanel(tier, info.Price, info.Description, info.Modules)
                {
                    Width = 182,
                    Height = 300,
                    Margin = new Padding(0, 0, 14, 0)
                };
                card.CardClicked += () => Select(tier);
                _cards[tier] = card;
                row.Controls.Add(card);
            }

            Controls.Add(row);
        }

        /// <summary>Call once after construction (and again whenever editing an existing company)
        /// to set the starting selection without treating it as a user-driven change.</summary>
        public void SetInitialTier(PlanTier tier) => Select(tier);

        public void Select(PlanTier tier)
        {
            SelectedTier = tier;
            foreach (var (candidateTier, card) in _cards)
            {
                card.IsSelected = candidateTier == tier;
            }
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// One selectable pricing card. Click handling is wired on every child label too (not just
        /// the panel itself) since a label fully covers most of the card's clickable area - without
        /// that, only the thin uncovered strip around the edges would actually respond to a click.
        /// </summary>
        private sealed class TierCardPanel : Panel
        {
            public event Action? CardClicked;

            private bool _isSelected;

            [System.ComponentModel.Browsable(false)]
            [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public bool IsSelected
            {
                get => _isSelected;
                set { _isSelected = value; Invalidate(); }
            }

            public TierCardPanel(PlanTier tier, string price, string description, string[] modules)
            {
                DoubleBuffered = true;
                Cursor = Cursors.Hand;
                BackColor = AppTheme.WhiteColor;
                Padding = new Padding(12);

                var title = new Label
                {
                    Text = tier.ToString(),
                    Font = new Font("Segoe UI", 13, FontStyle.Bold),
                    ForeColor = AppTheme.TextPrimaryColor,
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.BottomLeft,
                    BackColor = AppTheme.WhiteColor,
                    Cursor = Cursors.Hand
                };

                var priceLabel = new Label
                {
                    Text = price,
                    Font = new Font("Segoe UI", 12, FontStyle.Bold),
                    ForeColor = AppTheme.NavyColor,
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.TopLeft,
                    BackColor = AppTheme.WhiteColor,
                    Cursor = Cursors.Hand
                };

                var descLabel = new Label
                {
                    Text = description,
                    Font = AppTheme.SecondaryFont,
                    ForeColor = AppTheme.TextSecondaryColor,
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.TopLeft,
                    BackColor = AppTheme.WhiteColor,
                    Cursor = Cursors.Hand
                };

                var modulesLabel = new Label
                {
                    Text = string.Join("\n", modules.Select(m => m.EndsWith(':') ? m : $"✓ {m}")),
                    Font = AppTheme.SecondaryFont,
                    ForeColor = AppTheme.TextPrimaryColor,
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.TopLeft,
                    BackColor = AppTheme.WhiteColor,
                    Cursor = Cursors.Hand
                };

                // TableLayoutPanel with Absolute rows for the fixed-height sections, not stacked
                // Dock=Top siblings - this runtime's Dock/AutoSize interplay across plain sibling
                // stacking doesn't reserve space correctly (see AppTheme.BuildFieldGroup's remarks
                // on the same quirk).
                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 4,
                    BackColor = AppTheme.WhiteColor
                };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.Controls.Add(title, 0, 0);
                layout.Controls.Add(priceLabel, 0, 1);
                layout.Controls.Add(descLabel, 0, 2);
                layout.Controls.Add(modulesLabel, 0, 3);

                Controls.Add(layout);

                foreach (var clickable in new Control[] { this, layout, title, priceLabel, descLabel, modulesLabel })
                {
                    clickable.Click += (_, _) => CardClicked?.Invoke();
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                var borderWidth = IsSelected ? 2 : 1;
                var borderColor = IsSelected ? AppTheme.NavyColor : AppTheme.BorderColor;
                var rect = new Rectangle(borderWidth / 2, borderWidth / 2, Width - borderWidth - 1, Height - borderWidth - 1);
                using var path = AppTheme.CreateRoundedRectangle(rect, AppTheme.BorderRadius);
                using var pen = new Pen(borderColor, borderWidth);
                e.Graphics.DrawPath(pen, path);
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                using var path = AppTheme.CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), AppTheme.BorderRadius);
                Region = new Region(path);
            }
        }
    }
}
