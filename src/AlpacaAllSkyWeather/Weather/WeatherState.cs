namespace AlpacaAllSkyWeather.Weather;

public sealed record WeatherSnapshot(
    double CloudCover,
    double DewPoint,
    double Humidity,
    double Pressure,
    double RainRate,
    double SkyQuality,
    double SkyBrightness,
    double Temperature,
    double WindDirection,
    double WindSpeed,
    double WindGust,
    double StarCount,
    DateTimeOffset NightStart,
    DateTimeOffset NightEnd,
    DateTimeOffset PolledAtUtc);

public sealed class WeatherState
{
    private readonly object _lock = new();
    private WeatherSnapshot? _latest;

    public void Update(WeatherJsonDto dto, DateTimeOffset polledAtUtc)
    {
        var snapshot = new WeatherSnapshot(
            dto.CloudCover,
            dto.DewPoint,
            dto.Humidity,
            dto.Pressure,
            dto.RainRate,
            dto.SkyQuality,
            dto.SkyBrightness,
            dto.Temperature,
            dto.WindDirection,
            dto.WindSpeed,
            dto.WindGust,
            dto.StarCount,
            dto.NightStart,
            dto.NightEnd,
            polledAtUtc);

        lock (_lock)
        {
            _latest = snapshot;
        }
    }

    public WeatherSnapshot? TryGetLatest()
    {
        lock (_lock)
        {
            return _latest;
        }
    }
}
