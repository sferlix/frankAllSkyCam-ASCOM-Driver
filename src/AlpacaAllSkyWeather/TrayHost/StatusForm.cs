using System.Globalization;
using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.TrayHost;

/// <summary>Shows every weather value the driver currently knows about, refreshing itself every 5 seconds.</summary>
public sealed class StatusForm : Form
{
    private static readonly Color Background = Color.FromArgb(0x14, 0x16, 0x1F);
    private static readonly Color TextPrimary = Color.FromArgb(0xF5, 0xF6, 0xFA);
    private static readonly Color TextMuted = Color.FromArgb(0x8A, 0x8F, 0xA3);

    private static readonly Color AccentTemp = Color.FromArgb(0xFF, 0x8A, 0x5B);
    private static readonly Color AccentHumidity = Color.FromArgb(0x4F, 0xC3, 0xF7);
    private static readonly Color AccentPressure = Color.FromArgb(0xB3, 0x9D, 0xDB);
    private static readonly Color AccentCloud = Color.FromArgb(0x90, 0xA4, 0xAE);
    private static readonly Color AccentSky = Color.FromArgb(0xFF, 0xD5, 0x4F);
    private static readonly Color AccentRain = Color.FromArgb(0x4F, 0xA8, 0xE0);
    private static readonly Color AccentWind = Color.FromArgb(0x66, 0xD9, 0xC2);
    private static readonly Color AccentStar = Color.FromArgb(0xCE, 0x93, 0xD8);
    private static readonly Color CompassNeedle = Color.FromArgb(0xFF, 0x8A, 0x5B);

    private static readonly Color DotFresh = Color.FromArgb(0x4C, 0xD9, 0x7B);
    private static readonly Color DotAging = Color.FromArgb(0xFF, 0xC1, 0x07);
    private static readonly Color DotStale = Color.FromArgb(0xE5, 0x73, 0x73);

    private readonly WeatherState _state;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly Panel _headerPanel;
    private readonly Label _subtitleLabel;
    private Color _dotColor = DotStale;

    private readonly MetricCard _temperature;
    private readonly MetricCard _humidity;
    private readonly MetricCard _dewPoint;
    private readonly MetricCard _pressure;
    private readonly MetricCard _cloudCover;
    private readonly MetricCard _skyBrightness;
    private readonly MetricCard _skyQuality;
    private readonly MetricCard _rainRate;
    private readonly MetricCard _windGust;
    private readonly CompassCard _wind;
    private readonly MetricCard _starCount;
    private readonly NightCard _night;

    public StatusForm(WeatherState state)
    {
        _state = state;

        Text = "frankAllSkyCam ASCOM Driver";
        BackColor = Background;
        ClientSize = new Size(14 * 2 + 3 * 168 + 2 * 8, 50 + 14 * 2 + 4 * 94 + 3 * 8);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.Manual;
        Icon = TrayIconFactory.CreateIcon();
        Load += (_, _) => PositionNearTray();

        _headerPanel = new Panel { Dock = DockStyle.Top, Height = 50, BackColor = Background };
        _headerPanel.Paint += HeaderPanel_Paint;

        var titleLabel = new Label
        {
            Text = "frankAllSkyCam",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel),
            ForeColor = TextPrimary,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(14, 8),
        };

