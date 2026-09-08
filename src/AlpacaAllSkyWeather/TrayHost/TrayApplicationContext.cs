using AlpacaAllSkyWeather.Alpaca;
using AlpacaAllSkyWeather.Weather;
using Microsoft.Extensions.Options;

namespace AlpacaAllSkyWeather.TrayHost;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly WebApplication _app;
    private readonly WeatherState _state;
    private readonly SafetyMonitorDevice _safety;
    private readonly TelegramNotifier _notifier;
    private readonly string _appSettingsPath;
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private readonly System.Windows.Forms.Timer _tooltipTimer;
    private readonly Control _uiThreadMarshal;
    private bool _running = true;
    private StatusForm? _statusForm;

    public TrayApplicationContext(WebApplication app, int httpPort)
    {
        _app = app;
        _state = app.Services.GetRequiredService<WeatherState>();
        _safety = app.Services.GetRequiredService<SafetyMonitorDevice>();
        _notifier = app.Services.GetRequiredService<TelegramNotifier>();
        _appSettingsPath = Path.Combine(app.Environment.ContentRootPath, "appsettings.json");
        _icon = TrayIconFactory.CreateIcon();

        _uiThreadMarshal = new Control();
        _uiThreadMarshal.CreateControl();
        app.Services.GetRequiredService<StatusImageRenderer>().Render = RenderStatusImageAsync;

        var menu = new ContextMenuStrip();

        var statusItem = new ToolStripMenuItem("Show status");
        statusItem.Click += (_, _) => ShowStatusWindow();
        menu.Items.Add(statusItem);

        var notificationsItem = new ToolStripMenuItem("Notification settings...");
        notificationsItem.Click += (_, _) => ShowNotificationSettings();
        menu.Items.Add(notificationsItem);

        var thresholdsItem = new ToolStripMenuItem("Safety threshold settings...");
        thresholdsItem.Click += (_, _) => ShowSafetyRulesSettings();
        menu.Items.Add(thresholdsItem);
        menu.Items.Add(new ToolStripSeparator());

        var toggleItem = new ToolStripMenuItem("Stop");
        toggleItem.Click += async (_, _) =>
        {
            if (_running)
            {
                await _app.StopAsync();
                toggleItem.Text = "Start";
            }
            else
            {
                await _app.StartAsync();
                toggleItem.Text = "Stop";
            }
            _running = !_running;
        };
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitThread();
        menu.Items.Add(toggleItem);
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = $"AllSky Weather (port {httpPort})",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => ShowStatusWindow();

        _tooltipTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _tooltipTimer.Tick += (_, _) => UpdateTooltip(httpPort);
        _tooltipTimer.Start();
        UpdateTooltip(httpPort);

        ThreadExit += async (_, _) =>
        {
            _tooltipTimer.Stop();
            _notifyIcon.Visible = false;
            _statusForm?.Close();
            _icon.Dispose();
            _uiThreadMarshal.Dispose();
            await _app.StopAsync();
        };
    }

    private void ShowStatusWindow()
    {
        if (_statusForm is null || _statusForm.IsDisposed)
        {
            _statusForm = new StatusForm(_state, _safety);
            _statusForm.Show();
        }

        if (_statusForm.WindowState == FormWindowState.Minimized)
        {
            _statusForm.WindowState = FormWindowState.Normal;
        }

        _statusForm.Activate();
    }

    private void ShowNotificationSettings()
    {
        var current = _app.Services.GetRequiredService<IOptionsMonitor<NotificationOptions>>().CurrentValue;
        using var form = new NotificationSettingsForm(current, _notifier, _appSettingsPath);
        form.ShowDialog();
    }

    private void ShowSafetyRulesSettings()
    {
        var current = _app.Services.GetRequiredService<IOptionsMonitor<SafetyRulesOptions>>().CurrentValue;
        using var form = new SafetyRulesSettingsForm(current, _appSettingsPath);
        form.ShowDialog();
    }

    /// <summary>Renders an off-screen copy of <see cref="StatusForm"/> (positioned far outside the
    /// virtual screen so it never becomes visible) to a PNG, for the Telegram "now" command. Must
    /// marshal onto the UI thread since WinForms controls can only be touched from the thread that
    /// created them; this method itself runs on a hosted-service thread-pool thread.</summary>
    private Task<byte[]?> RenderStatusImageAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<byte[]?>();
        _uiThreadMarshal.BeginInvoke(new Action(() =>
        {
            try
            {
                using var form = new StatusForm(_state, _safety, positionNearTray: false)
                {
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-32000, -32000),
                    ShowInTaskbar = false,
                };
                form.Show();
                using var bmp = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.Size));
                form.Close();

                using var stream = new MemoryStream();
                bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                tcs.TrySetResult(stream.ToArray());
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }));
        return tcs.Task;
    }

    private void UpdateTooltip(int httpPort)
    {
        var snapshot = _state.TryGetLatest();
        var lastPoll = snapshot is null
            ? "no successful poll yet"
            : $"last poll: {snapshot.PolledAtUtc.ToLocalTime():HH:mm:ss}";
        // NotifyIcon.Text has a 63-character limit.
        var text = $"AllSky Weather :{httpPort} - {lastPoll}";
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
    }
}
