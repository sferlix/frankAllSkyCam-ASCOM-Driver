using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlpacaAllSkyWeather.Weather;

public sealed class WeatherPollerService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WeatherState _state;
    private readonly WeatherPollerOptions _options;
    private readonly ILogger<WeatherPollerService> _logger;

    public WeatherPollerService(
        IHttpClientFactory httpClientFactory,
        WeatherState state,
        IOptions<WeatherPollerOptions> options,
        ILogger<WeatherPollerService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _state = state;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var httpClient = _httpClientFactory.CreateClient();

        while (!stoppingToken.IsCancellationRequested)
        {
            await PollOnceAsync(httpClient, _state, _options.SourceUrl, _logger, stoppingToken);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    internal static async Task PollOnceAsync(
        HttpClient httpClient,
        WeatherState state,
        string sourceUrl,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetAsync(sourceUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var dto = WeatherJsonDto.Parse(json);
            state.Update(dto, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Failed to poll weather source {Url}", sourceUrl);
        }
    }
}
