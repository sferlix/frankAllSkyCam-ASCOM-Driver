using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.Tests.Weather;

public class SafetyAlertServiceTests
{
    [Fact]
    public void BuildTransitionMessage_returns_null_on_the_first_check()
    {
        var message = SafetyAlertService.BuildTransitionMessage(
            previousIsSafe: null, currentIsSafe: true, unsafeReasons: Array.Empty<string>());

        Assert.Null(message);
    }

    [Fact]
    public void BuildTransitionMessage_returns_null_when_the_state_has_not_changed()
    {
        Assert.Null(SafetyAlertService.BuildTransitionMessage(true, true, Array.Empty<string>()));
        Assert.Null(SafetyAlertService.BuildTransitionMessage(false, false, new[] { "pioggia rilevata" }));
    }

    [Fact]
    public void BuildTransitionMessage_reports_the_reasons_when_becoming_unsafe()
    {
        var message = SafetyAlertService.BuildTransitionMessage(
            previousIsSafe: true, currentIsSafe: false, unsafeReasons: new[] { "pioggia rilevata", "copertura nuvolosa 95%" });

        Assert.NotNull(message);
        Assert.Contains("pioggia rilevata", message);
        Assert.Contains("copertura nuvolosa 95%", message);
    }

    [Fact]
    public void BuildTransitionMessage_reports_recovery_when_becoming_safe_again()
    {
        var message = SafetyAlertService.BuildTransitionMessage(
            previousIsSafe: false, currentIsSafe: true, unsafeReasons: Array.Empty<string>());

        Assert.NotNull(message);
        Assert.DoesNotContain("pioggia", message);
    }

    private static WeatherSnapshot NightSnapshot(DateTimeOffset now) => new(
        CloudCover: 0, DewPoint: 0, Humidity: 0, Pressure: 0, RainRate: 0, SkyQuality: 0,
        SkyBrightness: 0, Temperature: 0, WindDirection: 0, WindSpeed: 0, WindGust: 0, StarCount: 0,
        NightStart: now.AddHours(-1), NightEnd: now.AddHours(1), PolledAtUtc: now);

    [Fact]
    public void IsWithinNightWindow_is_false_when_there_is_no_snapshot_yet()
    {
        Assert.False(SafetyAlertService.IsWithinNightWindow(null, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IsWithinNightWindow_is_true_when_now_is_inside_NightStart_and_NightEnd()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(SafetyAlertService.IsWithinNightWindow(NightSnapshot(now), now));
    }

    [Fact]
    public void IsWithinNightWindow_is_false_when_now_is_outside_NightStart_and_NightEnd()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = NightSnapshot(now) with { NightStart = now.AddHours(-5), NightEnd = now.AddHours(-1) };
        Assert.False(SafetyAlertService.IsWithinNightWindow(snapshot, now));
    }

    [Fact]
    public void ShouldNotify_is_false_when_notifications_are_disabled_regardless_of_time()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.False(SafetyAlertService.ShouldNotify(enabled: false, notifyOnlyAtNight: false, NightSnapshot(now), now));
        Assert.False(SafetyAlertService.ShouldNotify(enabled: false, notifyOnlyAtNight: true, NightSnapshot(now), now));
    }

    [Fact]
    public void ShouldNotify_is_true_at_any_time_of_day_when_night_only_is_off()
    {
        var now = DateTimeOffset.UtcNow;
        var daytimeSnapshot = NightSnapshot(now) with { NightStart = now.AddHours(-5), NightEnd = now.AddHours(-1) };

        Assert.True(SafetyAlertService.ShouldNotify(enabled: true, notifyOnlyAtNight: false, daytimeSnapshot, now));
    }

    [Fact]
    public void ShouldNotify_is_false_during_the_day_when_night_only_is_on()
    {
        var now = DateTimeOffset.UtcNow;
        var daytimeSnapshot = NightSnapshot(now) with { NightStart = now.AddHours(-5), NightEnd = now.AddHours(-1) };

        Assert.False(SafetyAlertService.ShouldNotify(enabled: true, notifyOnlyAtNight: true, daytimeSnapshot, now));
    }

    [Fact]
    public void ShouldNotify_is_true_at_night_when_night_only_is_on()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.True(SafetyAlertService.ShouldNotify(enabled: true, notifyOnlyAtNight: true, NightSnapshot(now), now));
    }

    [Fact]
    public void ShouldNotify_is_false_when_night_only_is_on_and_there_is_no_weather_data_yet()
    {
        Assert.False(SafetyAlertService.ShouldNotify(enabled: true, notifyOnlyAtNight: true, snapshot: null, DateTimeOffset.UtcNow));
    }
}
