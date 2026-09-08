using ASCOM.Alpaca.Discovery;

namespace AlpacaAllSkyWeather.Alpaca;

public static class ManagementEndpoints
{
    // Fixed so Alpaca clients that cache configured-device identity see a stable value across restarts.
    private const string DeviceUniqueId = "a2a99117-fe7e-4621-a88a-72e787ffee10";

    public static void MapManagementEndpoints(this WebApplication app)
    {
        app.MapGet("/management/apiversions", async (HttpRequest r) =>
        {
            var clientId = await AlpacaTransaction.GetClientTransactionIdAsync(r);
            var serverId = AlpacaTransaction.NextServerTransactionId();
            return Results.Ok(new ASCOM.Common.Alpaca.IntArray1DResponse(clientId, serverId, new[] { 1 }));
        });

        app.MapGet("/management/v1/description", async (HttpRequest r) =>
        {
            var clientId = await AlpacaTransaction.GetClientTransactionIdAsync(r);
            var serverId = AlpacaTransaction.NextServerTransactionId();
            var description = new AlpacaDeviceDescription("AlpacaAllSkyWeather", "sferlazza", "1.0", "N/A");
            return Results.Ok(new ManagementDescriptionResponse(description, clientId, serverId));
        });

        app.MapGet("/management/v1/configureddevices", async (HttpRequest r, ObservingConditionsDeviceName deviceName) =>
        {
            var clientId = await AlpacaTransaction.GetClientTransactionIdAsync(r);
            var serverId = AlpacaTransaction.NextServerTransactionId();
            var devices = new List<AlpacaConfiguredDevice>
            {
                new(deviceName.Value, ASCOM.Common.DeviceTypes.ObservingConditions.ToString(), 0, DeviceUniqueId),
            };
            return Results.Ok(new ASCOM.Alpaca.Discovery.AlpacaConfiguredDevicesResponse(clientId, serverId, devices));
        });
    }
}

public sealed record ManagementDescriptionResponse(AlpacaDeviceDescription Value, uint ClientTransactionID, uint ServerTransactionID);
