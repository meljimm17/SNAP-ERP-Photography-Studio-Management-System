using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SNAP.Controls
{
    /// <summary>
    /// A clickable, collapsible sidebar section header (OPERATIONS, RESOURCES, ...) with a
    /// chevron showing expanded/collapsed state. Collapsed by default so a sidebar with many
    /// modules doesn't render as one long, cramped list - clicking toggles ExpandedChanged, which
    /// the sidebar uses to show/hide that group's nav items.
    /// </summary>
    public class SidebarGroupHeader : Control
    {
        private bool _isHovered;
        private bool _expanded;

        public SidebarGroupHeader()
        {
            DoubleBuffered = true;
            Height = 30;
            AutoSize = false;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            ForeColor = Color.FromArgb(160, 165, 175);
            BackColor = AppTheme.NavyColor;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool Expanded
        {
            get => _expanded;
            set
            {
                if (_expanded == value) return;
                _expanded = value;
                Invalidate();
                ExpandedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? ExpandedChanged;

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovered = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Expanded = !Expanded;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);

            if (_isHovered)
            {
                using var hoverBrush = new SolidBrush(AppTheme.NavyLightColor);
                e.Graphics.FillRectangle(hoverBrush, 0, 0, Width, Height);
            }

            var textRect = new Rectangle(4, 0, Width - 26, Height);
            TextRenderer.DrawText(e.Graphics, Text.ToUpperInvariant(), Font, textRect, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            var chevron = _expanded ? "▾" : "▸"; // ▾ expanded / ▸ collapsed
            var chevronRect = new Rectangle(Width - 22, 0, 18, Height);
            TextRenderer.DrawText(e.Graphics, chevron, Font, chevronRect, ForeColor,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}
