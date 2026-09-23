using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SNAP.Controls
{
    /// <summary>
    /// Date/time picker styled to match RoundedTextBox: a rounded container hosting a
    /// DateTimePicker. Note: unlike ComboBox/TextBox, DateTimePicker has no flat/borderless
    /// mode in WinForms, so its own thin native border remains visible inside the rounded
    /// container - a known cosmetic limitation of the platform control, not a bug.
    /// </summary>
    public class RoundedDatePicker : Control
    {
        private readonly DateTimePicker _picker;
        private bool _isFocused;

        public RoundedDatePicker()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.BackgroundColor;
            Height = 45;
            AutoSize = false;

            _picker = new DateTimePicker
            {
                Font = AppTheme.BodyFont,
                Format = DateTimePickerFormat.Custom
            };

            _picker.GotFocus += (s, e) => { _isFocused = true; Invalidate(); };
            _picker.LostFocus += (s, e) => { _isFocused = false; Invalidate(); };
            _picker.ValueChanged += (s, e) => OnValueChanged(EventArgs.Empty);

            Controls.Add(_picker);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DateTimePickerFormat Format
        {
            get => _picker.Format;
            set => _picker.Format = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string CustomFormat
        {
            get => _picker.CustomFormat ?? string.Empty;
            set => _picker.CustomFormat = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DateTime Value
        {
            get => _picker.Value;
            set => _picker.Value = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DateTime MinDate
        {
            get => _picker.MinDate;
            set => _picker.MinDate = value;
        }

        public event EventHandler? ValueChanged;

        protected virtual void OnValueChanged(EventArgs e) => ValueChanged?.Invoke(this, e);

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            _picker.Enabled = Enabled;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? AppTheme.BackgroundColor);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            var borderColor = _isFocused ? AppTheme.NavyColor : AppTheme.BorderColor;

            using (var path = AppTheme.CreateRoundedRectangle(rect, AppTheme.BorderRadius))
            using (var brush = new SolidBrush(Enabled ? AppTheme.WhiteColor : Color.LightGray))
            {
                e.Graphics.FillPath(brush, path);
                using var pen = new Pen(borderColor, 1);
                e.Graphics.DrawPath(pen, path);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_picker == null) return;

            _picker.Location = new Point(AppTheme.TextBoxPadding, (Height - _picker.Height) / 2);
            _picker.Width = Width - (AppTheme.TextBoxPadding * 2);

            using var path = AppTheme.CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), AppTheme.BorderRadius);
            Region = new Region(path);
        }
    }
}
