using AlpacaAllSkyWeather.Weather;
using Microsoft.Extensions.Options;

namespace AlpacaAllSkyWeather.Alpaca;

/// <summary>Derives a simple safe/unsafe signal from the shared weather state, using
/// configurable per-quantity thresholds (see <see cref="SafetyRulesOptions"/>). Missing or stale
/// data is always treated as unsafe regardless of which rules are enabled — the conservative
/// default for a safety-critical signal, unlike <see cref="ObservingConditionsDevice"/>, which
/// keeps serving the last known reading.</summary>
public sealed class SafetyMonitorDevice
{
    private readonly WeatherState _state;
    private readonly IOptionsMonitor<SafetyRulesOptions> _rules;

    public SafetyMonitorDevice(WeatherState state, IOptionsMonitor<SafetyRulesOptions> rules)
    {
        _state = state;
        _rules = rules;
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

            var rules = _rules.CurrentValue;
            var reasons = new List<string>();

            var ageMinutes = (DateTimeOffset.UtcNow - snapshot.PolledAtUtc).TotalMinutes;
            if (ageMinutes > rules.MaxDataAgeMinutes)
            {
                reasons.Add($"dato meteo vecchio di {ageMinutes:0} minuti");
            }

            AddIfAbove(reasons, rules.CloudCover, snapshot.CloudCover, v => $"copertura nuvolosa {v:0}%");
            AddIfAbove(reasons, rules.RainRate, snapshot.RainRate, _ => "pioggia rilevata");
            AddIfAbove(reasons, rules.WindGust, snapshot.WindGust, v => $"raffica di vento {v:0} m/s");
            AddIfAbove(reasons, rules.WindSpeed, snapshot.WindSpeed, v => $"vento {v:0} m/s");
            AddIfAbove(reasons, rules.SkyBrightness, snapshot.SkyBrightness, v => $"cielo troppo luminoso ({v:0} lux)");
            AddIfAbove(reasons, rules.Humidity, snapshot.Humidity, v => $"umidità {v:0}%");
            AddIfAbove(reasons, rules.DewPoint, snapshot.DewPoint, v => $"punto di rugiada {v:0.0}°C");

            AddIfBelow(reasons, rules.SkyQuality, snapshot.SkyQuality, v => $"qualità cielo bassa ({v:0.0} mag/arcsec²)");
            AddIfBelow(reasons, rules.TemperatureMin, snapshot.Temperature, v => $"temperatura {v:0.0}°C");
            AddIfBelow(reasons, rules.PressureMin, snapshot.Pressure, v => $"pressione bassa {v:0.0} hPa");
            AddIfBelow(reasons, rules.StarCountMin, snapshot.StarCount, v => $"poche stelle rilevate ({(int)v})");

            if (rules.NightWindowEnabled)
            {
                var now = DateTimeOffset.UtcNow;
                if (now < snapshot.NightStart || now > snapshot.NightEnd)
                {
                    reasons.Add("fuori dalla finestra notturna");
                }
            }

            return reasons;
        }
    }

    private static void AddIfAbove(List<string> reasons, ThresholdRule rule, double value, Func<double, string> reason)
    {
        if (rule.Enabled && value > rule.Threshold)
        {
            reasons.Add(reason(value));
        }
    }

    private static void AddIfBelow(List<string> reasons, ThresholdRule rule, double value, Func<double, string> reason)
    {
        if (rule.Enabled && value < rule.Threshold)
        {
            reasons.Add(reason(value));
        }
    }
}
