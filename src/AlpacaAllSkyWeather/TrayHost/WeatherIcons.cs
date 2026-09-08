using System.Drawing.Drawing2D;

namespace AlpacaAllSkyWeather.TrayHost;

/// <summary>Hand-drawn flat vector icons (no image assets) used on the status window cards and the tray icon.</summary>
public static class WeatherIcons
{
    public delegate void Drawer(Graphics g, RectangleF rect, Color color);

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

    public static void Thermometer(Graphics g, RectangleF r, Color c)
    {
        using var brush = new SolidBrush(c);
        float cx = r.X + r.Width * 0.5f;
        float bulbR = r.Width * 0.22f;
        float stemW = r.Width * 0.16f;
        g.FillEllipse(brush, cx - bulbR, r.Bottom - bulbR * 2, bulbR * 2, bulbR * 2);
        using var stem = RoundedRect(new RectangleF(cx - stemW / 2, r.Y, stemW, r.Height - bulbR * 1.3f), stemW / 2);
        g.FillPath(brush, stem);
    }

    public static void Droplet(Graphics g, RectangleF r, Color c) => Droplet(g, r, c, withRainTicks: false);

    public static void DropletWithRain(Graphics g, RectangleF r, Color c) => Droplet(g, r, c, withRainTicks: true);

    private static void Droplet(Graphics g, RectangleF r, Color c, bool withRainTicks)
    {
        using var brush = new SolidBrush(c);
        using var path = new GraphicsPath();
        float w = r.Width, h = r.Height;
        var top = new PointF(r.X + w / 2, r.Y);
        var bottom = new PointF(r.X + w / 2, r.Bottom);
        path.AddBezier(top, new PointF(r.X + w * 0.05f, r.Y + h * 0.55f), new PointF(r.X, r.Bottom - h * 0.18f), bottom);
        path.AddBezier(bottom, new PointF(r.Right, r.Bottom - h * 0.18f), new PointF(r.Right - w * 0.05f, r.Y + h * 0.55f), top);
        g.FillPath(brush, path);

        if (withRainTicks)
        {
            using var pen = new Pen(c, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawLine(pen, r.X - 2, r.Y - 2, r.X - 6, r.Y + 6);
            g.DrawLine(pen, r.Right + 2, r.Y - 2, r.Right - 2, r.Y + 6);
        }
    }

    public static void Cloud(Graphics g, RectangleF r, Color c)
    {
        using var brush = new SolidBrush(c);
        float w = r.Width, h = r.Height;
        using var baseRect = RoundedRect(new RectangleF(r.X, r.Y + h * 0.45f, w, h * 0.5f), h * 0.25f);
        g.FillPath(brush, baseRect);
        g.FillEllipse(brush, r.X + w * 0.06f, r.Y + h * 0.28f, w * 0.42f, h * 0.5f);
        g.FillEllipse(brush, r.X + w * 0.34f, r.Y, w * 0.5f, h * 0.62f);
        g.FillEllipse(brush, r.X + w * 0.58f, r.Y + h * 0.22f, w * 0.4f, h * 0.5f);
    }

    public static void Sun(Graphics g, RectangleF r, Color c)
    {
        using var brush = new SolidBrush(c);
        using var pen = new Pen(c, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        float coreR = r.Width * 0.24f;
        g.FillEllipse(brush, cx - coreR, cy - coreR, coreR * 2, coreR * 2);
        float rayInner = coreR * 1.45f, rayOuter = r.Width * 0.5f;
        for (int i = 0; i < 8; i++)
        {
            double a = i * Math.PI / 4;
            var p1 = new PointF(cx + (float)(Math.Cos(a) * rayInner), cy + (float)(Math.Sin(a) * rayInner));
            var p2 = new PointF(cx + (float)(Math.Cos(a) * rayOuter), cy + (float)(Math.Sin(a) * rayOuter));
            g.DrawLine(pen, p1, p2);
        }
    }

    public static void Star(Graphics g, RectangleF r, Color c) => Star(g, r, c, points: 4);

    public static void FiveStar(Graphics g, RectangleF r, Color c) => Star(g, r, c, points: 5);

    private static void Star(Graphics g, RectangleF r, Color c, int points)
    {
        using var brush = new SolidBrush(c);
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2;
        float outerR = r.Width / 2, innerR = outerR * 0.38f;
        int n = points * 2;
        var pts = new PointF[n];
        for (int i = 0; i < n; i++)
        {
            double a = Math.PI * i / points - Math.PI / 2;
            float rad = i % 2 == 0 ? outerR : innerR;
            pts[i] = new PointF(cx + (float)(Math.Cos(a) * rad), cy + (float)(Math.Sin(a) * rad));
        }
        using var path = new GraphicsPath();
        path.AddPolygon(pts);
        g.FillPath(brush, path);
    }

    public static void Gauge(Graphics g, RectangleF r, Color c)
    {
        using var pen = new Pen(c, 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var arcRect = new RectangleF(r.X, r.Y + r.Height * 0.1f, r.Width, r.Height * 0.95f);
        g.DrawArc(pen, arcRect, 200, 140);
        float cx = r.X + r.Width / 2, cy = r.Y + r.Height * 0.62f;
        double a = (200 + 140 * 0.62) * Math.PI / 180;
        g.DrawLine(pen, cx, cy, cx + (float)(Math.Cos(a) * r.Width * 0.34f), cy + (float)(Math.Sin(a) * r.Width * 0.34f));
        using var brush = new SolidBrush(c);
        g.FillEllipse(brush, cx - 3, cy - 3, 6, 6);
    }

    public static void Wind(Graphics g, RectangleF r, Color c)
    {
        using var pen = new Pen(c, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float w = r.Width, h = r.Height;

        void Wave(float y, float ampFactor, float startX, float endX)
        {
            using var path = new GraphicsPath();
            path.AddBezier(new PointF(r.X + startX, r.Y + y), new PointF(r.X + startX + w * 0.15f, r.Y + y - h * ampFactor),
                new PointF(r.X + endX - w * 0.15f, r.Y + y + h * ampFactor), new PointF(r.X + endX, r.Y + y));
            g.DrawPath(pen, path);
        }

        Wave(h * 0.28f, 0.12f, 0, w * 0.85f);
        Wave(h * 0.55f, 0.12f, w * 0.1f, w);
        Wave(h * 0.82f, 0.12f, 0, w * 0.7f);
    }
}
