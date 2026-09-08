using Microsoft.Extensions.Logging;

namespace AlpacaAllSkyWeather.Weather;

/// <summary>Sends alert messages via the Telegram Bot API (free, no account billing —
/// see <see cref="NotificationOptions"/>). Missing token/chat ID is treated as "not configured
/// yet", not an error: it returns false without making a request.</summary>
public sealed class TelegramNotifier
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelegramNotifier> _logger;

    public TelegramNotifier(IHttpClientFactory httpClientFactory, ILogger<TelegramNotifier> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<bool> SendAsync(string botToken, string chatId, string message, CancellationToken cancellationToken = default)
    {
        using var httpClient = _httpClientFactory.CreateClient();
        return SendAsync(httpClient, botToken, chatId, message, _logger, cancellationToken);
    }

    internal static async Task<bool> SendAsync(
        HttpClient httpClient,
        string botToken,
        string chatId,
        string message,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
        {
            return false;
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
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Telegram API returned {StatusCode} sending a notification", response.StatusCode);
            }
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Failed to send Telegram notification");
            return false;
        }
    }
}
