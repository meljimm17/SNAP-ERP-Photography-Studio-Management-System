using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SNAP.Controls
{
    public enum ChartKind
    {
        /// <summary>Magnitude compared across a handful of named things.</summary>
        Bar,

        /// <summary>A measure moving over time.</summary>
        Line,

        /// <summary>Part-to-whole - each point is a share of one total. Always drawn in
        /// categorical colour, whatever UseCategoricalColors is set to, since a slice with no
        /// colour of its own would be meaningless.</summary>
        Donut,

        /// <summary>Same part-to-whole split as Donut, just without the punched-out hole/total
        /// label in the middle - a plain pie. Shares the same slice math and dot-and-label legend.</summary>
        Pie,

        /// <summary>Bar, rotated - for a ranked list or category names too long to fit under a
        /// vertical bar without truncating.</summary>
        HorizontalBar
    }

    public sealed record ChartPoint(string Label, decimal Value);

    /// <summary>
    /// A small owner-drawn chart. The WinForms Chart control was never ported past .NET Framework,
    /// and every package claiming to be it on NuGet is a community fork - so this draws with GDI+,
    /// the same way RoundedButton and the grid status badges already do.
    ///
    /// Four forms, chosen by the job the data does: Bar for magnitude compared across a handful of
    /// named things, Line for a measure moving over time, Donut for a part-to-whole split, and
    /// HorizontalBar for a ranked list or category names too long to fit under a vertical bar.
    /// </summary>
    public class ChartPanel : Control
    {
        /// <summary>
        /// Fixed categorical order, assigned by position and never cycled - a category keeps its
        /// colour even when the set being drawn changes. Validated for colour-blind separation and
        /// contrast against the chart surface; do not reorder or extend without re-validating.
        /// The app's status colours are deliberately NOT reused here: Danger and Warning are too
        /// close to tell apart as neighbouring series.
        /// </summary>
        private static readonly Color[] CategoricalPalette =
        {
            ColorTranslator.FromHtml("#2563EB"),
            ColorTranslator.FromHtml("#0D9488"),
            ColorTranslator.FromHtml("#D97706"),
            ColorTranslator.FromHtml("#7C3AED"),
            ColorTranslator.FromHtml("#DC2626")
        };

        private const int GridLineCount = 4;
        private const int BarCornerRadius = 4;
        private const int BarGap = 2;

        private string _title = string.Empty;
        private ChartKind _kind = ChartKind.Bar;
        private IReadOnlyList<ChartPoint> _points = Array.Empty<ChartPoint>();
        private bool _useCategoricalColors;
        private string _valueFormat = "N0";

        public ChartPanel()
        {
            DoubleBuffered = true;
            BackColor = AppTheme.WhiteColor;
            Height = 260;
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ChartKind Kind
        {
            get => _kind;
            set { _kind = value; Invalidate(); }
        }

        /// <summary>
        /// True only when each bar is a different *thing* (expense categories). False when the bars
        /// are one measure compared across entities (revenue per branch) - there, a second colour
        /// would imply a distinction that isn't in the data, so every bar shares one hue.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool UseCategoricalColors
        {
            get => _useCategoricalColors;
            set { _useCategoricalColors = value; Invalidate(); }
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string ValueFormat
        {
            get => _valueFormat;
            set { _valueFormat = value; Invalidate(); }
        }

        public void SetData(IReadOnlyList<ChartPoint> points)
        {
            _points = points;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            Render(e.Graphics, Width, Height);
        }

        /// <summary>
        /// The chart exactly as drawn on screen, as a PNG for the PDF report. Laid out at the given
        /// logical size, then rasterised at <paramref name="scale"/>x so it stays sharp when printed
        /// - a 1:1 screen capture looks soft on paper.
        /// </summary>
        public byte[] RenderToPng(int width, int height, float scale = 3f)
        {
            // Left at the default 96 DPI on purpose: the fonts are sized in points, so a higher
            // bitmap DPI would enlarge the text a second time on top of ScaleTransform.
            using var bitmap = new Bitmap((int)(width * scale), (int)(height * scale));
            using (var g = Graphics.FromImage(bitmap))
            {
                // ClearType's sub-pixel colour fringes are tuned for an LCD, not a printed page.
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.ScaleTransform(scale, scale);
                Render(g, width, height);
            }

            using var stream = new MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return stream.ToArray();
        }

        private void Render(Graphics g, int width, int height)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(BackColor);

            var titleHeight = 0;
            if (!string.IsNullOrWhiteSpace(_title))
            {
                using var titleBrush = new SolidBrush(AppTheme.TextPrimaryColor);
                g.DrawString(_title, AppTheme.SectionTitleFont, titleBrush, 4, 4);
                titleHeight = AppTheme.SectionTitleFont.Height + 12;
            }

            if (_points.Count == 0)
            {
                using var emptyBrush = new SolidBrush(AppTheme.TextSecondaryColor);
                g.DrawString("No data for this period.", AppTheme.BodyFont, emptyBrush, 4, titleHeight + 8);
                return;
            }

            // Donut and HorizontalBar don't share Bar/Line's left value-axis + bottom category-
            // label layout (a donut has no axis at all; a horizontal bar's labels sit to the left
            // of each bar, not underneath), so each gets the full remaining area and lays it out
            // its own way instead.
            if (_kind == ChartKind.Donut || _kind == ChartKind.Pie)
            {
                var donutPlot = new Rectangle(4, titleHeight + 4, Math.Max(1, width - 8), Math.Max(1, height - titleHeight - 8));
                DrawPieOrDonut(g, donutPlot, withHole: _kind == ChartKind.Donut);
                return;
            }

            if (_kind == ChartKind.HorizontalBar)
            {
                var hBarPlot = new Rectangle(4, titleHeight + 4, Math.Max(1, width - 8), Math.Max(1, height - titleHeight - 8));
                var hBarAxisMax = NiceAxisMaximum(_points.Max(p => p.Value));
                DrawHorizontalBars(g, hBarPlot, hBarAxisMax);
                return;
            }

            // Room for the value axis on the left and the category labels underneath.
            const int axisWidth = 64;
            const int labelHeight = 26;
            var plot = new Rectangle(
                axisWidth,
                titleHeight + 10,
                Math.Max(1, width - axisWidth - 16),
                Math.Max(1, height - titleHeight - labelHeight - 20));

            var axisMax = NiceAxisMaximum(_points.Max(p => p.Value));
            DrawGridAndAxis(g, plot, axisMax);

            if (_kind == ChartKind.Bar)
            {
                DrawBars(g, plot, axisMax);
            }
            else
            {
                DrawLine(g, plot, axisMax);
            }
        }

        /// <summary>
        /// Rounds the axis top up to a 1/2/5 x 10^n step so the gridline values read as whole
        /// numbers rather than whatever the tallest bar happens to be.
        /// </summary>
        private static decimal NiceAxisMaximum(decimal dataMax)
        {
            if (dataMax <= 0) return 1m;

            var magnitude = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)dataMax)));
            var normalized = dataMax / magnitude;
            var step = normalized switch
            {
                <= 1m => 1m,
                <= 2m => 2m,
                <= 5m => 5m,
                _ => 10m
            };
            return step * magnitude;
        }

        /// <summary>Grid and axis stay recessive - they orient the eye without competing with the data.</summary>
        private void DrawGridAndAxis(Graphics g, Rectangle plot, decimal axisMax)
        {
            using var gridPen = new Pen(AppTheme.BorderColor, 1);
            using var labelBrush = new SolidBrush(AppTheme.TextSecondaryColor);
            using var format = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };

            for (var i = 0; i <= GridLineCount; i++)
            {
                var y = plot.Bottom - (int)(plot.Height * (i / (float)GridLineCount));
                g.DrawLine(gridPen, plot.Left, y, plot.Right, y);

                var value = axisMax * i / GridLineCount;
                g.DrawString(
                    value.ToString(_valueFormat),
                    AppTheme.SecondaryFont,
                    labelBrush,
                    new RectangleF(0, y - 10, plot.Left - 8, 20),
                    format);
            }
        }

        /// <summary>Widest a single bar is ever drawn, however few categories there are - a bar
        /// chart with only two or three bars still centres each one in its own slot rather than
        /// stretching to fill it, so it doesn't read as a handful of oversized blocks. Slots are
        /// still evenly divided across the full plot width regardless, so every bar - however many
        /// there are - keeps its own space and never overlaps or runs past the edge.</summary>
        private const int MaxBarWidth = 56;

        private void DrawBars(Graphics g, Rectangle plot, decimal axisMax)
        {
            var slot = plot.Width / (float)_points.Count;
            var barWidth = Math.Min(MaxBarWidth, Math.Max(4f, slot - (BarGap * 2)));

            using var labelBrush = new SolidBrush(AppTheme.TextSecondaryColor);
            using var valueBrush = new SolidBrush(AppTheme.TextPrimaryColor);
            using var centered = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

            for (var i = 0; i < _points.Count; i++)
            {
                var point = _points[i];
                var height = axisMax == 0 ? 0 : (int)(plot.Height * (float)(point.Value / axisMax));
                var x = plot.Left + (slot * i) + ((slot - barWidth) / 2f);
                var bar = new RectangleF(x, plot.Bottom - height, barWidth, height);

                if (height > 0)
                {
                    using var brush = new SolidBrush(ColorFor(i));
                    using var path = RoundedTopRectangle(bar, BarCornerRadius);
                    g.FillPath(brush, path);
                }

                // Direct labels are required, not decorative: two of the categorical hues sit close
                // enough under colour-blind simulation that colour alone wouldn't separate them.
                var labelBox = new RectangleF(plot.Left + (slot * i), plot.Bottom + 6, slot, 18);
                g.DrawString(point.Label, AppTheme.SecondaryFont, labelBrush, labelBox, centered);

                if (height > 0)
                {
                    var valueBox = new RectangleF(plot.Left + (slot * i), bar.Top - 18, slot, 16);
                    g.DrawString(point.Value.ToString(_valueFormat), AppTheme.SecondaryFont, valueBrush, valueBox, centered);
                }
            }
        }

        private void DrawLine(Graphics g, Rectangle plot, decimal axisMax)
        {
            var step = _points.Count == 1 ? 0f : plot.Width / (float)(_points.Count - 1);
            var points = new PointF[_points.Count];

            for (var i = 0; i < _points.Count; i++)
            {
                var height = axisMax == 0 ? 0 : plot.Height * (float)(_points[i].Value / axisMax);
                points[i] = new PointF(
                    _points.Count == 1 ? plot.Left + (plot.Width / 2f) : plot.Left + (step * i),
                    plot.Bottom - height);
            }

            using var linePen = new Pen(CategoricalPalette[0], 2) { LineJoin = LineJoin.Round };
            if (points.Length > 1)
            {
                g.DrawLines(linePen, points);
            }

            using var markerBrush = new SolidBrush(CategoricalPalette[0]);
            using var ringPen = new Pen(BackColor, 2);
            using var labelBrush = new SolidBrush(AppTheme.TextSecondaryColor);
            using var centered = new StringFormat { Alignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };

            // Only the ends and the peak get a value - a number on every point is noise.
            var peakIndex = 0;
            for (var i = 1; i < _points.Count; i++)
            {
                if (_points[i].Value > _points[peakIndex].Value) peakIndex = i;
            }

            for (var i = 0; i < points.Length; i++)
            {
                var marker = new RectangleF(points[i].X - 4, points[i].Y - 4, 8, 8);
                g.FillEllipse(markerBrush, marker);
                g.DrawEllipse(ringPen, marker);

                var slot = Math.Max(step, 48f);
                g.DrawString(_points[i].Label, AppTheme.SecondaryFont, labelBrush,
                    new RectangleF(points[i].X - (slot / 2f), plot.Bottom + 6, slot, 18), centered);

                if (i == 0 || i == points.Length - 1 || i == peakIndex)
                {
                    using var valueBrush = new SolidBrush(AppTheme.TextPrimaryColor);
                    g.DrawString(_points[i].Value.ToString(_valueFormat), AppTheme.SecondaryFont, valueBrush,
                        new RectangleF(points[i].X - (slot / 2f), points[i].Y - 20, slot, 16), centered);
                }
            }
        }

        private Color ColorFor(int index) =>
            _useCategoricalColors || _kind == ChartKind.Donut || _kind == ChartKind.Pie
                ? CategoricalPalette[index % CategoricalPalette.Length]
                : CategoricalPalette[0];

        /// <summary>
        /// Draws every slice as a full pie wedge; when withHole is true, erases a smaller circle
        /// out of the middle back to the panel's own background to turn it into a ring (a donut) -
        /// GDI+ has no native annulus primitive, so this two-layer trick is the standard way to
        /// fake one. A dot-and-label legend sits to the right either way.
        /// </summary>
        private void DrawPieOrDonut(Graphics g, Rectangle plot, bool withHole)
        {
            var total = _points.Sum(p => p.Value);

            const int legendWidth = 160;
            var ringArea = new Rectangle(plot.Left, plot.Top, Math.Max(1, plot.Width - legendWidth), plot.Height);
            var diameter = Math.Max(1, Math.Min(ringArea.Width, ringArea.Height) - 8);
            var ringRect = new Rectangle(
                ringArea.Left + ((ringArea.Width - diameter) / 2),
                ringArea.Top + ((ringArea.Height - diameter) / 2),
                diameter, diameter);

            var startAngle = -90f;
            for (var i = 0; i < _points.Count; i++)
            {
                if (_points[i].Value <= 0 || total <= 0) continue;
                var sweep = (float)(360m * (_points[i].Value / total));
                using var sliceBrush = new SolidBrush(ColorFor(i));
                g.FillPie(sliceBrush, ringRect, startAngle, Math.Max(sweep, 0.1f));
                startAngle += sweep;
            }

            if (withHole)
            {
                var holeDiameter = (int)(diameter * 0.55);
                var holeRect = new Rectangle(
                    ringRect.Left + ((diameter - holeDiameter) / 2),
                    ringRect.Top + ((diameter - holeDiameter) / 2),
                    holeDiameter, holeDiameter);
                using (var holeBrush = new SolidBrush(BackColor))
                {
                    g.FillEllipse(holeBrush, holeRect);
                }

                using var totalBrush = new SolidBrush(AppTheme.TextPrimaryColor);
                using var totalFont = new Font("Segoe UI", 11, FontStyle.Bold);
                using var centerFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(total.ToString(_valueFormat), totalFont, totalBrush, holeRect, centerFormat);
            }

            var legendX = ringArea.Right + 8;
            var legendY = plot.Top + 2;
            using var legendBrush = new SolidBrush(AppTheme.TextPrimaryColor);
            for (var i = 0; i < _points.Count && legendY < plot.Bottom - 16; i++)
            {
                using var swatchBrush = new SolidBrush(ColorFor(i));
                g.FillEllipse(swatchBrush, legendX, legendY + 3, 10, 10);

                var pct = total <= 0 ? 0 : _points[i].Value / total * 100m;
                var text = plot.Width < 260
                    ? _points[i].Label
                    : $"{_points[i].Label} ({pct:N0}%)";
                g.DrawString(text, AppTheme.SecondaryFont, legendBrush,
                    new RectangleF(legendX + 16, legendY, legendWidth - 20, 18));
                legendY += 20;
            }
        }

        /// <summary>Bar, rotated: category labels to the left, bars growing rightward from a
        /// shared baseline, value printed just past each bar's end. No axis/gridlines - a ranked
        /// list reads better plain than cluttered with a value scale nobody needs to line up
        /// against.</summary>
        private void DrawHorizontalBars(Graphics g, Rectangle plot, decimal axisMax)
        {
            const int labelWidth = 130;
            const int valueWidth = 64;
            var barsArea = new Rectangle(
                plot.Left + labelWidth,
                plot.Top,
                Math.Max(1, plot.Width - labelWidth - valueWidth),
                plot.Height);

            var slot = barsArea.Height / (float)_points.Count;
            var barHeight = Math.Max(4f, slot - (BarGap * 2));

            using var labelBrush = new SolidBrush(AppTheme.TextPrimaryColor);
            using var valueBrush = new SolidBrush(AppTheme.TextPrimaryColor);
            using var labelFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            using var valueFormat = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };

            for (var i = 0; i < _points.Count; i++)
            {
                var point = _points[i];
                var y = barsArea.Top + (slot * i) + ((slot - barHeight) / 2f);
                var width = axisMax == 0 ? 0 : Math.Max(2, (int)(barsArea.Width * (float)(point.Value / axisMax)));

                g.DrawString(point.Label, AppTheme.SecondaryFont, labelBrush,
                    new RectangleF(plot.Left, y, labelWidth - 8, barHeight), labelFormat);

                using var barBrush = new SolidBrush(ColorFor(i));
                using var path = RoundedRightRectangle(new RectangleF(barsArea.Left, y, width, barHeight), BarCornerRadius);
                g.FillPath(barBrush, path);

                g.DrawString(point.Value.ToString(_valueFormat), AppTheme.SecondaryFont, valueBrush,
                    new RectangleF(barsArea.Left + width + 6, y, valueWidth, barHeight), valueFormat);
            }
        }

        /// <summary>Rounds only the right-hand corners, so a horizontal bar stays anchored to its
        /// left baseline the same way DrawBars' vertical bars stay anchored to the bottom.</summary>
        private static GraphicsPath RoundedRightRectangle(RectangleF rect, int radius)
        {
            var path = new GraphicsPath();
            var diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));

            if (diameter <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddLine(rect.Left, rect.Top, rect.Right - diameter, rect.Top);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddLine(rect.Right - diameter, rect.Bottom, rect.Left, rect.Bottom);
            path.CloseFigure();
            return path;
        }

        /// <summary>Rounds only the top corners, so the bar stays anchored to the baseline.</summary>
        private static GraphicsPath RoundedTopRectangle(RectangleF rect, int radius)
        {
            var path = new GraphicsPath();
            var diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));

            if (diameter <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.Left, rect.Top, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Top, diameter, diameter, 270, 90);
            path.AddLine(rect.Right, rect.Top + diameter, rect.Right, rect.Bottom);
            path.AddLine(rect.Right, rect.Bottom, rect.Left, rect.Bottom);
            path.CloseFigure();
            return path;
        }
    }
}
