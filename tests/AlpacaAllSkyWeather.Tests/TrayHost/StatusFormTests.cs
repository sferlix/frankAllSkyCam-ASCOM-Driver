using AlpacaAllSkyWeather.TrayHost;
using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.Tests.TrayHost;

public class StatusFormTests
{
    private static WeatherSnapshot Snapshot(double cloudCover, DateTimeOffset nightStart, DateTimeOffset nightEnd) => new(
        CloudCover: cloudCover, DewPoint: 0, Humidity: 0, Pressure: 0, RainRate: 0,
        SkyQuality: 0, SkyBrightness: 0, Temperature: 0, WindDirection: 0,
        WindSpeed: 0, WindGust: 0, StarCount: 0,
        NightStart: nightStart, NightEnd: nightEnd, PolledAtUtc: DateTimeOffset.UtcNow);

    [Theory]
    [InlineData(50)]
    [InlineData(80)]
    [InlineData(100)]
    public void CloudCoverIcon_shows_clouds_when_mostly_covered_regardless_of_day_or_night(double cloudCover)
    {
        var now = DateTimeOffset.UtcNow;
        var daytimeSnapshot = Snapshot(cloudCover, now.AddHours(2), now.AddHours(4));
        var nighttimeSnapshot = Snapshot(cloudCover, now.AddHours(-1), now.AddHours(1));

        Assert.Equal(WeatherIcons.Cloud, StatusForm.CloudCoverIcon(daytimeSnapshot));
        Assert.Equal(WeatherIcons.Cloud, StatusForm.CloudCoverIcon(nighttimeSnapshot));
    }

    [Fact]
    public void CloudCoverIcon_shows_the_sun_when_clear_during_the_day()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(20, now.AddHours(2), now.AddHours(4)); // night window is in the future

        Assert.Equal(WeatherIcons.Sun, StatusForm.CloudCoverIcon(snapshot));
    }

    [Fact]
    public void CloudCoverIcon_shows_stars_when_clear_at_night()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = Snapshot(20, now.AddHours(-1), now.AddHours(1)); // inside the night window

        Assert.Equal(WeatherIcons.FiveStar, StatusForm.CloudCoverIcon(snapshot));
    }
}
