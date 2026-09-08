using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AlpacaAllSkyWeather.Tests.Alpaca;

public class ManagementEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ManagementEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.RemoveAll<IHostedService>()));
    }

    private sealed record ApiVersionsBody(int[] Value);
    private sealed record DescriptionBody(DescriptionValue Value);
    private sealed record DescriptionValue(string ServerName, string Manufacturer, string ManufacturerVersion, string Location);
    private sealed record ConfiguredDevice(string DeviceName, string DeviceType, int DeviceNumber, string UniqueID);
    private sealed record ConfiguredDevicesBody(List<ConfiguredDevice> Value);

    [Fact]
    public async Task ApiVersions_reports_version_1()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<ApiVersionsBody>("/management/apiversions");

        Assert.NotNull(body);
        Assert.Contains(1, body!.Value);
    }

    [Fact]
    public async Task Description_reports_server_name()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<DescriptionBody>("/management/v1/description");

        Assert.NotNull(body);
        Assert.Equal("AlpacaAllSkyWeather", body!.Value.ServerName);
    }

    [Fact]
    public async Task ConfiguredDevices_lists_the_ObservingConditions_and_SafetyMonitor_devices()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<ConfiguredDevicesBody>("/management/v1/configureddevices");

        Assert.NotNull(body);
        Assert.Equal(2, body!.Value.Count);

        var observingConditions = Assert.Single(body.Value, d => d.DeviceType == "ObservingConditions");
        Assert.Equal(0, observingConditions.DeviceNumber);

        var safetyMonitor = Assert.Single(body.Value, d => d.DeviceType == "SafetyMonitor");
        Assert.Equal(0, safetyMonitor.DeviceNumber);

        Assert.NotEqual(observingConditions.UniqueID, safetyMonitor.UniqueID);
    }
}
