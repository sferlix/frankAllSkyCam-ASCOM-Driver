using System.Drawing.Drawing2D;

namespace AlpacaAllSkyWeather.TrayHost;

/// <summary>A card showing wind direction as a compass rose (8 cardinal labels + tick marks) with a
/// needle overlaid pointing at the live direction, plus the numeric degrees and cardinal abbreviation.</summary>
public sealed class CompassCard : Control
{
    private static readonly string[] CardinalNames = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

    private static readonly Color CardBackground = Color.FromArgb(0x1E, 0x21, 0x2D);
    private static readonly Color CardBorder = Color.FromArgb(0x2A, 0x2E, 0x3D);
    private static readonly Color TextPrimary = Color.FromArgb(0xF5, 0xF6, 0xFA);
    private static readonly Color TextMuted = Color.FromArgb(0x8A, 0x8F, 0xA3);
    private static readonly Color RingColor = Color.FromArgb(0x3A, 0x3F, 0x52);

    private readonly Color _accent;
    private readonly Color _needleColor;
    private float? _degrees;
    private string _speedValue = "—";
    private string _speedUnit = "";

    public string Label { get; }

    public CompassCard(Color accent, Color needleColor, string label)
    {
        _accent = accent;
        _needleColor = needleColor;
        Label = label;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(0x14, 0x16, 0x1F);
        Size = new Size(168, 94);
    }

    /// <summary>Updates both the compass needle (<paramref name="degrees"/>) and the wind-speed readout
    /// shown alongside it — direction and speed share this one card.</summary>
    public void SetWind(float? degrees, string speedValue, string speedUnit)
    {
        if (_degrees == degrees && _speedValue == speedValue && _speedUnit == speedUnit)
        {
            return;
        }

        _degrees = degrees;
        _speedValue = speedValue;
        _speedUnit = speedUnit;
        Invalidate();
    }

    private static string ToCardinal(float degrees)
    {
        int index = (int)Math.Round(((degrees % 360 + 360) % 360) / 45.0) % 8;
        return CardinalNames[index];
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new RectangleF(0, 0, Width - 1, Height - 1);
        using (var path = RoundedRect(rect, 14f))
        using (var bgBrush = new SolidBrush(CardBackground))
        using (var borderPen = new Pen(CardBorder, 1.2f))
        {
            g.FillPath(bgBrush, path);
            g.DrawPath(borderPen, path);
        }

        float dialCx = 42, dialCy = Height / 2f;
        float dialR = 30f;

        using (var ringPen = new Pen(RingColor, 1.6f))
        {
            g.DrawEllipse(ringPen, dialCx - dialR, dialCy - dialR, dialR * 2, dialR * 2);
        }
        using (var innerRingPen = new Pen(Color.FromArgb(60, RingColor), 1f))
        {
            g.DrawEllipse(innerRingPen, dialCx - dialR * 0.68f, dialCy - dialR * 0.68f, dialR * 1.36f, dialR * 1.36f);
        }

        using var mainFont = new Font("Segoe UI", 7.2f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var minorFont = new Font("Segoe UI", 5.8f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var labelBrush = new SolidBrush(TextMuted);
        using var mainLabelBrush = new SolidBrush(TextPrimary);

        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4 - Math.PI / 2;
            bool isMain = i % 2 == 0;
            float labelR = dialR + (isMain ? 9f : 7f);
            var name = CardinalNames[i];
            var font = isMain ? mainFont : minorFont;
            var brush = isMain ? mainLabelBrush : labelBrush;
            var size = g.MeasureString(name, font);
            float lx = dialCx + (float)(Math.Cos(a) * labelR) - size.Width / 2;
            float ly = dialCy + (float)(Math.Sin(a) * labelR) - size.Height / 2;
            g.DrawString(name, font, brush, lx, ly);

            using var tickPen = new Pen(isMain ? TextMuted : RingColor, isMain ? 1.6f : 1f);
            var t1 = new PointF(dialCx + (float)(Math.Cos(a) * dialR * 0.86f), dialCy + (float)(Math.Sin(a) * dialR * 0.86f));
            var t2 = new PointF(dialCx + (float)(Math.Cos(a) * dialR), dialCy + (float)(Math.Sin(a) * dialR));
            g.DrawLine(tickPen, t1, t2);
        }

        float textX = 96;
        using var speedValueFont = new Font("Segoe UI", 15f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var speedUnitFont = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var degreesFont = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var cardinalFont = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var smallLabelFont = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var accentBrush = new SolidBrush(_accent);

        // Wind speed is the headline value (matches the other cards' style); direction is the
        // supporting detail, shown smaller underneath as "184° S" per the user's request.
        var speedSize = g.MeasureString(_speedValue, speedValueFont);
        g.DrawString(_speedValue, speedValueFont, mainLabelBrush, textX, 21);
        if (!string.IsNullOrEmpty(_speedUnit))
        {
            g.DrawString(_speedUnit, speedUnitFont, labelBrush, textX + speedSize.Width + 1, 25);
        }

        if (_degrees is { } degrees)
        {
            using var needleBrush = new SolidBrush(_needleColor);
            double rad = (degrees - 90) * Math.PI / 180;
            var tip = new PointF(dialCx + (float)(Math.Cos(rad) * dialR * 0.72f), dialCy + (float)(Math.Sin(rad) * dialR * 0.72f));
            var tailA1 = rad + Math.PI * 0.86;
            var tailA2 = rad - Math.PI * 0.86;
            var tail1 = new PointF(dialCx + (float)(Math.Cos(tailA1) * dialR * 0.22f), dialCy + (float)(Math.Sin(tailA1) * dialR * 0.22f));
            var tail2 = new PointF(dialCx + (float)(Math.Cos(tailA2) * dialR * 0.22f), dialCy + (float)(Math.Sin(tailA2) * dialR * 0.22f));
            using var needlePath = new GraphicsPath();
            needlePath.AddPolygon(new[] { tip, tail1, tail2 });
            g.FillPath(needleBrush, needlePath);
            using var hubBrush = new SolidBrush(CardBackground);
            g.FillEllipse(hubBrush, dialCx - 2.5f, dialCy - 2.5f, 5, 5);

            string degreesStr = $"{degrees:0}°";
            var degreesSize = g.MeasureString(degreesStr, degreesFont);
            g.DrawString(degreesStr, degreesFont, labelBrush, textX, 42);
            g.DrawString(ToCardinal(degrees), cardinalFont, accentBrush, textX + degreesSize.Width + 4, 42);
        }

        g.DrawString(Label, smallLabelFont, labelBrush, textX, 66);
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
