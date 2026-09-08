namespace AlpacaAllSkyWeather.Weather;

public sealed class SafetyRulesOptions
{
    public const string SectionName = "SafetyMonitor";

    public string DeviceName { get; set; } = "AllSky Safety Monitor";

    public double MaxCloudCoverPercent { get; set; } = 80;

    public double MaxRainRate { get; set; } = 0;

    public double MaxWindGustSpeed { get; set; } = 15;

    public double MaxSkyBrightnessLux { get; set; } = 1000;

    public double MaxDataAgeMinutes { get; set; } = 10;
}
