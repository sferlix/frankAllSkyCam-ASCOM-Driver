using AlpacaAllSkyWeather.Weather;
using Microsoft.Extensions.Options;

namespace AlpacaAllSkyWeather.Alpaca;

/// <summary>Derives a simple safe/unsafe signal from the shared weather state, using
/// configurable thresholds (see <see cref="SafetyRulesOptions"/>). Missing or stale data is
/// treated as unsafe — the conservative default for a safety-critical signal, unlike
/// <see cref="ObservingConditionsDevice"/>, which keeps serving the last known reading.</summary>
public sealed class SafetyMonitorDevice
{
    private readonly WeatherState _state;
    private readonly SafetyRulesOptions _rules;

    public SafetyMonitorDevice(WeatherState state, IOptions<SafetyRulesOptions> rules)
    {
        _state = state;
        _rules = rules.Value;
    }

    public bool Connected { get; set; } = true;

    public bool IsSafe => UnsafeReasons.Count == 0;

    public IReadOnlyList<string> UnsafeReasons
    {
        get
        {
            var snapshot = _state.TryGetLatest();
            if (snapshot is null)
            {
                return new[] { "nessun dato meteo ricevuto" };
            }

            var reasons = new List<string>();

            var ageMinutes = (DateTimeOffset.UtcNow - snapshot.PolledAtUtc).TotalMinutes;
            if (ageMinutes > _rules.MaxDataAgeMinutes)
            {
                reasons.Add($"dato meteo vecchio di {ageMinutes:0} minuti");
            }

            if (snapshot.CloudCover > _rules.MaxCloudCoverPercent)
            {
                reasons.Add($"copertura nuvolosa {snapshot.CloudCover:0}%");
            }

            if (snapshot.RainRate > _rules.MaxRainRate)
            {
                reasons.Add("pioggia rilevata");
            }

            if (snapshot.WindGust > _rules.MaxWindGustSpeed)
            {
                reasons.Add($"raffica di vento {snapshot.WindGust:0} m/s");
            }

            if (snapshot.SkyBrightness > _rules.MaxSkyBrightnessLux)
            {
                reasons.Add($"cielo troppo luminoso ({snapshot.SkyBrightness:0} lux)");
            }

            return reasons;
        }
    }
}
