using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.Tests.Alpaca;

public class ObservingConditionsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ObservingConditionsEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.RemoveAll<IHostedService>()));
    }

    private sealed record DoubleBody(double Value, uint ClientTransactionID, uint ServerTransactionID, int ErrorNumber, string ErrorMessage);
    private sealed record StringBody(string Value, uint ClientTransactionID, uint ServerTransactionID, int ErrorNumber, string ErrorMessage);

    private void SeedState(double cloudCover = 21.4)
    {
        var state = _factory.Services.GetRequiredService<WeatherState>();
        state.Update(new WeatherJsonDto { CloudCover = cloudCover, WindSpeed = 1.8 }, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task CloudCover_returns_the_seeded_value()
    {
        SeedState(cloudCover: 33.3);
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<DoubleBody>("/api/v1/observingconditions/0/cloudcover");

        Assert.NotNull(body);
        Assert.Equal(33.3, body!.Value);
        Assert.Equal(0, body.ErrorNumber);
    }

    [Fact]
    public async Task CloudCover_returns_ValueNotSet_error_before_any_poll()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<DoubleBody>("/api/v1/observingconditions/0/cloudcover");

        Assert.NotNull(body);
        Assert.Equal(0x402, body!.ErrorNumber); // ValueNotSet
    }

    [Fact]
    public async Task SkyTemperature_returns_NotImplemented_error()
    {
        SeedState();
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<DoubleBody>("/api/v1/observingconditions/0/skytemperature");

        Assert.NotNull(body);
        Assert.Equal(0x400, body!.ErrorNumber); // NotImplemented
    }

    [Fact]
    public async Task AveragePeriod_put_zero_then_get_round_trips()
    {
        var client = _factory.CreateClient();

        var putResponse = await client.PutAsync(
            "/api/v1/observingconditions/0/averageperiod",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["AveragePeriod"] = "0" }));
        putResponse.EnsureSuccessStatusCode();

        var body = await client.GetFromJsonAsync<DoubleBody>("/api/v1/observingconditions/0/averageperiod");
        Assert.NotNull(body);
        Assert.Equal(0, body!.Value);
    }

    [Fact]
    public async Task TimeSinceLastUpdate_reports_a_small_positive_age_just_after_a_poll()
    {
        SeedState();
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<DoubleBody>(
            "/api/v1/observingconditions/0/timesincelastupdate?SensorName=CloudCover");

        Assert.NotNull(body);
        Assert.InRange(body!.Value, 0, 5);
    }

    [Fact]
    public async Task SensorDescription_returns_text_for_a_known_sensor()
    {
        SeedState();
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<StringBody>(
            "/api/v1/observingconditions/0/sensordescription?SensorName=CloudCover");

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Value));
    }

    [Fact]
    public async Task Refresh_succeeds_with_no_error()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsync(
            "/api/v1/observingconditions/0/refresh",
            new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task TimeSinceLastUpdate_skytemperature_returns_NotImplemented_error()
    {
        SeedState();
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<DoubleBody>(
            "/api/v1/observingconditions/0/timesincelastupdate?SensorName=SkyTemperature");

        Assert.NotNull(body);
        Assert.Equal(0x400, body!.ErrorNumber); // NotImplemented
    }
}
