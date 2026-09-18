using System.Globalization;
using System.Text.Json;
using AlpacaAllSkyWeather.TrayHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlpacaAllSkyWeather.Weather;

/// <summary>Result of <see cref="TelegramCommandListener.DownloadImageAsync"/>: either the image
/// bytes plus the image format Telegram needs to be told about, or a human-readable reason it
/// couldn't be fetched.</summary>
public readonly record struct ImageDownloadResult(byte[]? Bytes, string? Error, string ContentType = "image/jpeg", string FileName = "allskycam.jpg");

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
            var download = await DownloadImageAsync(_httpClient, allSkyCamUrl, _logger, ct);
            if (download.Bytes is not null)
            {
                var sendResult = await _notifier.SendPhotoAsync(
                    botToken, chatId, download.Bytes, caption: "🌌 AllSkyCam", ct,
                    contentType: download.ContentType, fileName: download.FileName);
                if (!sendResult.Success)
                {
                    // Reported to the chat (not just logged): this is a tray app with no visible
                    // console/log file, so a warning-level log is otherwise unreachable by the user.
                    await _notifier.SendAsync(botToken, chatId, $"⚠️ Could not send the AllSkyCam image: {sendResult.ErrorDetail}", ct);
                }
            }
            else
            {
                await _notifier.SendAsync(botToken, chatId, $"⚠️ Could not fetch the AllSkyCam image: {download.Error}", ct);
            }
        }

        return result;
    }

    /// <summary>Downloads the AllSkyCam's live image so it can be forwarded to Telegram. Never
    /// throws: a blank URL, a failed request, or a non-success response all come back as a null
    /// <see cref="ImageDownloadResult.Bytes"/> with a human-readable <see
    /// cref="ImageDownloadResult.Error"/> — a broken camera feed shouldn't stop the status
    /// screenshot from being sent, but the reason should still reach the user.</summary>
    internal static async Task<ImageDownloadResult> DownloadImageAsync(HttpClient httpClient, string url, ILogger logger, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new ImageDownloadResult(null, "no AllSkyCam image URL is configured.");
        }

        try
        {
            using var response = await httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to download the AllSkyCam image: {StatusCode}", response.StatusCode);
                return new ImageDownloadResult(null, $"the camera responded {(int)response.StatusCode} {response.StatusCode}.");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            var (contentType, fileName) = DetectImageFormat(bytes, response.Content.Headers.ContentType?.MediaType);
            return new ImageDownloadResult(bytes, null, contentType, fileName);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Error while downloading the AllSkyCam image");
            return new ImageDownloadResult(null, $"network error: {ex.Message}");
        }
    }

    /// <summary>Picks the (ContentType, FileName) pair to tell Telegram about, since sending a
    /// JPEG declared as "image/png" (or vice versa) makes Telegram's own image processing reject
    /// the upload outright. The file's own magic bytes are sniffed first and trusted over the
    /// declared Content-Type, since many camera HTTP servers mislabel or omit that header (e.g.
    /// "application/octet-stream"); the declared type is only a fallback for the rare case the
    /// bytes don't match a known signature (e.g. too short to sniff).</summary>
    internal static (string ContentType, string FileName) DetectImageFormat(byte[] bytes, string? declaredContentType)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
        {
            return ("image/png", "allskycam.png");
        }
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
        {
            return ("image/jpeg", "allskycam.jpg");
        }
        if (bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38)
        {
            return ("image/gif", "allskycam.gif");
        }
        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D)
        {
            return ("image/bmp", "allskycam.bmp");
        }

        return declaredContentType?.Trim().ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ("image/jpeg", "allskycam.jpg"),
            "image/png" => ("image/png", "allskycam.png"),
            "image/gif" => ("image/gif", "allskycam.gif"),
            "image/bmp" => ("image/bmp", "allskycam.bmp"),
            // Most AllSky camera feeds are JPEG; fall back to it when the format can't be determined.
            _ => ("image/jpeg", "allskycam.jpg"),
        };
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
            // Thrown (not just logged) so the caller's existing catch+backoff kicks in — most
            // commonly a 409 Conflict from a second machine polling the same bot token, which
            // would otherwise retry in a tight loop hammering the Telegram API.
            throw new HttpRequestException($"Telegram getUpdates returned {(int)response.StatusCode} {response.StatusCode}: {body}");
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
