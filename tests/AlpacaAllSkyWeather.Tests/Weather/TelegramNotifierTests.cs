using System.Net;
using AlpacaAllSkyWeather.Weather;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlpacaAllSkyWeather.Tests.Weather;

public class TelegramNotifierTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastRequestBody { get; private set; }

        public RecordingHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_statusCode);
        }
    }

    [Fact]
    public async Task SendAsync_posts_to_the_telegram_api_with_chat_id_and_text()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);

        var result = await TelegramNotifier.SendAsync(
            httpClient, "123:ABC", "999", "ciao mondo", NullLogger.Instance, CancellationToken.None);

        Assert.True(result);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://api.telegram.org/bot123:ABC/sendMessage", handler.LastRequest!.RequestUri!.GetLeftPart(UriPartial.Path));
        Assert.Contains("chat_id=999", handler.LastRequestBody);
        Assert.Contains("ciao", handler.LastRequestBody);
    }

    [Fact]
    public async Task SendAsync_returns_false_on_http_error_without_throwing()
    {
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized);
        using var httpClient = new HttpClient(handler);

        var result = await TelegramNotifier.SendAsync(
            httpClient, "bad-token", "999", "ciao", NullLogger.Instance, CancellationToken.None);

        Assert.False(result);
    }

    [Theory]
    [InlineData("", "999")]
    [InlineData("123:ABC", "")]
    public async Task SendAsync_returns_false_without_calling_out_when_token_or_chat_id_is_missing(string token, string chatId)
    {
        var handler = new RecordingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);

        var result = await TelegramNotifier.SendAsync(
            httpClient, token, chatId, "ciao", NullLogger.Instance, CancellationToken.None);

        Assert.False(result);
        Assert.Null(handler.LastRequest);
    }
}
