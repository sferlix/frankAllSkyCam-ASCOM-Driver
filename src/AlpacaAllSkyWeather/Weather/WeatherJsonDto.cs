using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlpacaAllSkyWeather.Weather;

public sealed record WeatherJsonDto
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [JsonPropertyName("CloudCover")]
    public double CloudCover { get; init; }

    [JsonPropertyName("DewPoint")]
    public double DewPoint { get; init; }

    [JsonPropertyName("Humidity")]
    public double Humidity { get; init; }

    [JsonPropertyName("Pressure")]
    public double Pressure { get; init; }

    [JsonPropertyName("RainRate")]
    public double RainRate { get; init; }

    [JsonPropertyName("SkyQuality")]
    public double SkyQuality { get; init; }

    [JsonPropertyName("SkyBrightness")]
    public double SkyBrightness { get; init; }

    [JsonPropertyName("Temperature")]
    public double Temperature { get; init; }

    [JsonPropertyName("WindDirection")]
    public double WindDirection { get; init; }

    [JsonPropertyName("WindSpeed")]
    public double WindSpeed { get; init; }

    [JsonPropertyName("WindGust")]
    public double WindGust { get; init; }

    [JsonPropertyName("StarCount")]
    public double StarCount { get; init; }

    [JsonPropertyName("NightStart")]
    public DateTimeOffset NightStart { get; init; }

    [JsonPropertyName("NightEnd")]
    public DateTimeOffset NightEnd { get; init; }

    public static WeatherJsonDto Parse(string json)
        => JsonSerializer.Deserialize<WeatherJsonDto>(json, Options)
           ?? throw new JsonException("weather.json deserialized to null");
}
