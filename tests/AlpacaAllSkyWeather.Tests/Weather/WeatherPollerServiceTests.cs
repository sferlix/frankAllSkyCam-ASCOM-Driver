using System.Net;
using AlpacaAllSkyWeather.Weather;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlpacaAllSkyWeather.Tests.Weather;

public class WeatherPollerServiceTests
{
    private const string SampleJson = """
    {
      "CloudCover": 21.4, "DewPoint": 14.57, "Humidity": 54, "Pressure": 916.5,
      "RainRate": 0, "SkyQuality": 0, "SkyBrightness": 70950, "Temperature": 24.5,
      "WindDirection": 184, "WindSpeed": 1.8, "WindGust": 1.9, "StarCount": 0
    }
    """;

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string? _body;

        public StubHandler(HttpStatusCode statusCode, string? body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode);
            if (_body is not null)
            {
                response.Content = new StringContent(_body);
            }
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task PollOnceAsync_updates_state_on_success()
    {
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.OK, SampleJson));
        var state = new WeatherState();

        await WeatherPollerService.PollOnceAsync(
            httpClient, state, "http://example.invalid/weather.json",
            NullLogger.Instance, CancellationToken.None);

        var snapshot = state.TryGetLatest();
        Assert.NotNull(snapshot);
        Assert.Equal(21.4, snapshot!.CloudCover);
    }

    [Fact]
    public async Task PollOnceAsync_leaves_state_unchanged_on_http_error()
    {
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.InternalServerError, null));
        var state = new WeatherState();

        await WeatherPollerService.PollOnceAsync(
            httpClient, state, "http://example.invalid/weather.json",
            NullLogger.Instance, CancellationToken.None);

        Assert.Null(state.TryGetLatest());
    }

    [Fact]
    public async Task PollOnceAsync_leaves_state_unchanged_on_malformed_json()
    {
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.OK, "{ not valid"));
        var state = new WeatherState();

        await WeatherPollerService.PollOnceAsync(
            httpClient, state, "http://example.invalid/weather.json",
            NullLogger.Instance, CancellationToken.None);

        Assert.Null(state.TryGetLatest());
    }
}
