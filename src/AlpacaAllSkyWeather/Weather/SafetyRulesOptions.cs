namespace AlpacaAllSkyWeather.Weather;

/// <summary>One independently-toggleable safety rule: a threshold that, when crossed, makes
/// the SafetyMonitor report "unsafe". Disabled rules are never evaluated.</summary>
public sealed class ThresholdRule
{
    public bool Enabled { get; set; }

    public double Threshold { get; set; }
}

public sealed class SafetyRulesOptions
{
    public const string SectionName = "SafetyMonitor";

    public string DeviceName { get; set; } = "AllSky Safety Monitor";

    /// <summary>Always active (not a per-quantity rule): missing or stale data is always
    /// treated conservatively as unsafe, regardless of what the rules below say.</summary>
    public double MaxDataAgeMinutes { get; set; } = 10;

    // Unsafe when the reading is ABOVE the threshold:
    public ThresholdRule CloudCover { get; set; } = new() { Enabled = true, Threshold = 80 };
    public ThresholdRule RainRate { get; set; } = new() { Enabled = true, Threshold = 0 };
    public ThresholdRule WindGust { get; set; } = new() { Enabled = true, Threshold = 15 };
    public ThresholdRule WindSpeed { get; set; } = new() { Enabled = false, Threshold = 12 };
    public ThresholdRule SkyBrightness { get; set; } = new() { Enabled = true, Threshold = 1000 };
    public ThresholdRule Humidity { get; set; } = new() { Enabled = false, Threshold = 90 };
    public ThresholdRule DewPoint { get; set; } = new() { Enabled = false, Threshold = 20 };

    // Unsafe when the reading is BELOW the threshold:
    public ThresholdRule SkyQuality { get; set; } = new() { Enabled = false, Threshold = 18 };
    public ThresholdRule TemperatureMin { get; set; } = new() { Enabled = false, Threshold = -10 };
    public ThresholdRule PressureMin { get; set; } = new() { Enabled = false, Threshold = 990 };
    public ThresholdRule StarCountMin { get; set; } = new() { Enabled = false, Threshold = 3 };

    /// <summary>No threshold value: unsafe whenever "now" falls outside [NightStart, NightEnd]
    /// from the latest poll, i.e. it's daytime.</summary>
    public bool NightWindowEnabled { get; set; } = false;
}
