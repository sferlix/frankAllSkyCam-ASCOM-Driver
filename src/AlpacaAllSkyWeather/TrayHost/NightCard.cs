using System.Drawing.Drawing2D;

namespace AlpacaAllSkyWeather.TrayHost;

/// <summary>Card showing the astronomical night window (start/end times) reported by frankAllSkyCam.</summary>
public sealed class NightCard : Control
{
    private static readonly Color CardBackground = Color.FromArgb(0x1E, 0x21, 0x2D);
    private static readonly Color CardBorder = Color.FromArgb(0x2A, 0x2E, 0x3D);
    private static readonly Color TextPrimary = Color.FromArgb(0xF5, 0xF6, 0xFA);
    private static readonly Color TextMuted = Color.FromArgb(0x8A, 0x8F, 0xA3);

    private readonly Color _accent;
    private string _start = "—";
    private string _end = "—";

    public NightCard(Color accent)
    {
        _accent = accent;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(0x14, 0x16, 0x1F);
        Size = new Size(168, 94);
    }

    public void SetWindow(string start, string end)
    {
        if (_start == start && _end == end)
        {
            return;
        }

        _start = start;
        _end = end;
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

        var iconRect = new RectangleF(14, 13, 26, 26);
        WeatherIcons.Moon(g, iconRect, _accent);

        using var valueFont = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var captionFont = new Font("Segoe UI", 8.5f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var valueBrush = new SolidBrush(TextPrimary);
        using var mutedBrush = new SolidBrush(TextMuted);

        const float textX = 14;
        g.DrawString("NIGHT START", captionFont, mutedBrush, textX, 40);
        g.DrawString(_start, valueFont, valueBrush, textX, 49);
        g.DrawString("NIGHT END", captionFont, mutedBrush, textX, 65);
        g.DrawString(_end, valueFont, valueBrush, textX, 74);
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
