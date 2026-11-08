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
        private readonly Label _revealToggle;
        private bool _isFocused = false;

        // Whether this field was ever configured as a password field at all (set once, via the
        // UseSystemPasswordChar setter) - independent of _textBox.UseSystemPasswordChar's current
        // live value, which the reveal toggle flips back and forth without un-configuring the
        // field itself. Only a password field ever shows the eye icon.
        private bool _isPasswordField;

        // Segoe MDL2 Assets glyphs - the same ones Windows' own built-in password-reveal button
        // uses (e.g. the Microsoft Account sign-in box), so this reads as a native control rather
        // than an invented one.
        private const string RevealGlyph = ""; // "View" - eye
        private const string HideGlyph = "";   // "Hide" - eye with a line through it

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

            // Only ever shown on a password field (see UseSystemPasswordChar's setter) - lets
            // someone unsure they typed their password correctly check it without retyping, the
            // same reveal-password convention every modern login form uses.
            _revealToggle = new Label
            {
                Text = RevealGlyph,
                Font = new Font("Segoe MDL2 Assets", 11f),
                ForeColor = AppTheme.TextSecondaryColor,
                BackColor = AppTheme.WhiteColor,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _revealToggle.Click += (_, _) => ToggleReveal();
            _revealToggle.MouseEnter += (_, _) => _revealToggle.ForeColor = AppTheme.NavyColor;
            _revealToggle.MouseLeave += (_, _) => _revealToggle.ForeColor = AppTheme.TextSecondaryColor;

            Controls.Add(_textBox);
            Controls.Add(_revealToggle);
        }

        private void ToggleReveal()
        {
            _textBox.UseSystemPasswordChar = !_textBox.UseSystemPasswordChar;
            _revealToggle.Text = _textBox.UseSystemPasswordChar ? RevealGlyph : HideGlyph;
            _textBox.Focus();
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
        /// Gets or sets whether the text box uses password masking. Setting this true is also what
        /// turns this into "a password field" for good - it shows the reveal-toggle eye icon, which
        /// stays available even after someone clicks it to peek at what they typed (clicking only
        /// flips the live masking state below, not this flag).
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool UseSystemPasswordChar
        {
            get => _textBox.UseSystemPasswordChar;
            set
            {
                _textBox.UseSystemPasswordChar = value;
                if (value) _isPasswordField = true;
                _revealToggle.Visible = _isPasswordField;
                _revealToggle.Text = _textBox.UseSystemPasswordChar ? RevealGlyph : HideGlyph;
                PositionChildren();
            }
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
            PositionChildren();
        }

        /// <summary>
        /// Lays out the inner textbox and, on a password field, the reveal-toggle icon that eats
        /// into its right edge - called on resize and whenever the icon's visibility changes, since
        /// turning it on/off changes how much width the textbox itself gets.
        /// </summary>
        private void PositionChildren()
        {
            // Guard against null - can be called during the base Control constructor's own resize
            // before this constructor has created either child yet.
            if (_textBox == null || _revealToggle == null)
                return;

            const int iconWidth = 32;
            var textBoxWidth = Width - (AppTheme.TextBoxPadding * 2) - (_revealToggle.Visible ? iconWidth : 0);

            _textBox.Location = new Point(AppTheme.TextBoxPadding, (Height - _textBox.Height) / 2);
            _textBox.Width = Math.Max(0, textBoxWidth);

            if (_revealToggle.Visible)
            {
                _revealToggle.Size = new Size(iconWidth, Height - 4);
                _revealToggle.Location = new Point(Width - iconWidth - 2, 2);
            }

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
            _revealToggle.Enabled = Enabled;
            _revealToggle.Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            BackColor = Enabled ? AppTheme.BackgroundColor : Color.LightGray;
            Invalidate();
        }
    }
}
