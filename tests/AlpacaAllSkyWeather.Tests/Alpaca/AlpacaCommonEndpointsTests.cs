using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.Tests.Alpaca;

public class AlpacaCommonEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AlpacaCommonEndpointsTests(WebApplicationFactory<Program> factory)
    {
        // The real app also starts WeatherPollerService, which would try to hit the
        // real internet during tests. Integration tests only need the HTTP surface,
        // so the hosted service is removed and the test seeds WeatherState directly.
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.RemoveAll<IHostedService>()));
    }

    private sealed record ConnectedBody(bool Value, uint ClientTransactionID, uint ServerTransactionID, int ErrorNumber, string ErrorMessage);
    private sealed record StringBody(string Value, uint ClientTransactionID, uint ServerTransactionID, int ErrorNumber, string ErrorMessage);
    private sealed record StringListBody(List<string> Value, uint ClientTransactionID, uint ServerTransactionID, int ErrorNumber, string ErrorMessage);

    [Fact]
    public async Task Connected_always_returns_true()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<ConnectedBody>(
            "/api/v1/observingconditions/0/connected?ClientID=1&ClientTransactionID=1");

        Assert.NotNull(body);
        Assert.True(body!.Value);
        Assert.Equal(0, body.ErrorNumber);
    }

    [Fact]
    public async Task Connected_json_uses_exact_PascalCase_property_names_required_by_Alpaca()
    {
        // Minimal API JSON defaults to camelCase; Alpaca requires exact PascalCase.
        // This guards Program.cs's ConfigureHttpJsonOptions override against regressions.
        var client = _factory.CreateClient();

        var raw = await client.GetStringAsync(
            "/api/v1/observingconditions/0/connected?ClientID=1&ClientTransactionID=1");

        Assert.Contains("\"ClientTransactionID\"", raw);
        Assert.Contains("\"ErrorNumber\"", raw);
        Assert.DoesNotContain("\"clientTransactionID\"", raw);
    }

    [Fact]
    public async Task Description_returns_driver_description()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<StringBody>(
            "/api/v1/observingconditions/0/description");

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Value));
    }

    [Fact]
    public async Task SupportedActions_lists_starcount()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<StringListBody>(
            "/api/v1/observingconditions/0/supportedactions");

        Assert.NotNull(body);
        Assert.Contains("starcount", body!.Value, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Action_starcount_returns_the_star_count_from_state()
    {
        var state = _factory.Services.GetRequiredService<WeatherState>();
        state.Update(new WeatherJsonDto { StarCount = 12 }, DateTimeOffset.UtcNow);
        var client = _factory.CreateClient();

        var response = await client.PutAsync(
            "/api/v1/observingconditions/0/action",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Action"] = "starcount",
                ["Parameters"] = "",
                ["ClientID"] = "1",
                ["ClientTransactionID"] = "1",
            }));

        var body = await response.Content.ReadFromJsonAsync<StringBody>();
        Assert.NotNull(body);
        Assert.Equal("12", body!.Value);
        Assert.Equal(0, body.ErrorNumber);
    }

    [Fact]
    public async Task Action_unknown_returns_ActionNotImplemented_error()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsync(
            "/api/v1/observingconditions/0/action",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Action"] = "notarealaction",
                ["Parameters"] = "",
            }));

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StringBody>();
        Assert.NotNull(body);
        Assert.Equal(0x40C, body!.ErrorNumber); // ActionNotImplementedException
    }
}
