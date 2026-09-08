using System.Globalization;
using ASCOM.Common.Alpaca;
using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.Alpaca;

public sealed class ObservingConditionsDevice
{
    private static readonly HashSet<string> KnownSensors = new(StringComparer.OrdinalIgnoreCase)
    {
        "CloudCover", "DewPoint", "Humidity", "Pressure", "RainRate", "SkyBrightness",
        "SkyQuality", "Temperature", "WindDirection", "WindGust", "WindSpeed",
    };

    private static readonly HashSet<string> UnsupportedSensors = new(StringComparer.OrdinalIgnoreCase)
    {
        "SkyTemperature", "StarFWHM",
    };

    private readonly WeatherState _state;
    private double _averagePeriod;

    public ObservingConditionsDevice(WeatherState state)
    {
        _state = state;
    }

    public double AveragePeriod
    {
        get => _averagePeriod;
        set
        {
            if (value != 0)
            {
                throw new AlpacaDeviceException(
                    AlpacaErrors.InvalidValue,
                    "AveragePeriod must be 0: this driver reports only the latest instantaneous reading.");
            }

            _averagePeriod = value;
        }
    }

    public double CloudCover => GetValue(s => s.CloudCover);
    public double DewPoint => GetValue(s => s.DewPoint);
    public double Humidity => GetValue(s => s.Humidity);
    public double Pressure => GetValue(s => s.Pressure);
    public double RainRate => GetValue(s => s.RainRate);
    public double SkyBrightness => GetValue(s => s.SkyBrightness);
    public double SkyQuality => GetValue(s => s.SkyQuality);
    public double Temperature => GetValue(s => s.Temperature);
    public double WindDirection => GetValue(s => s.WindDirection);
    public double WindGust => GetValue(s => s.WindGust);
    public double WindSpeed => GetValue(s => s.WindSpeed);

    public double SkyTemperature => throw NotImplemented("SkyTemperature");
    public double StarFWHM => throw NotImplemented("StarFWHM");

    public double TimeSinceLastUpdate(string sensorName)
    {
        var snapshot = GetSnapshotOrThrow();
        return (DateTimeOffset.UtcNow - snapshot.PolledAtUtc).TotalSeconds;
    }

    public string SensorDescription(string sensorName)
    {
        if (UnsupportedSensors.Contains(sensorName))
        {
            throw NotImplemented(sensorName);
        }

        if (!KnownSensors.Contains(sensorName))
        {
            throw new AlpacaDeviceException(AlpacaErrors.InvalidValue, $"Unknown sensor name '{sensorName}'.");
        }

        return $"{sensorName} reported by frankAllSkyCam via weather.json";
    }

    public string GetStarCount()
    {
        var snapshot = GetSnapshotOrThrow();
        return snapshot.StarCount.ToString(CultureInfo.InvariantCulture);
    }

    public void Refresh()
    {
        // No-op: WeatherPollerService already refreshes the shared state on a fixed
        // interval, so there is no separate "on demand" fetch to trigger here.
    }

    private double GetValue(Func<WeatherSnapshot, double> selector)
        => selector(GetSnapshotOrThrow());

    private WeatherSnapshot GetSnapshotOrThrow()
        => _state.TryGetLatest()
           ?? throw new AlpacaDeviceException(
               AlpacaErrors.ValueNotSet,
               "No successful poll of the weather source has completed yet.");

    private static AlpacaDeviceException NotImplemented(string sensorName)
        => new(AlpacaErrors.NotImplemented, $"{sensorName} sensor is not available on this AllSkyCam.");
}
