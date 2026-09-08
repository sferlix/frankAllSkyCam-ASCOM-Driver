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

    private static (DateTimeOffset start, DateTimeOffset end) DaytimeWindow(DateTimeOffset now) => (now.AddHours(6), now.AddHours(10));

    private static (DateTimeOffset start, DateTimeOffset end) NighttimeWindow(DateTimeOffset now) => (now.AddHours(-1), now.AddHours(1));

    [Theory]
    [InlineData(70)]
    [InlineData(85)]
    [InlineData(100)]
    public void CloudCoverIcon_shows_clouds_when_overcast_regardless_of_day_or_night(double cloudCover)
    {
        var now = DateTimeOffset.UtcNow;
        var (dayStart, dayEnd) = DaytimeWindow(now);
        var (nightStart, nightEnd) = NighttimeWindow(now);

        Assert.Equal(WeatherIcons.Cloud, StatusForm.CloudCoverIcon(Snapshot(cloudCover, dayStart, dayEnd)));
        Assert.Equal(WeatherIcons.Cloud, StatusForm.CloudCoverIcon(Snapshot(cloudCover, nightStart, nightEnd)));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(50)]
    [InlineData(69)]
    public void CloudCoverIcon_shows_the_partly_cloudy_day_icon_when_partly_covered_during_the_day(double cloudCover)
    {
        var now = DateTimeOffset.UtcNow;
        var (dayStart, dayEnd) = DaytimeWindow(now);

        Assert.Equal(WeatherIcons.PartlyCloudyDay, StatusForm.CloudCoverIcon(Snapshot(cloudCover, dayStart, dayEnd)));
    }

    [Theory]
    [InlineData(30)]
    [InlineData(50)]
    [InlineData(69)]
    public void CloudCoverIcon_shows_the_partly_cloudy_night_icon_when_partly_covered_at_night(double cloudCover)
    {
        var now = DateTimeOffset.UtcNow;
        var (nightStart, nightEnd) = NighttimeWindow(now);

        Assert.Equal(WeatherIcons.PartlyCloudyNight, StatusForm.CloudCoverIcon(Snapshot(cloudCover, nightStart, nightEnd)));
    }

    [Fact]
    public void CloudCoverIcon_shows_the_sun_when_clear_during_the_day()
    {
        var now = DateTimeOffset.UtcNow;
        var (dayStart, dayEnd) = DaytimeWindow(now);

        Assert.Equal(WeatherIcons.Sun, StatusForm.CloudCoverIcon(Snapshot(20, dayStart, dayEnd)));
    }

    [Fact]
    public void CloudCoverIcon_shows_stars_when_clear_at_night()
    {
        var now = DateTimeOffset.UtcNow;
        var (nightStart, nightEnd) = NighttimeWindow(now);

        Assert.Equal(WeatherIcons.FiveStar, StatusForm.CloudCoverIcon(Snapshot(20, nightStart, nightEnd)));
    }
}
