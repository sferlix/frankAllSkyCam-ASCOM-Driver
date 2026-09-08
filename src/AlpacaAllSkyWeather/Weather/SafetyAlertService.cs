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
    private readonly TelegramNotifier _notifier;
    private readonly IOptionsMonitor<NotificationOptions> _options;
    private bool? _lastIsSafe;

    public SafetyAlertService(SafetyMonitorDevice safety, TelegramNotifier notifier, IOptionsMonitor<NotificationOptions> options)
    {
        _safety = safety;
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
        if (!options.Enabled)
        {
            return;
        }

        await _notifier.SendAsync(options.TelegramBotToken, options.TelegramChatId, message, cancellationToken);
    }

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
