namespace AlpacaAllSkyWeather.Alpaca;

public static class ObservingConditionsEndpoints
{
    private const string BaseRoute = "/api/v1/observingconditions/0";

    public static void MapObservingConditionsEndpoints(this WebApplication app)
    {
        MapDouble(app, "cloudcover", d => d.CloudCover);
        MapDouble(app, "dewpoint", d => d.DewPoint);
        MapDouble(app, "humidity", d => d.Humidity);
        MapDouble(app, "pressure", d => d.Pressure);
        MapDouble(app, "rainrate", d => d.RainRate);
        MapDouble(app, "skybrightness", d => d.SkyBrightness);
        MapDouble(app, "skyquality", d => d.SkyQuality);
        MapDouble(app, "skytemperature", d => d.SkyTemperature);
        MapDouble(app, "starfwhm", d => d.StarFWHM);
        MapDouble(app, "temperature", d => d.Temperature);
        MapDouble(app, "winddirection", d => d.WindDirection);
        MapDouble(app, "windgust", d => d.WindGust);
        MapDouble(app, "windspeed", d => d.WindSpeed);

        app.MapGet($"{BaseRoute}/averageperiod", (HttpRequest r, ObservingConditionsDevice d) =>
            AlpacaEndpointHelpers.HandleDoubleAsync(r, () => d.AveragePeriod));

        app.MapPut($"{BaseRoute}/averageperiod", async (HttpRequest r, ObservingConditionsDevice d) =>
        {
            var form = await r.ReadFormAsync();
            var raw = form["AveragePeriod"].ToString();
            return await AlpacaEndpointHelpers.HandleMethodAsync(r, () =>
            {
                if (!double.TryParse(raw, out var value))
                {
                    throw new AlpacaDeviceException(ASCOM.Common.Alpaca.AlpacaErrors.InvalidValue, $"'{raw}' is not a valid AveragePeriod.");
                }
                d.AveragePeriod = value;
            });
        });

        app.MapGet($"{BaseRoute}/timesincelastupdate", (HttpRequest r, ObservingConditionsDevice d) =>
            AlpacaEndpointHelpers.HandleDoubleAsync(r, () => d.TimeSinceLastUpdate(r.Query["SensorName"].ToString())));

        app.MapGet($"{BaseRoute}/sensordescription", (HttpRequest r, ObservingConditionsDevice d) =>
            AlpacaEndpointHelpers.HandleStringAsync(r, () => d.SensorDescription(r.Query["SensorName"].ToString())));
    }

    private static void MapDouble(WebApplication app, string route, Func<ObservingConditionsDevice, double> selector)
    {
        app.MapGet($"{BaseRoute}/{route}", (HttpRequest r, ObservingConditionsDevice d) =>
            AlpacaEndpointHelpers.HandleDoubleAsync(r, () => selector(d)));
    }
}
