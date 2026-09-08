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
}