        _subtitleLabel = new Label
        {
            Text = "in attesa del primo aggiornamento...",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Pixel),
            ForeColor = TextMuted,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(26, 30),
        };

        _headerPanel.Controls.Add(titleLabel);
        _headerPanel.Controls.Add(_subtitleLabel);

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            BackColor = Background,
            Padding = new Padding(10),
        };
        for (int i = 0; i < 3; i++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
        }
        for (int i = 0; i < 4; i++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));
        }

        _temperature = new MetricCard(WeatherIcons.Thermometer, AccentTemp, "Temperatura") { Margin = new Padding(4), Dock = DockStyle.Fill };
        _humidity = new MetricCard(WeatherIcons.Droplet, AccentHumidity, "Umidita") { Margin = new Padding(4), Dock = DockStyle.Fill };
        _dewPoint = new MetricCard(WeatherIcons.Droplet, AccentHumidity, "Punto di rugiada") { Margin = new Padding(4), Dock = DockStyle.Fill };
        _pressure = new MetricCard(WeatherIcons.Gauge, AccentPressure, "Pressione") { Margin = new Padding(4), Dock = DockStyle.Fill };
        _cloudCover = new MetricCard(WeatherIcons.Cloud, AccentCloud, "Copertura nuvolosa") { Margin = new Padding(4), Dock = DockStyle.Fill };
        _skyBrightness = new MetricCard(WeatherIcons.Sun, AccentSky, "Luminosita cielo") { Margin = new Padding(4), Dock = DockStyle.Fill };
        _skyQuality = new MetricCard(WeatherIcons.Star, AccentSky, "Qualita cielo") { Margin = new Padding(4), Dock = DockStyle.Fill };
        _rainRate = new MetricCard(WeatherIcons.DropletWithRain, AccentRain, "Pioggia") { Margin = new Padding(4), Dock = DockStyle.Fill };
        _windGust = new MetricCard(WeatherIcons.Wind, AccentWind, "Raffica") { Margin = new Padding(4), Dock = DockStyle.Fill };
        _wind = new CompassCard(AccentWind, CompassNeedle, "Vento") { Margin = new Padding(4), Dock = DockStyle.Fill };
        _starCount = new MetricCard(WeatherIcons.FiveStar, AccentStar, "Stelle rilevate") { Margin = new Padding(4), Dock = DockStyle.Fill };
        _night = new NightCard(AccentSky) { Margin = new Padding(4), Dock = DockStyle.Fill };

        grid.Controls.Add(_temperature, 0, 0);
        grid.Controls.Add(_humidity, 1, 0);
        grid.Controls.Add(_dewPoint, 2, 0);
        grid.Controls.Add(_pressure, 0, 1);
        grid.Controls.Add(_cloudCover, 1, 1);
        grid.Controls.Add(_skyBrightness, 2, 1);
        grid.Controls.Add(_skyQuality, 0, 2);
        grid.Controls.Add(_rainRate, 1, 2);
        grid.Controls.Add(_windGust, 2, 2);
        grid.Controls.Add(_wind, 0, 3);
        grid.Controls.Add(_starCount, 1, 3);
        grid.Controls.Add(_night, 2, 3);

        Controls.Add(grid);
        Controls.Add(_headerPanel);

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _refreshTimer.Tick += (_, _) => RefreshFromState();
        _refreshTimer.Start();
        RefreshFromState();
    }

    private void HeaderPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var dotBrush = new SolidBrush(_dotColor);
        g.FillEllipse(dotBrush, 14, 32, 7, 7);

        // Text badge only, not the ASCOM/Alpaca logo: the official marks are restricted to
        // registered/certified products, which this personal driver is not (see ascom-standards.org
        // Requirements). It has passed a ConformU conformance check, so the text itself is accurate.
        const string badgeText = "ASCOM Alpaca";
        using var badgeFont = new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Pixel);
        var textSize = g.MeasureString(badgeText, badgeFont);
        var badgeRect = new RectangleF(_headerPanel.Width - textSize.Width - 22, 12, textSize.Width + 14, 18);
        using var badgePath = RoundedRect(badgeRect, 9f);
        using var badgeBorder = new Pen(AccentWind, 1f);
        g.DrawPath(badgeBorder, badgePath);
        using var badgeTextBrush = new SolidBrush(AccentWind);
        g.DrawString(badgeText, badgeFont, badgeTextBrush, badgeRect.X + 7, badgeRect.Y + 3);
    }

    private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Places the window near the system tray (bottom-right of the working area), since
    /// it's opened from the tray icon and WinForms has no direct API for the icon's own position.</summary>
    private void PositionNearTray()
    {
        var workingArea = Screen.FromControl(this).WorkingArea;
        Location = new Point(workingArea.Right - Width - 12, workingArea.Bottom - Height - 12);
    }

    private void RefreshFromState()
    {
        var snapshot = _state.TryGetLatest();
        if (snapshot is null)
        {
            _dotColor = DotStale;
            _subtitleLabel.Text = "in attesa del primo aggiornamento...";
            _headerPanel.Invalidate();
            return;
        }

        var age = DateTimeOffset.UtcNow - snapshot.PolledAtUtc;
        _dotColor = age.TotalSeconds switch
        {
            < 120 => DotFresh,
            < 600 => DotAging,
            _ => DotStale,
        };
        _subtitleLabel.Text = $"aggiornato alle {snapshot.PolledAtUtc.ToLocalTime():HH:mm:ss} - frankAllSkyCam";
        _headerPanel.Invalidate();

        var c = CultureInfo.InvariantCulture;
        _temperature.SetValue(snapshot.Temperature.ToString("0.0", c), "°C");
        _humidity.SetValue(snapshot.Humidity.ToString("0", c), "%");
        _dewPoint.SetValue(snapshot.DewPoint.ToString("0.0", c), "°C");
        _pressure.SetValue(snapshot.Pressure.ToString("0.0", c), "hPa");
        _cloudCover.SetValue(snapshot.CloudCover.ToString("0", c), "%");
        _skyBrightness.SetValue(snapshot.SkyBrightness.ToString("0", c), "lux");
        _skyQuality.SetValue(snapshot.SkyQuality.ToString("0.0", c), "mag/arcsec²");
        _rainRate.SetValue(snapshot.RainRate.ToString("0.0", c), "mm/h");
        _windGust.SetValue(snapshot.WindGust.ToString("0.0", c), "m/s");
        _wind.SetWind((float)snapshot.WindDirection, snapshot.WindSpeed.ToString("0.0", c), "m/s");
        _starCount.SetValue(((int)snapshot.StarCount).ToString(c), "");
        _night.SetWindow(
            snapshot.NightStart.ToLocalTime().ToString("HH:mm", c),
            snapshot.NightEnd.ToLocalTime().ToString("HH:mm", c));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _refreshTimer.Stop();
            _refreshTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
