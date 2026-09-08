namespace AlpacaAllSkyWeather.Weather;

public sealed class WeatherPollerOptions
{
    public const string SectionName = "AllSkyWeather";

    public string SourceUrl { get; set; } = "https://www.meteobrallo.com/webcam/allsky/weather.json";

    public int PollIntervalSeconds { get; set; } = 60;

    public int HttpPort { get; set; } = 51111;

    public string DeviceName { get; set; } = "AllSky Weather";
}
