using AlpacaAllSkyWeather.Alpaca;
using AlpacaAllSkyWeather.Weather;
using Microsoft.Extensions.Options;

namespace AlpacaAllSkyWeather.Tests.Alpaca;

public class SafetyMonitorDeviceTests
{
    private static WeatherJsonDto SafeDto() => new()
    {
        CloudCover = 20,
        RainRate = 0,
        WindGust = 5,
        WindSpeed = 3,
        SkyBrightness = 10,
        SkyQuality = 21.0,
        Humidity = 40,
        Temperature = 15,
        DewPoint = 5,
        Pressure = 1013,
        StarCount = 50,
        NightStart = DateTimeOffset.UtcNow.AddHours(-1),
        NightEnd = DateTimeOffset.UtcNow.AddHours(1),
    };

    private static SafetyMonitorDevice CreateDevice(WeatherState state, SafetyRulesOptions? rules = null)
        => new(state, new StaticOptionsMonitor<SafetyRulesOptions>(rules ?? new SafetyRulesOptions()));

    /// <summary>Minimal IOptionsMonitor fake: SafetyMonitorDevice uses IOptionsMonitor (not
    /// IOptions) so appsettings.json edits are picked up live without a restart.</summary>
    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T value) => CurrentValue = value;
        public T CurrentValue { get; }
        public T Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }

    [Fact]
    public void IsSafe_is_false_before_any_poll()
    {
        var device = CreateDevice(new WeatherState());

        Assert.False(device.IsSafe);
        Assert.Contains(device.UnsafeReasons, r => r.Contains("data", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IsSafe_is_true_when_all_readings_are_within_thresholds_using_default_rules()
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
    public void Disabled_rules_are_never_evaluated()
    {
        var rules = new SafetyRulesOptions
        {
            CloudCover = new ThresholdRule { Enabled = false, Threshold = 5 }, // would fail if evaluated
        };
        var state = new WeatherState();
        state.Update(SafeDto() with { CloudCover = 99 }, DateTimeOffset.UtcNow);
        var device = CreateDevice(state, rules);

        Assert.True(device.IsSafe);
    }

    [Theory]
    [InlineData(nameof(WeatherJsonDto.CloudCover), 95, "cloud")]
    [InlineData(nameof(WeatherJsonDto.RainRate), 0.1, "rain")]
    [InlineData(nameof(WeatherJsonDto.WindGust), 20, "gust")]
    [InlineData(nameof(WeatherJsonDto.SkyBrightness), 5000, "bright")]
    public void Above_threshold_rules_report_unsafe_when_enabled(string field, double value, string expectedReasonSubstring)
    {
        var rules = new SafetyRulesOptions();
        var dto = field switch
        {
            nameof(WeatherJsonDto.CloudCover) => SafeDto() with { CloudCover = value },
            nameof(WeatherJsonDto.RainRate) => SafeDto() with { RainRate = value },
            nameof(WeatherJsonDto.WindGust) => SafeDto() with { WindGust = value },
            nameof(WeatherJsonDto.SkyBrightness) => SafeDto() with { SkyBrightness = value },
            _ => throw new ArgumentException(field),
        };
        var state = new WeatherState();
        state.Update(dto, DateTimeOffset.UtcNow);
        var device = CreateDevice(state, rules);

        Assert.False(device.IsSafe);
        Assert.Contains(device.UnsafeReasons, r => r.Contains(expectedReasonSubstring, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WindSpeed_rule_reports_unsafe_when_enabled_and_exceeded()
    {
        var rules = new SafetyRulesOptions { WindSpeed = new ThresholdRule { Enabled = true, Threshold = 10 } };
        var state = new WeatherState();
        state.Update(SafeDto() with { WindSpeed = 20 }, DateTimeOffset.UtcNow);
        var device = CreateDevice(state, rules);

        Assert.False(device.IsSafe);
        Assert.Contains(device.UnsafeReasons, r => r.Contains("wind", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Humidity_rule_reports_unsafe_when_enabled_and_exceeded()
    {
        var rules = new SafetyRulesOptions { Humidity = new ThresholdRule { Enabled = true, Threshold = 80 } };
        var state = new WeatherState();
        state.Update(SafeDto() with { Humidity = 95 }, DateTimeOffset.UtcNow);
        var device = CreateDevice(state, rules);

        Assert.False(device.IsSafe);
        Assert.Contains(device.UnsafeReasons, r => r.Contains("humid", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DewPoint_rule_reports_unsafe_when_enabled_and_exceeded()
    {
        var rules = new SafetyRulesOptions { DewPoint = new ThresholdRule { Enabled = true, Threshold = 15 } };
        var state = new WeatherState();
        state.Update(SafeDto() with { DewPoint = 18 }, DateTimeOffset.UtcNow);
        var device = CreateDevice(state, rules);

        Assert.False(device.IsSafe);
        Assert.Contains(device.UnsafeReasons, r => r.Contains("dew", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SkyQuality_rule_reports_unsafe_when_enabled_and_below_threshold()
    {
        var rules = new SafetyRulesOptions { SkyQuality = new ThresholdRule { Enabled = true, Threshold = 18 } };
        var state = new WeatherState();
        state.Update(SafeDto() with { SkyQuality = 5 }, DateTimeOffset.UtcNow);
        var device = CreateDevice(state, rules);

        Assert.False(device.IsSafe);
        Assert.Contains(device.UnsafeReasons, r => r.Contains("quality", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TemperatureMin_rule_reports_unsafe_when_enabled_and_below_threshold()
    {
        var rules = new SafetyRulesOptions { TemperatureMin = new ThresholdRule { Enabled = true, Threshold = 0 } };
        var state = new WeatherState();
        state.Update(SafeDto() with { Temperature = -5 }, DateTimeOffset.UtcNow);
        var device = CreateDevice(state, rules);

        Assert.False(device.IsSafe);
        Assert.Contains(device.UnsafeReasons, r => r.Contains("temperature", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PressureMin_rule_reports_unsafe_when_enabled_and_below_threshold()
    {
        var rules = new SafetyRulesOptions { PressureMin = new ThresholdRule { Enabled = true, Threshold = 1000 } };
        var state = new WeatherState();
        state.Update(SafeDto() with { Pressure = 980 }, DateTimeOffset.UtcNow);
        var device = CreateDevice(state, rules);

        Assert.False(device.IsSafe);
        Assert.Contains(device.UnsafeReasons, r => r.Contains("pressure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void StarCountMin_rule_reports_unsafe_when_enabled_and_below_threshold()
    {
        var rules = new SafetyRulesOptions { StarCountMin = new ThresholdRule { Enabled = true, Threshold = 10 } };
        var state = new WeatherState();
        state.Update(SafeDto() with { StarCount = 2 }, DateTimeOffset.UtcNow);
        var device = CreateDevice(state, rules);

        Assert.False(device.IsSafe);
        Assert.Contains(device.UnsafeReasons, r => r.Contains("stars", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NightWindow_rule_reports_unsafe_when_enabled_and_now_is_outside_the_window()
    {
        var rules = new SafetyRulesOptions { NightWindowEnabled = true };
        var state = new WeatherState();
        // NightStart/NightEnd both in the past: "now" is after the window (daytime).
        state.Update(SafeDto() with
        {
            NightStart = DateTimeOffset.UtcNow.AddHours(-5),
            NightEnd = DateTimeOffset.UtcNow.AddHours(-1),
        }, DateTimeOffset.UtcNow);
        var device = CreateDevice(state, rules);

        Assert.False(device.IsSafe);
        Assert.Contains(device.UnsafeReasons, r => r.Contains("night", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NightWindow_rule_is_safe_when_now_is_inside_the_window()
    {
        var rules = new SafetyRulesOptions { NightWindowEnabled = true };
        var state = new WeatherState();
        state.Update(SafeDto(), DateTimeOffset.UtcNow); // SafeDto: NightStart -1h, NightEnd +1h -> now is inside
        var device = CreateDevice(state, rules);

        Assert.True(device.IsSafe);
    }

    [Fact]
    public void NightWindow_rule_is_ignored_when_disabled_even_outside_the_window()
    {
        var rules = new SafetyRulesOptions { NightWindowEnabled = false };
        var state = new WeatherState();
        state.Update(SafeDto() with
        {
            NightStart = DateTimeOffset.UtcNow.AddHours(-5),
            NightEnd = DateTimeOffset.UtcNow.AddHours(-1),
        }, DateTimeOffset.UtcNow);
        var device = CreateDevice(state, rules);

        Assert.True(device.IsSafe);
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
