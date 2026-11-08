using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using SNAP.Controls;

namespace SNAP
{
    /// <summary>
    /// Classification used by <see cref="AppTheme.StyleStatusColumn"/> to pick a status dot color -
    /// kept deliberately small (four accent colors + neutral) so status coloring stays the
    /// exception rather than turning the whole grid colorful.
    /// </summary>
    public enum StatusKind
    {
        Neutral,
        Success,
        Warning,
        Danger,
        Info
    }

    public static class AppTheme
    {
        public const string Navy = "#262C38";
        public const string NavyDark = "#1D212A";
        public const string NavyLight = "#474C56";
        public const string White = "#FFFFFF";
        public const string Background = "#F6F7F9";
        public const string Border = "#E5E7EB";
        public const string TextPrimary = "#1F2937";
        public const string TextSecondary = "#6B7280";
        public const string Success = "#16A34A";
        public const string Warning = "#D97706";
        public const string Danger = "#DC2626";
        public const string Info = "#2563EB";

        public static Color NavyColor => ColorTranslator.FromHtml(Navy);
        public static Color NavyDarkColor => ColorTranslator.FromHtml(NavyDark);
        public static Color NavyLightColor => ColorTranslator.FromHtml(NavyLight);
        public static Color WhiteColor => ColorTranslator.FromHtml(White);
        public static Color SurfaceColor => WhiteColor;
        public static Color BackgroundColor => ColorTranslator.FromHtml(Background);
        public static Color BorderColor => ColorTranslator.FromHtml(Border);
        public static Color TextPrimaryColor => ColorTranslator.FromHtml(TextPrimary);
        public static Color TextSecondaryColor => ColorTranslator.FromHtml(TextSecondary);
        public static Color SuccessColor => ColorTranslator.FromHtml(Success);
        public static Color WarningColor => ColorTranslator.FromHtml(Warning);
        public static Color DangerColor => ColorTranslator.FromHtml(Danger);
        public static Color InfoColor => ColorTranslator.FromHtml(Info);

        /// <summary>Subtle light tint used for a selected/highlighted row or list item.</summary>
        public static Color SelectionTintColor => Color.FromArgb(237, 238, 241);

        // Modern UI constants
        public const int BorderRadius = 8; // Consistent radius for cards/inputs
        public const int ButtonRadius = 6; // Smaller, flatter radius specifically for buttons
        public const int TextBoxPadding = 12; // Internal padding for text inputs
        public const int ButtonHeight = 40; // Standard button height
        public const int FocusBorderWidth = 2; // Focus border thickness

        // Shadow/depth effect - kept available for the rare popup that still wants one, but no
        // longer used by default anywhere (flat + subtle border is the standard look now).
        public static Color ShadowColor => Color.FromArgb(20, 0, 0, 0);

        // Typography - centralizes the page/section/body/secondary type scale so forms stop
        // inventing their own ad-hoc font sizes. Segoe UI is the standard modern Windows UI font
        // (GenericSansSerif renders as the dated Microsoft Sans Serif on this platform).
        public static Font PageTitleFont => new("Segoe UI", 20f, FontStyle.Bold);
        public static Font SectionTitleFont => new("Segoe UI", 14f, FontStyle.Bold);
        public static Font BodyFont => new("Segoe UI", 10f, FontStyle.Regular);
        public static Font SecondaryFont => new("Segoe UI", 9f, FontStyle.Regular);

        /// <summary>
        /// Creates a rounded rectangular path for GDI+ drawing.
        /// </summary>
        public static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                return path;
            }

            // Ensure radius doesn't exceed bounds
            radius = System.Math.Max(0, System.Math.Min(radius, System.Math.Min(rect.Width, rect.Height) / 2));
            var d = radius * 2;

