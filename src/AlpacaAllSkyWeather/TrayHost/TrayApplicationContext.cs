using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.TrayHost;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly WebApplication _app;
    private readonly WeatherState _state;
    private readonly NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private readonly System.Windows.Forms.Timer _tooltipTimer;
    private bool _running = true;
    private StatusForm? _statusForm;

    public TrayApplicationContext(WebApplication app, int httpPort)
    {
        _app = app;
        _state = app.Services.GetRequiredService<WeatherState>();
        _icon = TrayIconFactory.CreateIcon();

        var menu = new ContextMenuStrip();

        var statusItem = new ToolStripMenuItem("Mostra stato");
        statusItem.Click += (_, _) => ShowStatusWindow();
        menu.Items.Add(statusItem);
        menu.Items.Add(new ToolStripSeparator());

        var toggleItem = new ToolStripMenuItem("Ferma");
        toggleItem.Click += async (_, _) =>
        {
            if (_running)
            {
                await _app.StopAsync();
                toggleItem.Text = "Avvia";
            }
            else
            {
                await _app.StartAsync();
                toggleItem.Text = "Ferma";
            }
            _running = !_running;
        };
        var exitItem = new ToolStripMenuItem("Esci");
        exitItem.Click += (_, _) => ExitThread();
        menu.Items.Add(toggleItem);
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = $"AllSky Weather (porta {httpPort})",
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
            await _app.StopAsync();
        };
    }

    private void ShowStatusWindow()
    {
        if (_statusForm is null || _statusForm.IsDisposed)
        {
            _statusForm = new StatusForm(_state);
            _statusForm.Show();
        }

        if (_statusForm.WindowState == FormWindowState.Minimized)
        {
            _statusForm.WindowState = FormWindowState.Normal;
        }

        _statusForm.Activate();
    }

    private void UpdateTooltip(int httpPort)
    {
        var snapshot = _state.TryGetLatest();
        var lastPoll = snapshot is null
            ? "nessun poll riuscito ancora"
            : $"ultimo poll: {snapshot.PolledAtUtc.ToLocalTime():HH:mm:ss}";
        // NotifyIcon.Text has a 63-character limit.
        var text = $"AllSky Weather :{httpPort} - {lastPoll}";
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
    }
}
