using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SNAP.Controls
{
    /// <summary>
    /// A modern text box with a rounded container, focus border, and modern styling.
    /// </summary>
    public class RoundedTextBox : Control
    {
        private readonly TextBox _textBox;
        private bool _isFocused = false;

        public RoundedTextBox()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.BackgroundColor;
            Height = 45;
            AutoSize = false;

            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = AppTheme.WhiteColor,
                ForeColor = Color.Black,
                Font = AppTheme.BodyFont,
                Multiline = false,
                Cursor = Cursors.IBeam
            };

            _textBox.GotFocus += (s, e) =>
            {
                _isFocused = true;
                Invalidate();
            };

            _textBox.LostFocus += (s, e) =>
            {
                _isFocused = false;
                Invalidate();
            };

            _textBox.TextChanged += (s, e) => OnTextChanged(EventArgs.Empty);

            Controls.Add(_textBox);
        }

        /// <summary>
        /// Gets or sets the placeholder text. Delegates to the inner TextBox's native
        /// PlaceholderText rendering rather than drawing it on this control - the inner TextBox is
        /// an opaque child control that fully covers this control's own painted area, so anything
        /// drawn here directly is hidden behind it.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string PlaceholderText
        {
            get => _textBox.PlaceholderText;
            set => _textBox.PlaceholderText = value;
        }

        /// <summary>
        /// Gets or sets the text in the text box.
        /// </summary>
        public override string Text
        {
            get => _textBox.Text;
            set => _textBox.Text = value;
        }

        /// <summary>
        /// Gets or sets whether the text box uses password masking.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool UseSystemPasswordChar
        {
            get => _textBox.UseSystemPasswordChar;
            set => _textBox.UseSystemPasswordChar = value;
        }

        /// <summary>
        /// Gets or sets whether the text box is read-only.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool ReadOnly
        {
            get => _textBox.ReadOnly;
            set => _textBox.ReadOnly = value;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            var borderColor = _isFocused ? AppTheme.NavyColor : AppTheme.BorderColor;

            // Draw rounded background
            using (var path = AppTheme.CreateRoundedRectangle(rect, AppTheme.BorderRadius))
            using (var brush = new SolidBrush(AppTheme.WhiteColor))
            {
                e.Graphics.FillPath(brush, path);

                // Draw border
                using (var pen = new Pen(borderColor, 1))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            // Guard against null - can be called during constructor before _textBox is initialized
            if (_textBox == null)
                return;

            // Position the inner textbox with padding
            _textBox.Location = new Point(AppTheme.TextBoxPadding, (Height - _textBox.Height) / 2);
            _textBox.Width = Width - (AppTheme.TextBoxPadding * 2);

            // Update region for rounded clipping
            using (var path = AppTheme.CreateRoundedRectangle(
                new Rectangle(0, 0, Width, Height),
                AppTheme.BorderRadius))
            {
                Region = new Region(path);
            }
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            _textBox.Enabled = Enabled;
            BackColor = Enabled ? AppTheme.BackgroundColor : Color.LightGray;
            Invalidate();
        }
    }
}
