using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SNAP.Controls
{
    /// <summary>
    /// A modern button with rounded corners, hover effects, and focus states. Both weights are
    /// solid-filled from the SNAP palette rather than outline/white, per direct feedback - Primary
    /// (full Navy, strongest emphasis, e.g. Save/+Add) and Secondary (lighter Navy tint, e.g.
    /// Edit/Delete) via <see cref="IsPrimary"/>, so callers stop hand-overriding BackColor/
    /// ForeColor for lower-emphasis actions.
    /// </summary>
    public class RoundedButton : Button
    {
        private bool _isHovered = false;
        private bool _isFocused = false;
        private bool _isPrimary = true;

        public RoundedButton()
        {
            DoubleBuffered = true;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;

            Height = AppTheme.ButtonHeight;
            AutoSize = false;
            Cursor = Cursors.Hand;

            // Default colors
            BackColor = AppTheme.NavyColor;
            ForeColor = AppTheme.WhiteColor;
            Font = AppTheme.BodyFont;
        }

        /// <summary>
        /// True (default) for a strong-emphasis primary action (solid full-Navy fill). Set false
        /// for a simpler secondary action (solid lighter-Navy fill) - e.g. Cancel next to a
        /// primary Save button, or Edit/Delete next to a page's +Add action.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool IsPrimary
        {
            get => _isPrimary;
            set
            {
                _isPrimary = value;
                BackColor = value ? AppTheme.NavyColor : AppTheme.NavyLightColor;
                ForeColor = AppTheme.WhiteColor;
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

        protected override void OnGotFocus(EventArgs e)
        {
            _isFocused = true;
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            _isFocused = false;
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? AppTheme.BackgroundColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Determine button color based on state and emphasis - both weights are solid fills;
            // primary lightens on hover, secondary deepens toward full Navy on hover, so both get
            // a clear, distinct hover cue while staying filled with a palette color.
            var buttonColor = _isPrimary
                ? (_isHovered ? AppTheme.NavyLightColor : AppTheme.NavyColor)
                : (_isHovered ? AppTheme.NavyColor : AppTheme.NavyLightColor);
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Draw rounded background
            using (var path = AppTheme.CreateRoundedRectangle(rect, AppTheme.ButtonRadius))
            using (var brush = new SolidBrush(buttonColor))
            {
                e.Graphics.FillPath(brush, path);

                // Draw focus border if focused
                if (_isFocused)
                {
                    using (var pen = new Pen(AppTheme.WhiteColor, AppTheme.FocusBorderWidth))
                    {
                        e.Graphics.DrawPath(pen, path);
                    }
                }
            }

            // Draw text
            var textSize = e.Graphics.MeasureString(Text, Font);
            var textX = (Width - textSize.Width) / 2;
            var textY = (Height - textSize.Height) / 2;

            using (var brush = new SolidBrush(ForeColor))
            {
                e.Graphics.DrawString(Text, Font, brush, textX, textY);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // Update the region to clip the control to the rounded path
            using (var path = AppTheme.CreateRoundedRectangle(
                new Rectangle(0, 0, Width, Height),
                AppTheme.ButtonRadius))
            {
                Region = new Region(path);
            }
        }
    }
}
