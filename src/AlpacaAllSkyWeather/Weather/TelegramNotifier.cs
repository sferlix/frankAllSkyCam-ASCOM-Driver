using Microsoft.Extensions.Logging;

namespace AlpacaAllSkyWeather.Weather;

/// <summary>Result of a Telegram send attempt. <see cref="ErrorDetail"/> carries Telegram's own
/// error description (or the network exception message) so a human can actually diagnose a
/// failure instead of just seeing "it didn't work".</summary>
public readonly record struct TelegramSendResult(bool Success, string? ErrorDetail)
{
    public static TelegramSendResult Ok() => new(true, null);

    public static TelegramSendResult Fail(string detail) => new(false, detail);
}

/// <summary>Sends alert messages via the Telegram Bot API (free, no account billing —
/// see <see cref="NotificationOptions"/>). Missing token/chat ID is treated as "not configured
/// yet", not an error: it returns a failure result without making a request.</summary>
public sealed class TelegramNotifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelegramNotifier> _logger;

    public TelegramNotifier(IHttpClientFactory httpClientFactory, ILogger<TelegramNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<TelegramSendResult> SendAsync(string botToken, string chatId, string message, CancellationToken cancellationToken = default)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        return SendAsync(httpClient, botToken, chatId, message, _logger, cancellationToken);
    }

    internal static async Task<TelegramSendResult> SendAsync(
        HttpClient httpClient,
        string botToken,
        string chatId,
        string message,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
        {
            return TelegramSendResult.Fail("Token o Chat ID mancanti.");
        }

        try
        {
            var url = $"https://api.telegram.org/bot{botToken}/sendMessage";
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["chat_id"] = chatId,
                ["text"] = message,
            });
            var response = await httpClient.PostAsync(url, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return TelegramSendResult.Ok();
            }

            logger.LogWarning("Telegram API returned {StatusCode} sending a notification: {Body}", response.StatusCode, body);
            return TelegramSendResult.Fail($"Telegram ha risposto {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Failed to send Telegram notification");
            return TelegramSendResult.Fail($"Errore di rete: {ex.Message}");
        }
    }
}
