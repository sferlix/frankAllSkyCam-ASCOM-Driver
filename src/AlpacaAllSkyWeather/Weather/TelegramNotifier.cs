using System.Net;
using System.Net.Sockets;
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
/// yet", not an error: it returns a failure result without making a request.
///
/// Owns a single long-lived <see cref="HttpClient"/> directly instead of going through
/// <c>IHttpClientFactory</c>: on at least one real machine, a factory-created client with this
/// exact IPv4-only <c>ConnectCallback</c> failed every request with an immediate
/// <see cref="TaskCanceledException"/>, while a plain <c>new HttpClient(handler)</c> using the
/// identical handler succeeded consistently in under 200ms. Root cause not fully identified
/// (suspected interaction between the factory's handler lifetime/pooling and a custom
/// ConnectCallback), but owning the client directly reproduced the working behavior.</summary>
public sealed class TelegramNotifier : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TelegramNotifier> _logger;

    public TelegramNotifier(ILogger<TelegramNotifier> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient(CreateIPv4OnlyHandler());
    }

    public Task<TelegramSendResult> SendAsync(string botToken, string chatId, string message, CancellationToken cancellationToken = default)
        => SendAsync(_httpClient, botToken, chatId, message, _logger, cancellationToken);

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
            return TelegramSendResult.Fail("Missing Token or Chat ID.");
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
            return TelegramSendResult.Fail($"Telegram responded {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Failed to send Telegram notification");
            return TelegramSendResult.Fail($"Network error: {ex.Message}");
        }
    }

    public Task<TelegramSendResult> SendPhotoAsync(string botToken, string chatId, byte[] photoPng, string? caption, CancellationToken cancellationToken = default)
        => SendPhotoAsync(_httpClient, botToken, chatId, photoPng, caption, _logger, cancellationToken);

    internal static async Task<TelegramSendResult> SendPhotoAsync(
        HttpClient httpClient,
        string botToken,
        string chatId,
        byte[] photoPng,
        string? caption,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(chatId))
        {
            return TelegramSendResult.Fail("Missing Token or Chat ID.");
        }

        try
        {
            var url = $"https://api.telegram.org/bot{botToken}/sendPhoto";
            using var content = new MultipartFormDataContent
            {
                { new StringContent(chatId), "chat_id" },
            };
            if (!string.IsNullOrWhiteSpace(caption))
            {
                content.Add(new StringContent(caption), "caption");
            }
            var photoContent = new ByteArrayContent(photoPng);
            photoContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            content.Add(photoContent, "photo", "stato.png");

            var response = await httpClient.PostAsync(url, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return TelegramSendResult.Ok();
            }

            logger.LogWarning("Telegram API returned {StatusCode} sending a photo: {Body}", response.StatusCode, body);
            return TelegramSendResult.Fail($"Telegram responded {(int)response.StatusCode} {response.StatusCode}: {body}");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Failed to send Telegram photo");
            return TelegramSendResult.Fail($"Network error: {ex.Message}");
        }
    }

    /// <summary>Forces IPv4: .NET's Happy Eyeballs can stall for many seconds on networks where
    /// IPv6 has no working route (common on home routers) before falling back to IPv4, even
    /// though tools like curl fail over almost instantly. Internal (not private) so
    /// TelegramCommandListener's own long-lived HttpClient can reuse it.</summary>
    internal static SocketsHttpHandler CreateIPv4OnlyHandler() => new()
    {
        ConnectCallback = async (context, cancellationToken) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, cancellationToken);
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(addresses[0], context.DnsEndPoint.Port, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };

    public void Dispose() => _httpClient.Dispose();
}
