using AlpacaAllSkyWeather.Alpaca;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AlpacaAllSkyWeather.Weather;

/// <summary>Watches <see cref="SafetyMonitorDevice.IsSafe"/> and sends a Telegram alert only on
/// state transitions (safe→unsafe and back), not on every poll — one alert per event, not spam.</summary>
public sealed class SafetyAlertService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    private readonly SafetyMonitorDevice _safety;
    private readonly WeatherState _weatherState;
    private readonly TelegramNotifier _notifier;
    private readonly IOptionsMonitor<NotificationOptions> _options;
    private bool? _lastIsSafe;

    public SafetyAlertService(SafetyMonitorDevice safety, WeatherState weatherState, TelegramNotifier notifier, IOptionsMonitor<NotificationOptions> options)
    {
        _safety = safety;
        _weatherState = weatherState;
        _notifier = notifier;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await CheckAndNotifyAsync(stoppingToken);

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    internal async Task CheckAndNotifyAsync(CancellationToken cancellationToken)
    {
        var isSafe = _safety.IsSafe;
        var message = BuildTransitionMessage(_lastIsSafe, isSafe, _safety.UnsafeReasons);
        _lastIsSafe = isSafe;

        if (message is null)
        {
            return;
        }

        var options = _options.CurrentValue;
        if (!ShouldNotify(options.Enabled, options.NotifyOnlyAtNight, _weatherState.TryGetLatest(), DateTimeOffset.UtcNow))
        {
            return;
        }

        await _notifier.SendAsync(options.TelegramBotToken, options.TelegramChatId, message, cancellationToken);
    }

    /// <summary>The decision <see cref="CheckAndNotifyAsync"/> makes once it already has a
    /// transition message to send: notifications must be enabled, and — when <paramref
    /// name="notifyOnlyAtNight"/> is set — "now" must fall inside the current night window.</summary>
    internal static bool ShouldNotify(bool enabled, bool notifyOnlyAtNight, WeatherSnapshot? snapshot, DateTimeOffset now)
        => enabled && (!notifyOnlyAtNight || IsWithinNightWindow(snapshot, now));

    /// <summary>True when <paramref name="now"/> falls inside the snapshot's night window (same
    /// inclusive bounds as StatusForm's day/night icon check). No snapshot yet counts as "not
    /// night" — conservative, so a fresh start doesn't fire a notification before real sun times
    /// are known.</summary>
    internal static bool IsWithinNightWindow(WeatherSnapshot? snapshot, DateTimeOffset now)
        => snapshot is not null && now >= snapshot.NightStart && now <= snapshot.NightEnd;

    /// <summary>Returns the alert text for a safe/unsafe transition, or null if there is nothing
    /// to report (first check, or the state didn't actually change).</summary>
    internal static string? BuildTransitionMessage(bool? previousIsSafe, bool currentIsSafe, IReadOnlyList<string> unsafeReasons)
    {
        if (previousIsSafe is null || previousIsSafe == currentIsSafe)
        {
            return null;
        }

        return currentIsSafe
            ? "✅ AllSky Weather: conditions are safe again for imaging."
            : $"⚠️ AllSky Weather: conditions are NOT safe - {string.Join(", ", unsafeReasons)}";
    }
}
