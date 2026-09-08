using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.Tests.Weather;

public class WeatherStateTests
{
    private static WeatherJsonDto SampleDto() => new()
    {
        CloudCover = 21.4,
        DewPoint = 14.57,
        Humidity = 54,
        Pressure = 916.5,
        RainRate = 0,
        SkyQuality = 0,
        SkyBrightness = 70950,
        Temperature = 24.5,
        WindDirection = 184,
        WindSpeed = 1.8,
        WindGust = 1.9,
        StarCount = 0,
        NightStart = new DateTimeOffset(2026, 9, 8, 19, 28, 48, TimeSpan.Zero),
        NightEnd = new DateTimeOffset(2026, 9, 9, 3, 12, 24, TimeSpan.Zero),
    };

    [Fact]
    public void TryGetLatest_returns_null_before_any_update()
    {
        var state = new WeatherState();

        Assert.Null(state.TryGetLatest());
    }

    [Fact]
    public void TryGetLatest_returns_mapped_snapshot_after_update()
    {
        var state = new WeatherState();
        var polledAt = new DateTimeOffset(2026, 9, 8, 9, 37, 3, TimeSpan.Zero);

        state.Update(SampleDto(), polledAt);
        var snapshot = state.TryGetLatest();

        Assert.NotNull(snapshot);
        Assert.Equal(21.4, snapshot!.CloudCover);
        Assert.Equal(1.8, snapshot.WindSpeed);
        Assert.Equal(new DateTimeOffset(2026, 9, 8, 19, 28, 48, TimeSpan.Zero), snapshot.NightStart);
        Assert.Equal(new DateTimeOffset(2026, 9, 9, 3, 12, 24, TimeSpan.Zero), snapshot.NightEnd);
        Assert.Equal(polledAt, snapshot.PolledAtUtc);
    }

    [Fact]
    public void Update_overwrites_previous_snapshot()
    {
        var state = new WeatherState();
        state.Update(SampleDto(), DateTimeOffset.UtcNow.AddMinutes(-5));

        var newer = SampleDto() with { CloudCover = 80.0 };
        var polledAt = DateTimeOffset.UtcNow;
        state.Update(newer, polledAt);

        var snapshot = state.TryGetLatest();
        Assert.Equal(80.0, snapshot!.CloudCover);
        Assert.Equal(polledAt, snapshot.PolledAtUtc);
    }
}
