using System.Globalization;
using System.Text.Json;
using AlpacaAllSkyWeather.TrayHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlpacaAllSkyWeather.Weather;

/// <summary>Long-polls Telegram for incoming messages and replies to "now"/"status" with a
/// screenshot of the status window — the exact same view the user sees via "Show status", not a
/// separately maintained text summary. Owns its own HttpClient for the same reason as
/// <see cref="TelegramNotifier"/> (see its remarks on IHttpClientFactory).</summary>
public sealed class TelegramCommandListener : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly IOptionsMonitor<NotificationOptions> _options;
    private readonly TelegramNotifier _notifier;
    private readonly StatusImageRenderer _renderer;
    private readonly ILogger<TelegramCommandListener> _logger;
    private readonly HttpClient _httpClient;
    private long _offset;

    public TelegramCommandListener(
        IOptionsMonitor<NotificationOptions> options,
        TelegramNotifier notifier,
        StatusImageRenderer renderer,
        ILogger<TelegramCommandListener> logger)
    {
        _options = options;
        _notifier = notifier;
        _renderer = renderer;
        _logger = logger;
        _httpClient = new HttpClient(TelegramNotifier.CreateIPv4OnlyHandler());
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _options.CurrentValue;
            if (!options.Enabled || string.IsNullOrWhiteSpace(options.TelegramBotToken) || string.IsNullOrWhiteSpace(options.TelegramChatId))
            {
                await DelayIgnoringCancellation(stoppingToken);
                continue;
            }

            try
            {
                _offset = await PollOnceAsync(
                    _httpClient, options.TelegramBotToken, options.TelegramChatId, _offset,
                    (chatId, ct) => ReplyWithStatusAsync(options.TelegramBotToken, chatId, ct),
                    _logger, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Error while polling Telegram for commands");
                await DelayIgnoringCancellation(stoppingToken);
            }
        }
    }

    private async Task<TelegramSendResult> ReplyWithStatusAsync(string botToken, string chatId, CancellationToken ct)
    {
        var image = await _renderer.RenderAsync(ct);
        var result = image is null
            ? await _notifier.SendAsync(botToken, chatId, "No data available at the moment.", ct)
            : await _notifier.SendPhotoAsync(botToken, chatId, image, caption: null, ct);

        var allSkyCamUrl = _options.CurrentValue.AllSkyCamImageUrl;
        if (!string.IsNullOrWhiteSpace(allSkyCamUrl))
        {
            var camImage = await DownloadImageAsync(_httpClient, allSkyCamUrl, _logger, ct);
            if (camImage is not null)
            {
                await _notifier.SendPhotoAsync(botToken, chatId, camImage, caption: "🌌 AllSkyCam", ct);
            }
        }

        return result;
    }

    /// <summary>Downloads the AllSkyCam's live image so it can be forwarded to Telegram. Returns
    /// null (never throws) when the URL is blank, the request fails, or the response isn't
    /// successful — a broken camera feed shouldn't stop the status screenshot from being sent.</summary>
    internal static async Task<byte[]?> DownloadImageAsync(HttpClient httpClient, string url, ILogger logger, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        try
        {
            using var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to download the AllSkyCam image: {StatusCode}", response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Error while downloading the AllSkyCam image");
            return null;
        }
    }

    private static async Task DelayIgnoringCancellation(CancellationToken ct)
    {
        try { await Task.Delay(RetryDelay, ct); } catch (TaskCanceledException) { }
    }

    /// <summary>Matches "now"/"status", with or without a leading "/", case-insensitively.</summary>
    internal static bool IsRecognizedCommand(string text)
    {
        var trimmed = text.Trim().TrimStart('/');
        return trimmed.Equals("now", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("status", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Fetches one batch of updates via long-polling and replies (via
    /// <paramref name="sendReplyAsync"/>) to every recognized command coming from
    /// <paramref name="expectedChatId"/>, ignoring everything else. Returns the offset to pass on
    /// the next call.</summary>
    internal static async Task<long> PollOnceAsync(
        HttpClient httpClient,
        string botToken,
        string expectedChatId,
        long offset,
        Func<string, CancellationToken, Task<TelegramSendResult>> sendReplyAsync,
        ILogger logger,
        CancellationToken ct)
    {
        var url = $"https://api.telegram.org/bot{botToken}/getUpdates?offset={offset}&timeout=25";
        using var response = await httpClient.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Telegram getUpdates returned {StatusCode}: {Body}", response.StatusCode, body);
            return offset;
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("result", out var results))
        {
            return offset;
        }

        var newOffset = offset;
        foreach (var update in results.EnumerateArray())
        {
            newOffset = update.GetProperty("update_id").GetInt64() + 1;

            if (!update.TryGetProperty("message", out var message))
            {
                continue;
            }

            var chatId = message.GetProperty("chat").GetProperty("id").GetInt64().ToString(CultureInfo.InvariantCulture);
            var text = message.TryGetProperty("text", out var textProp) ? textProp.GetString() ?? "" : "";

            if (chatId != expectedChatId || !IsRecognizedCommand(text))
            {
                continue;
            }

            await sendReplyAsync(chatId, ct);
        }

        return newOffset;
    }
}
