using System.Drawing.Drawing2D;

namespace AlpacaAllSkyWeather.TrayHost;

/// <summary>A dark-themed, owner-drawn card showing one weather metric: icon, value, unit and label.</summary>
public sealed class MetricCard : Control
{
    private static readonly Color CardBackground = Color.FromArgb(0x1E, 0x21, 0x2D);
    private static readonly Color CardBorder = Color.FromArgb(0x2A, 0x2E, 0x3D);
    private static readonly Color TextPrimary = Color.FromArgb(0xF5, 0xF6, 0xFA);
    private static readonly Color TextMuted = Color.FromArgb(0x8A, 0x8F, 0xA3);

    private readonly WeatherIcons.Drawer _icon;
    private readonly Color _accent;
    private string _value = "—";
    private string _unit = "";

    public string Label { get; }

    public MetricCard(WeatherIcons.Drawer icon, Color accent, string label)
    {
        _icon = icon;
        _accent = accent;
        Label = label;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(0x14, 0x16, 0x1F);
        Size = new Size(190, 108);
    }

    public void SetValue(string value, string unit)
    {
        if (_value == value && _unit == unit)
        {
            return;
        }

        _value = value;
        _unit = unit;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var rect = new RectangleF(0, 0, Width - 1, Height - 1);
        using var path = RoundedRect(rect, 14f);
        using var bgBrush = new SolidBrush(CardBackground);
        using var borderPen = new Pen(CardBorder, 1.2f);
        g.FillPath(bgBrush, path);
        g.DrawPath(borderPen, path);

        var iconRect = new RectangleF(16, 16, 30, 30);
        _icon(g, iconRect, _accent);

        using var valueFont = new Font("Segoe UI", 17f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var unitFont = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var labelFont = new Font("Segoe UI", 10.5f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var valueBrush = new SolidBrush(TextPrimary);
        using var mutedBrush = new SolidBrush(TextMuted);

        var valueSize = g.MeasureString(_value, valueFont);
        float textX = 16, textY = 58;
        g.DrawString(_value, valueFont, valueBrush, textX, textY);
        if (!string.IsNullOrEmpty(_unit))
        {
            g.DrawString(_unit, unitFont, mutedBrush, textX + valueSize.Width + 2, textY + 5);
        }
        g.DrawString(Label, labelFont, mutedBrush, textX, textY + 26);
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
