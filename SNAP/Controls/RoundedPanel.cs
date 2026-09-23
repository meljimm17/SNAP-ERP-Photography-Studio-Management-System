using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SNAP.Controls
{
    /// <summary>
    /// A modern rounded panel for cards and containers with optional shadow.
    /// </summary>
    public class RoundedPanel : Panel
    {
        private bool _drawShadow = false;

        public RoundedPanel()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.WhiteColor;
            Padding = new Padding(1);
        }

        /// <summary>
        /// Gets or sets whether to draw a shadow effect.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool DrawShadow
        {
            get => _drawShadow;
            set
            {
                _drawShadow = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? AppTheme.BackgroundColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // Draw shadow if enabled
            if (_drawShadow)
            {
                AppTheme.DrawShadow(e.Graphics, rect, AppTheme.BorderRadius);
            }

            // Draw rounded background
            using (var path = AppTheme.CreateRoundedRectangle(rect, AppTheme.BorderRadius))
            using (var brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillPath(brush, path);

                // Draw border
                using (var pen = new Pen(AppTheme.BorderColor, 1))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // Update the region to clip the control to the rounded path
            using (var path = AppTheme.CreateRoundedRectangle(
                new Rectangle(0, 0, Width, Height),
                AppTheme.BorderRadius))
            {
                Region = new Region(path);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Don't paint background — OnPaint handles it
        }
    }
}
