using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SNAP.Controls
{
    /// <summary>
    /// A flat, left-aligned sidebar navigation row: plain text, a subtle hover tint, and a
    /// clear-but-subtle active state (light background tint + thin left accent bar). Used instead
    /// of RoundedButton in the sidebar so navigation items read as a list, not a stack of buttons.
    /// </summary>
    public class SidebarNavItem : Control
    {
        private bool _isHovered;
        private bool _isActive;

        public SidebarNavItem()
        {
            DoubleBuffered = true;
            Height = 38;
            AutoSize = false;
            Cursor = Cursors.Hand;
            Font = AppTheme.BodyFont;
            ForeColor = AppTheme.WhiteColor;
            BackColor = AppTheme.NavyColor;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                Invalidate();
            }
        }

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

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            if (_isActive)
            {
                using var activeBrush = new SolidBrush(AppTheme.NavyLightColor);
                e.Graphics.FillRectangle(activeBrush, 0, 0, Width, Height);

                using var accentBrush = new SolidBrush(AppTheme.WhiteColor);
                e.Graphics.FillRectangle(accentBrush, 0, 0, 3, Height);
            }
            else if (_isHovered)
            {
                using var hoverBrush = new SolidBrush(AppTheme.NavyLightColor);
                e.Graphics.FillRectangle(hoverBrush, 0, 0, Width, Height);
            }

            var textRect = new Rectangle(20, 0, Width - 20, Height);
            // NoPrefix: without it, TextRenderer treats a single "&" as a mnemonic marker and
            // swallows it (e.g. "Bookings & Scheduling" renders as "Bookings _Scheduling").
            TextRenderer.DrawText(e.Graphics, Text, Font, textRect, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
