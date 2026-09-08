using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.Tests.Alpaca;

public class SafetyMonitorEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SafetyMonitorEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.RemoveAll<IHostedService>()));
    }

    private sealed record BoolBody(bool Value, uint ClientTransactionID, uint ServerTransactionID, int ErrorNumber, string ErrorMessage);
    private sealed record StringBody(string Value, uint ClientTransactionID, uint ServerTransactionID, int ErrorNumber, string ErrorMessage);

    private void SeedSafeState()
    {
        var state = _factory.Services.GetRequiredService<WeatherState>();
        state.Update(new WeatherJsonDto { CloudCover = 10, RainRate = 0, WindGust = 2, SkyBrightness = 5 }, DateTimeOffset.UtcNow);
    }

    private void SeedUnsafeState()
    {
        var state = _factory.Services.GetRequiredService<WeatherState>();
        state.Update(new WeatherJsonDto { CloudCover = 10, RainRate = 5, WindGust = 2, SkyBrightness = 5 }, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task IsSafe_returns_false_before_any_poll()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<BoolBody>("/api/v1/safetymonitor/0/issafe");

        Assert.NotNull(body);
        Assert.False(body!.Value);
        Assert.Equal(0, body.ErrorNumber);
    }

    [Fact]
    public async Task IsSafe_returns_true_when_conditions_are_within_thresholds()
    {
        SeedSafeState();
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<BoolBody>("/api/v1/safetymonitor/0/issafe");

        Assert.NotNull(body);
        Assert.True(body!.Value);
    }

    [Fact]
    public async Task IsSafe_returns_false_when_it_is_raining()
    {
        SeedUnsafeState();
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<BoolBody>("/api/v1/safetymonitor/0/issafe");

        Assert.NotNull(body);
        Assert.False(body!.Value);
    }

    [Fact]
    public async Task Connected_always_returns_true_by_default()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<BoolBody>("/api/v1/safetymonitor/0/connected");

        Assert.NotNull(body);
        Assert.True(body!.Value);
        Assert.Equal(0, body.ErrorNumber);
    }

    [Fact]
    public async Task Description_returns_driver_description()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<StringBody>("/api/v1/safetymonitor/0/description");

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Value));
    }
}
