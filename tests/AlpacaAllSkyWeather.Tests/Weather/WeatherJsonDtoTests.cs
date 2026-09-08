using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.Tests.Weather;

public class WeatherJsonDtoTests
{
    // Captured from https://www.meteobrallo.com/webcam/allsky/weather.json on 2026-09-08.
    private const string SampleJson = """
    {
      "timestamp": "2026-09-08T09:37:03Z",
      "timestamp_local": "2026-09-08T11:37:03+02:00",
      "NightStart": "2026-09-08T19:28:48Z",
      "NightEnd": "2026-09-09T03:12:24Z",
      "CloudCover": 21.4,
      "DewPoint": 14.57,
      "Humidity": 54,
      "Pressure": 916.5,
      "SeaLevelPressure": 1023.27,
      "RainRate": 0,
      "SkyQuality": 0,
      "SkyBrightness": 70950,
      "Temperature": 24.5,
      "UVIndex": 5,
      "WindDirection": 184,
      "WindSpeed": 1.8,
      "WindGust": 1.9,
      "StarCount": 0
    }
    """;

    [Fact]
    public void Parse_reads_all_mapped_fields_from_real_sample()
    {
        var dto = WeatherJsonDto.Parse(SampleJson);

        Assert.Equal(21.4, dto.CloudCover);
        Assert.Equal(14.57, dto.DewPoint);
        Assert.Equal(54, dto.Humidity);
        Assert.Equal(916.5, dto.Pressure);
        Assert.Equal(0, dto.RainRate);
        Assert.Equal(0, dto.SkyQuality);
        Assert.Equal(70950, dto.SkyBrightness);
        Assert.Equal(24.5, dto.Temperature);
        Assert.Equal(184, dto.WindDirection);
        Assert.Equal(1.8, dto.WindSpeed);
        Assert.Equal(1.9, dto.WindGust);
        Assert.Equal(0, dto.StarCount);
    }

    [Fact]
    public void Parse_ignores_unmapped_fields_without_throwing()
    {
        // timestamp/timestamp_local/NightStart/NightEnd/SeaLevelPressure/UVIndex
        // are present in the real payload but unused by this driver (see spec, YAGNI section).
        var dto = WeatherJsonDto.Parse(SampleJson);

        Assert.NotNull(dto);
    }

    [Fact]
    public void Parse_throws_JsonException_on_malformed_json()
    {
        Assert.Throws<System.Text.Json.JsonException>(() => WeatherJsonDto.Parse("{ not valid json"));
    }
}
