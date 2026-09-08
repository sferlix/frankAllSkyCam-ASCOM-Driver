using ASCOM.Common.Alpaca;

namespace AlpacaAllSkyWeather.Alpaca;

public static class AlpacaCommonEndpoints
{
    private const string BaseRoute = "/api/v1/observingconditions/0";

    public static void MapAlpacaCommonEndpoints(this WebApplication app)
    {
        app.MapGet($"{BaseRoute}/connected", (HttpRequest r, ObservingConditionsDevice device) =>
            AlpacaEndpointHelpers.HandleBoolAsync(r, () => device.Connected));

        app.MapPut($"{BaseRoute}/connected", async (HttpRequest r, ObservingConditionsDevice device) =>
        {
            var form = await r.ReadFormAsync();
            var raw = form["Connected"].ToString();
            return await AlpacaEndpointHelpers.HandleMethodAsync(r, () =>
            {
                if (!bool.TryParse(raw, out var value))
                {
                    throw new AlpacaDeviceException(AlpacaErrors.InvalidValue, $"'{raw}' is not a valid Connected value.");
                }
                device.Connected = value;
            });
        });

        app.MapGet($"{BaseRoute}/description", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleStringAsync(r, () =>
                "AllSky Weather - bridges frankAllSkyCam's weather.json to ASCOM Alpaca ObservingConditions"));

        app.MapGet($"{BaseRoute}/driverinfo", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleStringAsync(r, () => "AlpacaAllSkyWeather"));

        app.MapGet($"{BaseRoute}/driverversion", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleStringAsync(r, () => "1.0"));

        // Declares 1, not 2: this driver implements the IObservingConditions (v1) surface only.
        // V2 (IAscomDeviceV2) adds Connect/Disconnect/Connecting/DeviceState for async connection
        // handling, which is out of scope for a device that is always synchronously "connected".
        app.MapGet($"{BaseRoute}/interfaceversion", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleIntAsync(r, () => 1));

        app.MapGet($"{BaseRoute}/name", (HttpRequest r, ObservingConditionsDeviceName deviceName) =>
            AlpacaEndpointHelpers.HandleStringAsync(r, () => deviceName.Value));

        app.MapGet($"{BaseRoute}/supportedactions", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleStringListAsync(r, () => new List<string> { "starcount" }));

        app.MapPut($"{BaseRoute}/action", async (HttpRequest r, ObservingConditionsDevice device) =>
        {
            var form = await r.ReadFormAsync();
            var action = form["Action"].ToString();

            return await AlpacaEndpointHelpers.HandleStringAsync(r, () => action.Equals("starcount", StringComparison.OrdinalIgnoreCase)
                ? device.GetStarCount()
                : throw new AlpacaDeviceException(AlpacaErrors.ActionNotImplementedException, $"Action '{action}' is not supported."));
        });
    }
}

/// <summary>Thin wrapper so the configured device name can be injected without exposing all of WeatherPollerOptions to the endpoint.</summary>
public sealed record ObservingConditionsDeviceName(string Value);