            if (d <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            // Top-left arc
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            // Top line
            path.AddLine(rect.X + radius, rect.Y, rect.Right - radius, rect.Y);
            // Top-right arc
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            // Right line
            path.AddLine(rect.Right, rect.Y + radius, rect.Right, rect.Bottom - radius);
            // Bottom-right arc
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            // Bottom line
            path.AddLine(rect.Right - radius, rect.Bottom, rect.X + radius, rect.Bottom);
            // Bottom-left arc
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            // Left line
            path.AddLine(rect.X, rect.Bottom - radius, rect.X, rect.Y + radius);

            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Draws a shadow effect around a rounded rectangle. Still available for the rare
        /// popup/dialog that wants one, but no longer wired up by default anywhere.
        /// </summary>
        public static void DrawShadow(Graphics g, Rectangle rect, int radius, int shadowSize = 4)
        {
            var shadowRect = new Rectangle(rect.X, rect.Y + 2, rect.Width, rect.Height);
            for (int i = shadowSize; i > 0; i--)
            {
                var shadowPathRect = new Rectangle(
                    shadowRect.X - i / 2,
                    shadowRect.Y + i / 2,
                    shadowRect.Width + i,
                    shadowRect.Height
                );
                using (var shadowPath = CreateRoundedRectangle(shadowPathRect, radius))
                using (var shadowBrush = new SolidBrush(Color.FromArgb((int)(20 * (1 - i / (float)shadowSize)), 0, 0, 0)))
                {
                    g.FillPath(shadowBrush, shadowPath);
                }
            }
        }

        /// <summary>
        /// Applies the shared minimalist look to a data grid: no row-header column, no cell
        /// borders/grid box, a flat header row with a thin bottom rule, light banded rows, and a
        /// subtle selection tint - used by every list screen (Customers, Bookings, Payments, etc.)
        /// instead of each form styling its own DataGridView from scratch.
        /// </summary>
        public static void StyleGrid(DataGridView grid)
        {
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.RowHeadersVisible = false;
            grid.BackgroundColor = WhiteColor;
            grid.GridColor = BorderColor;
            grid.RowTemplate.Height = 38;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight = 42;
            grid.EnableHeadersVisualStyles = false;

            // Every time the grid gets (re)bound: put spaces into PascalCase header text (e.g.
            // "ExpenseDate" -> "Expense Date"), measure each column's natural content width so
            // nothing truncates, then switch to Fill so columns stretch to use up any remaining
            // width instead of leaving the rest of the grid blank. Columns can never shrink below
            // their natural width, so if everything still doesn't fit, the grid scrolls
            // horizontally rather than clipping text again.
            //
            // DataBindingComplete alone isn't enough: LoadData() (and so the first bind) typically
            // runs from the form's constructor, before the grid has been parented into the visible,
            // Dock.Fill-sized control tree - at that point its Width is still whatever a freshly
            // constructed control defaults to, not its real on-screen width, so the very first Fill
            // pass can under-size the columns. HandleCreated fires once the grid's window handle
            // exists as part of the real, correctly-sized layout, so re-running the fit there
            // catches and corrects that case.
            grid.DataBindingComplete += (_, _) => FitColumnsToContentThenFill(grid);
            grid.HandleCreated += (_, _) => FitColumnsToContentThenFill(grid);

            grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = WhiteColor,
                ForeColor = TextSecondaryColor,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Alignment = DataGridViewContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                SelectionBackColor = WhiteColor,
                SelectionForeColor = TextSecondaryColor
            };

            grid.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = WhiteColor,
                ForeColor = TextPrimaryColor,
                Font = new Font("Segoe UI", 9.5f),
                Padding = new Padding(10, 4, 10, 4),
                SelectionBackColor = SelectionTintColor,
                SelectionForeColor = TextPrimaryColor
            };

            grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = BackgroundColor
            };
        }

        /// <summary>
        /// A small "title above big value" card - originally MainDashboardForm's stat tiles, moved
        /// here so any screen can use the same look (e.g. SubscriptionForm's Plan/Status/Renewal
        /// cards) instead of re-implementing it. valueColor lets a caller flag the value itself
        /// (e.g. red for a Suspended subscription) without a second card variant.
        /// </summary>
        public static RoundedPanel CreateStatCard(string title, string value, Color? valueColor = null)
        {
            var card = new RoundedPanel
            {
                Width = 220,
                Height = 110,
                BackColor = WhiteColor,
                Padding = new Padding(18),
                Margin = new Padding(0, 0, 16, 16)
            };

            var labelTitle = new Label
            {
                Text = title,
                ForeColor = TextSecondaryColor,
                Font = SecondaryFont,
                AutoSize = false,
                Width = 184,
                Height = 24,
                Location = new Point(18, 18)
            };

            var valueLabel = new Label
            {
                Text = value,
                ForeColor = valueColor ?? TextPrimaryColor,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                AutoSize = false,
                Width = 184,
                Height = 56,
                Location = new Point(18, 46),
                TextAlign = ContentAlignment.TopLeft
            };

            card.Controls.Add(labelTitle);
            card.Controls.Add(valueLabel);
            return card;
        }

        /// <summary>
        /// A key-number card that stretches to fill whichever TableLayoutPanel cell it's placed
        /// in, rather than CreateStatCard's fixed size - shared by Reports' KPI row and the home
        /// Dashboard so the two look identical. Title and value sit in their own row of an
        /// internal TableLayoutPanel (not Dock=Top/Fill siblings, which this runtime doesn't
        /// reserve space for correctly - see BuildFieldGroup's remarks on the same quirk). onClick,
        /// when given, makes the whole card act as one button: hand cursor plus a light hover
        /// tint, since a plain WinForms Panel has no built-in affordance for "this is clickable."
        /// </summary>
        public static RoundedPanel CreateKpiCard(string title, Label valueLabel, Action? onClick = null)
        {
            var card = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = WhiteColor,
                Padding = new Padding(18),
                Margin = new Padding(8)
            };

            var inner = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var titleLabel = new Label
            {
                Text = title,
                ForeColor = TextSecondaryColor,
                Font = SecondaryFont,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                AutoEllipsis = true
            };

            if (string.IsNullOrEmpty(valueLabel.Text)) valueLabel.Text = "—";
            valueLabel.ForeColor = TextPrimaryColor;
            valueLabel.Font = new Font("Segoe UI", 22, FontStyle.Bold);
            valueLabel.Dock = DockStyle.Fill;
            valueLabel.TextAlign = ContentAlignment.TopLeft;
            valueLabel.AutoEllipsis = true;

            inner.Controls.Add(titleLabel, 0, 0);
            inner.Controls.Add(valueLabel, 0, 1);
            card.Controls.Add(inner);

            if (onClick is not null)
            {
                card.Cursor = titleLabel.Cursor = valueLabel.Cursor = Cursors.Hand;
                card.Click += (_, _) => onClick();
                titleLabel.Click += (_, _) => onClick();
                valueLabel.Click += (_, _) => onClick();
                card.MouseEnter += (_, _) => card.BackColor = BackgroundColor;
                card.MouseLeave += (_, _) => card.BackColor = WhiteColor;
            }

            return card;
        }

        /// <summary>
        /// Makes a ChartPanel act as a link: hand cursor, and a click runs onClick. Used by both
        /// Reports (jump to a Detailed Reports tab) and the home Dashboard (open the module the
        /// chart's numbers come from).
        /// </summary>
        public static void MakeChartClickable(ChartPanel chart, Action onClick)
        {
            chart.Cursor = Cursors.Hand;
            chart.Click += (_, _) => onClick();
        }

        /// <summary>
        /// Applies the branch-column rule to any auto-generated grid whose bound row carries
        /// BranchId/BranchName. The raw BranchId is an internal key and never shown; BranchName
        /// appears as "Branch" only when several branches are combined in one grid, which is the
        /// only time the rows need telling apart.
        ///
        /// Lives here rather than in each screen because every branch-aware grid binds a DTO with
        /// these same two properties, and AutoGenerateColumns would otherwise surface both of them
        /// verbatim - including to Micro and Small tenants, who have no branch concept at all.
        /// </summary>
        public static void ApplyBranchColumns(DataGridView grid, bool showBranchName)
        {
            if (grid.Columns["BranchId"] is { } idColumn)
            {
                idColumn.Visible = false;
            }

            if (grid.Columns["BranchName"] is { } nameColumn)
            {
                nameColumn.HeaderText = "Branch";
                nameColumn.Visible = showBranchName;
            }
        }

        /// <summary>
        /// Paints a small colored dot + text over a status column's cells instead of plain text -
        /// e.g. Active/Pending/Paid/Cancelled - without adding a new column or changing the bound
        /// data. classify maps the cell's existing raw value to the label and dot color to show.
        /// </summary>
        public static void StyleStatusColumn(DataGridView grid, string columnName, System.Func<object?, (string Text, StatusKind Kind)> classify)
        {
            grid.CellPainting += (_, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

                var column = grid.Columns[e.ColumnIndex];
                if (column.Name != columnName && column.DataPropertyName != columnName) return;

                e.PaintBackground(e.CellBounds, true);

                var (text, kind) = classify(e.Value);
                var dotColor = kind switch
                {
                    StatusKind.Success => SuccessColor,
                    StatusKind.Warning => WarningColor,
                    StatusKind.Danger => DangerColor,
                    StatusKind.Info => InfoColor,
                    _ => TextSecondaryColor
                };

                const int dotSize = 8;
                var dotY = e.CellBounds.Top + (e.CellBounds.Height - dotSize) / 2;
                var dotX = e.CellBounds.Left + 10;
                var font = e.CellStyle?.Font ?? BodyFont;

                using (var brush = new SolidBrush(dotColor))
                {
                    e.Graphics!.FillEllipse(brush, dotX, dotY, dotSize, dotSize);
                }

                using (var textBrush = new SolidBrush(TextPrimaryColor))
                {
                    var textY = e.CellBounds.Top + (e.CellBounds.Height - font.Height) / 2;
                    e.Graphics!.DrawString(text, font, textBrush, dotX + dotSize + 6, textY);
                }

                e.Handled = true;
            };
        }

        /// <summary>
        /// Fills one inline action button (a DataGridViewButtonColumn cell) with a semantic status
        /// color, so an action's color means the same thing everywhere it appears: Navy = primary/
        /// confirm, Success = pay, Danger = delete, Info = secondary. The column must also set
        /// FlatStyle.Flat, or WinForms paints native button chrome and ignores these colors.
        ///
        /// Applied per cell from CellFormatting rather than once on the column, because
        /// AlternatingRowsDefaultCellStyle (see StyleGrid) outranks a column's DefaultCellStyle -
        /// setting the fill on the column left every second row showing the grid's background with
        /// white text on it, i.e. an invisible button.
        /// </summary>
        public static void StyleActionButtonCell(DataGridViewCell cell, Color fill)
        {
            cell.Style.BackColor = fill;
            cell.Style.ForeColor = WhiteColor;
            cell.Style.SelectionBackColor = fill;
            cell.Style.SelectionForeColor = WhiteColor;
            cell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        /// <summary>
        /// Clears an action button's fill for a row where that action doesn't apply - e.g. a
        /// booking with no payment yet has nothing to delete, so the cell is blanked. Without
        /// this the cell keeps its column fill and reads as a colored button with no label.
        /// </summary>
        public static void ClearActionButtonCell(DataGridViewCell cell)
        {
            var background = cell.RowIndex % 2 == 1 ? BackgroundColor : WhiteColor;
            cell.Style.BackColor = background;
            cell.Style.ForeColor = background;
            cell.Style.SelectionBackColor = SelectionTintColor;
            cell.Style.SelectionForeColor = SelectionTintColor;
        }

        /// <summary>
        /// Flattens a TabControl's native tab chrome (e.g. Finance's Expenses/Salary Payments
        /// tabs) into minimal text tabs with a bottom accent underline on the active tab - native
        /// tab headers can't otherwise be restyled in WinForms without owner-draw.
        /// </summary>
        public static void StyleTabControl(TabControl tabs)
        {
            tabs.Appearance = TabAppearance.Normal;
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.ItemSize = new Size(150, 38);
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;

            tabs.DrawItem += (_, e) =>
            {
                var page = tabs.TabPages[e.Index];
                var isSelected = e.Index == tabs.SelectedIndex;

                using (var backBrush = new SolidBrush(WhiteColor))
                {
                    e.Graphics.FillRectangle(backBrush, e.Bounds);
                }

                var textColor = isSelected ? TextPrimaryColor : TextSecondaryColor;
                using var font = new Font("Segoe UI", 10f, isSelected ? FontStyle.Bold : FontStyle.Regular);
                TextRenderer.DrawText(e.Graphics, page.Text, font, e.Bounds, textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                if (isSelected)
                {
                    using var accentPen = new Pen(NavyColor, 2);
                    e.Graphics.DrawLine(accentPen, e.Bounds.Left + 10, e.Bounds.Bottom - 2, e.Bounds.Right - 10, e.Bounds.Bottom - 2);
                }
            };
        }

        /// <summary>
        /// A small uppercase, muted section/group label - used both for sidebar section headers
        /// (OPERATIONS, FINANCE, ...) and for grouping related fields inside a dialog.
        /// </summary>
        public static Label CreateSectionLabel(string text) => new()
        {
            Text = text.ToUpperInvariant(),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = TextSecondaryColor,
            AutoSize = true,
            UseMnemonic = false
        };

        /// <summary>
        /// Builds the shared page-header row: title (large) with a secondary-text subtitle
        /// underneath, and an optional right-aligned primary action button - only present when the
        /// module actually has a "create new" action to attach it to.
        /// </summary>
        public static Control CreatePageHeader(string title, string subtitle, Control? primaryAction = null)
        {
            var titleLabel = new Label
            {
                Text = title,
                Font = PageTitleFont,
                ForeColor = TextPrimaryColor,
                AutoSize = true,
                UseMnemonic = false,
                Location = new Point(0, 0)
            };

            var subtitleLabel = new Label
            {
                Text = subtitle,
                Font = SecondaryFont,
                ForeColor = TextSecondaryColor,
                AutoSize = true,
                UseMnemonic = false,
                Location = new Point(0, titleLabel.PreferredHeight + 2)
            };

            var textStack = new Panel { Dock = DockStyle.Fill, BackColor = BackgroundColor };
            textStack.Controls.Add(titleLabel);
            textStack.Controls.Add(subtitleLabel);

            var header = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                BackColor = BackgroundColor
            };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            if (primaryAction is not null)
            {
                // An Absolute column sized exactly to the button's own Width is deterministic;
                // an AutoSize column measuring an Anchor-only (non-Docked) child can under-measure
                // before the header's own TableLayoutPanel has been given its final width by the
                // page's Dock.Fill resolution, which was clipping the button.
                header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, primaryAction.Width + 16));
                // Anchor=Right alone only fixes horizontal position; an explicit Y keeps the
                // button vertically centered in the row instead of stuck at the top.
                primaryAction.Location = new Point(16, (HeaderRowHeight - primaryAction.Height) / 2);
                primaryAction.Anchor = AnchorStyles.Right;
                primaryAction.Margin = new Padding(16, 0, 0, 0);
                header.Controls.Add(primaryAction, 1, 0);
            }
            else
            {
                header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
            }

            header.Controls.Add(textStack, 0, 0);

            return header;
        }

        /// <summary>
        /// Builds a single "label above field" group for a dialog form: a fixed-size Panel sized
        /// to the field's own Width/Height, with the label and field placed at explicit
        /// coordinates - replaces the old pattern of each dialog hand-placing a Label and its
        /// field via its own absolute Point/y-cursor bookkeeping. Deliberately avoids Dock/AutoSize
        /// interplay (this runtime's Dock.Fill sibling quirk - see MainDashboardForm/CustomerForm)
        /// in favor of plain, deterministic sizing. Stack several of these with
        /// <see cref="BuildDialogBody"/>.
        /// </summary>
        public static Control BuildFieldGroup(string label, Control field)
        {
            const int labelHeight = 18;
            const int gap = 4;

            var labelControl = new Label
            {
                Text = label,
                Font = SecondaryFont,
                ForeColor = TextSecondaryColor,
                AutoSize = false,
                UseMnemonic = false,
                Location = new Point(0, 0),
                Width = field.Width,
                Height = labelHeight
            };

            field.Location = new Point(0, labelHeight + gap);

            var group = new Panel
            {
                Width = field.Width,
                Height = labelHeight + gap + field.Height,
                Margin = new Padding(0, 0, 0, 14)
            };
            group.Controls.Add(labelControl);
            group.Controls.Add(field);
            return group;
        }

        /// <summary>
        /// Builds the shared Save (primary) / Cancel (secondary) button row for dialog forms,
        /// right-aligned with Save outermost - used instead of each dialog hand-positioning its
        /// own buttons and manually overriding Cancel's colors. Caller must set each button's
        /// Width/Height before calling.
        /// </summary>
        public static Control BuildDialogButtonRow(RoundedButton primary, RoundedButton secondary)
        {
            primary.IsPrimary = true;
            secondary.IsPrimary = false;

            var row = new FlowLayoutPanel
            {
                Width = primary.Width + secondary.Width + 12,
                Height = System.Math.Max(primary.Height, secondary.Height),
                Margin = new Padding(0, 10, 0, 0),
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false
            };
            primary.Margin = new Padding(0);
            secondary.Margin = new Padding(0, 0, 12, 0);

            row.Controls.Add(primary);
            row.Controls.Add(secondary);
            return row;
        }

        /// <summary>
        /// Stacks a dialog's field groups + button row top-down with the standard 24px margin on
        /// a white background, scrollable if the content ends up taller than the dialog. Sections
        /// keep their own Margin (field groups already carry bottom spacing; a bare control passed
        /// directly - e.g. BookingDialogForm's "+ New Customer" toggle - should set its own Margin
        /// before being passed in). Hiding a section's Visible flag closes its gap automatically,
        /// since FlowLayoutPanel skips invisible children - no manual Top-shifting needed.
        /// </summary>
        public static Control BuildDialogBody(params Control[] sections)
        {
            var stack = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(24),
                BackColor = WhiteColor
            };

            stack.Controls.AddRange(sections);
            return stack;
        }

        /// <summary>
        /// Builds a dialog's full contents: the field sections in a scrollable body, with the
        /// Save/Cancel row pinned in a fixed footer beneath it.
        ///
        /// The button row must stay OUT of the scrolling body. Passed as the body's last child it
        /// scrolls with everything else, and once the content is taller than the dialog the buttons
        /// end up clipped - unreachable even scrolled fully down, because a FlowLayoutPanel's
        /// AutoScroll extent doesn't include its bottom Padding. A TableLayoutPanel (Percent body +
        /// Absolute footer) is used rather than Dock siblings, which don't reserve space correctly
        /// on this runtime.
        /// </summary>
        public static Control BuildDialogLayout(RoundedButton primary, RoundedButton secondary, params Control[] sections)
        {
            var footer = BuildDialogButtonRow(primary, secondary);
            footer.Anchor = AnchorStyles.Right;
            footer.Margin = new Padding(0, 12, 24, 12);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = WhiteColor
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, footer.Height + 24));
            root.Controls.Add(BuildDialogBody(sections), 0, 0);
            root.Controls.Add(footer, 0, 1);

            // Clamped on Load, once the dialog has finished setting its own size in its
            // constructor. Pinning the footer only keeps the buttons reachable if the dialog
            // itself fits on screen - see ClampDialogToScreen.
            root.ParentChanged += (_, _) =>
            {
                if (root.FindForm() is { } dialog)
                {
                    // Both events, not just Load: on at least one runtime/DPI combination a
                    // dialog's final scaled size wasn't settled yet at Load, so the first clamp
                    // under-shrunk it and its footer still ended up beneath the taskbar. Shown
                    // fires after the form is actually on screen, catching whatever Load missed.
                    dialog.Load += (_, _) => ClampDialogToScreen(dialog);
                    dialog.Shown += (_, _) => ClampDialogToScreen(dialog);
                }
            };

            return root;
        }

        /// <summary>
        /// Shrinks a dialog to fit the working area of the screen it opens on (the desktop minus
        /// the taskbar). Dialogs hard-code a comfortable height - e.g. Run Payroll asks for 800px -
        /// which overflows a 768px display, pushing the button footer underneath the taskbar. They
        /// are FixedDialog with MaximizeBox off, so the user can't resize to reach the buttons and
        /// scrolling doesn't help: the footer is outside the scroll area by design. Clamping the
        /// height hands the overflow to the body's scrollbar, where it belongs.
        /// </summary>
        public static void ClampDialogToScreen(Form dialog)
        {
            var workingArea = Screen.FromControl(dialog).WorkingArea;
            dialog.Height = System.Math.Min(dialog.Height, workingArea.Height);
            dialog.Width = System.Math.Min(dialog.Width, workingArea.Width);

            // Centering on the parent can still leave a now-clamped dialog hanging off an edge,
            // so pull it back inside - but only as far as needed, leaving a dialog that already
            // fits where the caller's StartPosition put it.
            dialog.Location = new Point(
                System.Math.Clamp(dialog.Left, workingArea.Left, workingArea.Right - dialog.Width),
                System.Math.Clamp(dialog.Top, workingArea.Top, workingArea.Bottom - dialog.Height));
        }

        /// <summary>
        /// Wraps a Fill-docked content control (typically a grid) in a flat white surface with a
        /// subtle border - the minimal replacement for the old floating drop-shadow card, used for
        /// the main content area of every module page.
        /// </summary>
        public static Panel WrapContent(Control content)
        {
            var surface = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = WhiteColor,
                DrawShadow = false
            };
            content.Dock = DockStyle.Fill;
            surface.Controls.Add(content);
            return surface;
        }

        /// <summary>Header row height used by both BuildPage and CreatePageHeader.</summary>
        public const int HeaderRowHeight = 64;

        /// <summary>Toolbar row height used by BuildPage - tall enough for a 45px field/button
        /// plus the toolbar's own 12px top/bottom padding.</summary>
        public const int ToolbarRowHeight = 72;

        /// <summary>
        /// Assembles the shared Page Header -> Toolbar -> Content structure (toolbar optional) on
        /// the app background with a consistent outer margin, using a TableLayoutPanel so header/
        /// toolbar/content rows reserve space correctly - every module screen uses this instead of
        /// hand-rolling its own layout panel. A small gap row sits between each section so a
        /// header/toolbar button never visually crowds the content surface directly below it.
        /// </summary>
        public static Control BuildPage(Control header, Control? toolbar, Control content)
        {
            const int gap = 12;

            var rowCount = toolbar is null ? 3 : 5;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = rowCount, BackColor = BackgroundColor };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderRowHeight));
            header.Dock = DockStyle.Fill;
            layout.Controls.Add(header, 0, 0);

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, gap));

            var contentRow = 2;
            if (toolbar is not null)
            {
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, ToolbarRowHeight));
                toolbar.Dock = DockStyle.Fill;
                layout.Controls.Add(toolbar, 0, 2);

                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, gap));
                contentRow = 4;
            }

            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.Dock = DockStyle.Fill;
            layout.Controls.Add(content, 0, contentRow);

            var page = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24), BackColor = BackgroundColor };
            page.Controls.Add(layout);
            return page;
        }

        /// <summary>
        /// Splits PascalCase text into space-separated words, e.g. "ExpenseDate" -> "Expense
        /// Date". Already-spaced text (manually set header text like "Session Date") passes
        /// through unchanged, since there's no lowercase-to-uppercase transition to split on.
        /// </summary>
        private static string SplitPascalCaseWords(string text) =>
            Regex.Replace(text, "(?<=[a-z0-9])(?=[A-Z])", " ");

        /// <summary>
        /// Re-spaces header text, measures each column's natural (header + content) width, then
        /// switches to Fill sizing with that natural width as a floor - see StyleGrid. A blank-
        /// header DataGridViewButtonColumn (e.g. PaymentForm's inline Pay/Delete action slots) is a
        /// decorative action column, not real content - measuring it via AllCells picks up native
        /// button-chrome sizing that can be much wider than its actual "Pay"/"Delete" text, so it
        /// gets pinned to a small fixed width instead of a FillWeight share; otherwise it balloons
        /// into an obviously-empty strip once the grid is wider than the real content needs.
        /// </summary>
        private static void FitColumnsToContentThenFill(DataGridView grid)
        {
            if (grid.Columns.Count == 0)
            {
                return;
            }

            foreach (DataGridViewColumn column in grid.Columns)
            {
                column.HeaderText = SplitPascalCaseWords(column.HeaderText);
            }

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            var naturalWidths = grid.Columns.Cast<DataGridViewColumn>().Select(c => c.Width).ToList();

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            for (var i = 0; i < grid.Columns.Count; i++)
            {
                var column = grid.Columns[i];

                if (column is DataGridViewButtonColumn && string.IsNullOrEmpty(column.HeaderText))
                {
                    column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                    column.Width = 90;
                    continue;
                }

                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.NotSet;
                column.MinimumWidth = naturalWidths[i];
                column.FillWeight = System.Math.Max(naturalWidths[i], 1);
            }

            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }
    }
}
