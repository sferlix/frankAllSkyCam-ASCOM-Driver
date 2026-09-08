using AlpacaAllSkyWeather.Alpaca;
using AlpacaAllSkyWeather.Weather;
using Microsoft.Extensions.Options;

namespace AlpacaAllSkyWeather.Tests.Alpaca;

public class SafetyMonitorDeviceTests
{
    private static readonly SafetyRulesOptions DefaultRules = new();

    private static WeatherJsonDto SafeDto() => new()
    {
        CloudCover = 20,
        RainRate = 0,
        WindGust = 5,
        SkyBrightness = 10,
    };

    private static SafetyMonitorDevice CreateDevice(WeatherState state, SafetyRulesOptions? rules = null)
        => new(state, Options.Create(rules ?? DefaultRules));

    [Fact]
    public void IsSafe_is_false_before_any_poll()
    {
        var device = CreateDevice(new WeatherState());

        Assert.False(device.IsSafe);
        Assert.Contains(device.UnsafeReasons, r => r.Contains("dato", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IsSafe_is_true_when_all_readings_are_within_thresholds()
    {
        var state = new WeatherState();
        state.Update(SafeDto(), DateTimeOffset.UtcNow);
        var device = CreateDevice(state);

        Assert.True(device.IsSafe);
        Assert.Empty(device.UnsafeReasons);
    }

    [Fact]
    public void IsSafe_is_false_when_data_is_older_than_the_configured_max_age()
    {
        var state = new WeatherState();
        state.Update(SafeDto(), DateTimeOffset.UtcNow.AddMinutes(-15));
        var device = CreateDevice(state);

        Assert.False(device.IsSafe);
    }

    [Fact]
    public void IsSafe_is_false_when_cloud_cover_exceeds_the_threshold()
    {
        var state = new WeatherState();
        state.Update(SafeDto() with { CloudCover = 95 }, DateTimeOffset.UtcNow);
        var device = CreateDevice(state);

        Assert.False(device.IsSafe);
        Assert.Contains(device.UnsafeReasons, r => r.Contains("nuvol", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IsSafe_is_false_when_it_is_raining()
    {
        var state = new WeatherState();
        state.Update(SafeDto() with { RainRate = 0.1 }, DateTimeOffset.UtcNow);
        var device = CreateDevice(state);

        Assert.False(device.IsSafe);
        Assert.Contains(device.UnsafeReasons, r => r.Contains("pioggia", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IsSafe_is_false_when_wind_gust_exceeds_the_threshold()
    {
        var state = new WeatherState();
        state.Update(SafeDto() with { WindGust = 20 }, DateTimeOffset.UtcNow);
        var device = CreateDevice(state);

        Assert.False(device.IsSafe);
        Assert.Contains(device.UnsafeReasons, r => r.Contains("raffica", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IsSafe_is_false_when_sky_is_too_bright()
    {
        var state = new WeatherState();
        state.Update(SafeDto() with { SkyBrightness = 5000 }, DateTimeOffset.UtcNow);
        var device = CreateDevice(state);

        Assert.False(device.IsSafe);
        Assert.Contains(device.UnsafeReasons, r => r.Contains("luminos", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Connected_defaults_to_true_and_is_settable()
    {
        var device = CreateDevice(new WeatherState());

        Assert.True(device.Connected);

        device.Connected = false;

        Assert.False(device.Connected);
    }
}
