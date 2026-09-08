namespace AlpacaAllSkyWeather.Alpaca;

public static class SafetyMonitorEndpoints
{
    private const string BaseRoute = "/api/v1/safetymonitor/0";

    public static void MapSafetyMonitorEndpoints(this WebApplication app)
    {
        app.MapGet($"{BaseRoute}/connected", (HttpRequest r, SafetyMonitorDevice device) =>
            AlpacaEndpointHelpers.HandleBoolAsync(r, () => device.Connected));

        app.MapPut($"{BaseRoute}/connected", async (HttpRequest r, SafetyMonitorDevice device) =>
        {
            var form = await r.ReadFormAsync();
            var raw = form["Connected"].ToString();
            return await AlpacaEndpointHelpers.HandleMethodAsync(r, () =>
            {
                if (!bool.TryParse(raw, out var value))
                {
                    throw new AlpacaDeviceException(ASCOM.Common.Alpaca.AlpacaErrors.InvalidValue, $"'{raw}' is not a valid Connected value.");
                }
                device.Connected = value;
            });
        });

        app.MapGet($"{BaseRoute}/description", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleStringAsync(r, () =>
                "AllSky Safety Monitor - derives safe/unsafe from frankAllSkyCam weather thresholds"));

        app.MapGet($"{BaseRoute}/driverinfo", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleStringAsync(r, () => "AlpacaAllSkyWeather"));

        app.MapGet($"{BaseRoute}/driverversion", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleStringAsync(r, () => "1.0"));

        app.MapGet($"{BaseRoute}/interfaceversion", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleIntAsync(r, () => 1));

        app.MapGet($"{BaseRoute}/name", (HttpRequest r, SafetyMonitorDeviceName deviceName) =>
            AlpacaEndpointHelpers.HandleStringAsync(r, () => deviceName.Value));

        app.MapGet($"{BaseRoute}/supportedactions", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleStringListAsync(r, () => new List<string>()));

        app.MapPut($"{BaseRoute}/action", async (HttpRequest r) =>
        {
            var form = await r.ReadFormAsync();
            var action = form["Action"].ToString();
            return await AlpacaEndpointHelpers.HandleStringAsync(r, () =>
                throw new AlpacaDeviceException(ASCOM.Common.Alpaca.AlpacaErrors.ActionNotImplementedException, $"Action '{action}' is not supported."));
        });

        app.MapGet($"{BaseRoute}/issafe", (HttpRequest r, SafetyMonitorDevice device) =>
            AlpacaEndpointHelpers.HandleBoolAsync(r, () => device.IsSafe));
    }
}

public sealed record SafetyMonitorDeviceName(string Value);
