using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SNAP.Controls
{
    /// <summary>
    /// A dropdown selector styled to match RoundedTextBox: a rounded container hosting a
    /// borderless, flat-styled ComboBox. Intended for DropDownStyle.DropDownList usage
    /// (item pickers), matching how it's used throughout the Booking/Payment/Package dialogs.
    /// </summary>
    public class RoundedComboBox : Control
    {
        private readonly ComboBox _comboBox;
        private bool _isFocused;

        public RoundedComboBox()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.BackgroundColor;
            Height = 45;
            AutoSize = false;

            _comboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.WhiteColor,
                ForeColor = Color.Black,
                Font = AppTheme.BodyFont,
                Cursor = Cursors.Hand
            };

            _comboBox.GotFocus += (s, e) => { _isFocused = true; Invalidate(); };
            _comboBox.LostFocus += (s, e) => { _isFocused = false; Invalidate(); };
            _comboBox.SelectedIndexChanged += (s, e) => OnSelectedIndexChanged(EventArgs.Empty);

            Controls.Add(_comboBox);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ComboBox.ObjectCollection Items => _comboBox.Items;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? SelectedItem
        {
            get => _comboBox.SelectedItem;
            set => _comboBox.SelectedItem = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int SelectedIndex
        {
            get => _comboBox.SelectedIndex;
            set => _comboBox.SelectedIndex = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? DataSource
        {
            get => _comboBox.DataSource;
            set => _comboBox.DataSource = value;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? DisplayMember
        {
            get => _comboBox.DisplayMember;
            set => _comboBox.DisplayMember = value ?? string.Empty;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string? ValueMember
        {
            get => _comboBox.ValueMember;
            set => _comboBox.ValueMember = value ?? string.Empty;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public object? SelectedValue
        {
            get => _comboBox.SelectedValue;
            set => _comboBox.SelectedValue = value;
        }

        public event EventHandler? SelectedIndexChanged;

        protected virtual void OnSelectedIndexChanged(EventArgs e) => SelectedIndexChanged?.Invoke(this, e);

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            _comboBox.Enabled = Enabled;
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
            if (_comboBox == null) return;

            _comboBox.Location = new Point(AppTheme.TextBoxPadding, (Height - _comboBox.Height) / 2);
            _comboBox.Width = Width - (AppTheme.TextBoxPadding * 2);

            using var path = AppTheme.CreateRoundedRectangle(new Rectangle(0, 0, Width, Height), AppTheme.BorderRadius);
            Region = new Region(path);
        }
    }
}
