using System.Net;
using AlpacaAllSkyWeather.Weather;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlpacaAllSkyWeather.Tests.Weather;

public class TelegramCommandListenerTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _responseBody;
        public HttpRequestMessage? LastRequest { get; private set; }

        public StubHandler(string responseBody) => _responseBody = responseBody;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(_responseBody) });
        }
    }

    private sealed class RespondingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly byte[] _body;
        private readonly Exception? _throw;

        public RespondingHandler(HttpStatusCode statusCode, byte[]? body = null) { _statusCode = statusCode; _body = body ?? Array.Empty<byte>(); }
        public RespondingHandler(Exception toThrow) { _statusCode = HttpStatusCode.OK; _body = Array.Empty<byte>(); _throw = toThrow; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_throw is not null)
            {
                throw _throw;
            }
            return Task.FromResult(new HttpResponseMessage(_statusCode) { Content = new ByteArrayContent(_body) });
        }
    }

    private const string ExpectedChatId = "7515059983";

    private static string UpdatesJson(string text, string chatId = ExpectedChatId, long updateId = 100) =>
        "{\"ok\":true,\"result\":[{\"update_id\":" + updateId +
        ",\"message\":{\"message_id\":1,\"chat\":{\"id\":" + chatId + "},\"text\":\"" + text + "\"}}]}";

    [Theory]
    [InlineData("now", true)]
    [InlineData("/now", true)]
    [InlineData("NOW", true)]
    [InlineData("status", true)]
    [InlineData("/status", true)]
    [InlineData("ciao", false)]
    [InlineData("", false)]
    public void IsRecognizedCommand_matches_now_and_status_case_insensitively(string text, bool expected)
    {
        Assert.Equal(expected, TelegramCommandListener.IsRecognizedCommand(text));
    }

    [Fact]
    public async Task PollOnceAsync_replies_when_the_command_comes_from_the_expected_chat()
    {
        var handler = new StubHandler(UpdatesJson("now"));
        using var httpClient = new HttpClient(handler);
        string? repliedChatId = null;

        var newOffset = await TelegramCommandListener.PollOnceAsync(
            httpClient, "123:ABC", ExpectedChatId, offset: 1,
            (chatId, ct) => { repliedChatId = chatId; return Task.FromResult(TelegramSendResult.Ok()); },
            NullLogger.Instance, CancellationToken.None);

        Assert.Equal(ExpectedChatId, repliedChatId);
        Assert.Equal(101, newOffset); // update_id (100) + 1
    }

    [Fact]
    public async Task PollOnceAsync_ignores_commands_from_a_different_chat()
    {
        var handler = new StubHandler(UpdatesJson("now", chatId: "999999"));
        using var httpClient = new HttpClient(handler);
        var replied = false;

        await TelegramCommandListener.PollOnceAsync(
            httpClient, "123:ABC", ExpectedChatId, offset: 1,
            (_, _) => { replied = true; return Task.FromResult(TelegramSendResult.Ok()); },
            NullLogger.Instance, CancellationToken.None);

        Assert.False(replied);
    }

    [Fact]
    public async Task PollOnceAsync_ignores_unrecognized_text()
    {
        var handler = new StubHandler(UpdatesJson("ciao a tutti"));
        using var httpClient = new HttpClient(handler);
        var replied = false;

        await TelegramCommandListener.PollOnceAsync(
            httpClient, "123:ABC", ExpectedChatId, offset: 1,
            (_, _) => { replied = true; return Task.FromResult(TelegramSendResult.Ok()); },
            NullLogger.Instance, CancellationToken.None);

        Assert.False(replied);
    }

    [Fact]
    public async Task PollOnceAsync_returns_the_same_offset_when_there_are_no_updates()
    {
        var handler = new StubHandler("""{"ok":true,"result":[]}""");
        using var httpClient = new HttpClient(handler);

        var newOffset = await TelegramCommandListener.PollOnceAsync(
            httpClient, "123:ABC", ExpectedChatId, offset: 42,
            (_, _) => Task.FromResult(TelegramSendResult.Ok()),
            NullLogger.Instance, CancellationToken.None);

        Assert.Equal(42, newOffset);
    }

    [Fact]
    public async Task DownloadImageAsync_returns_null_without_a_request_when_url_is_blank()
    {
        var handler = new RespondingHandler(HttpStatusCode.OK);
        using var httpClient = new HttpClient(handler);

        var result = await TelegramCommandListener.DownloadImageAsync(httpClient, "", NullLogger.Instance, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DownloadImageAsync_returns_the_bytes_on_success()
    {
        var bytes = new byte[] { 1, 2, 3, 4 };
        var handler = new RespondingHandler(HttpStatusCode.OK, bytes);
        using var httpClient = new HttpClient(handler);

        var result = await TelegramCommandListener.DownloadImageAsync(
            httpClient, "https://example.com/allsky.jpg", NullLogger.Instance, CancellationToken.None);

        Assert.Equal(bytes, result);
    }

    [Fact]
    public async Task DownloadImageAsync_returns_null_on_http_error()
    {
        var handler = new RespondingHandler(HttpStatusCode.NotFound);
        using var httpClient = new HttpClient(handler);

        var result = await TelegramCommandListener.DownloadImageAsync(
            httpClient, "https://example.com/allsky.jpg", NullLogger.Instance, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task DownloadImageAsync_returns_null_on_network_exception()
    {
        var handler = new RespondingHandler(new HttpRequestException("connessione rifiutata"));
        using var httpClient = new HttpClient(handler);

        var result = await TelegramCommandListener.DownloadImageAsync(
            httpClient, "https://example.com/allsky.jpg", NullLogger.Instance, CancellationToken.None);

        Assert.Null(result);
    }
}
