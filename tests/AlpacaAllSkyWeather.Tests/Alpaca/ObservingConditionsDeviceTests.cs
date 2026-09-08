using ASCOM.Common.Alpaca;
using AlpacaAllSkyWeather.Alpaca;
using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.Tests.Alpaca;

public class ObservingConditionsDeviceTests
{
    private static WeatherJsonDto SampleDto() => new()
    {
        CloudCover = 21.4,
        DewPoint = 14.57,
        Humidity = 54,
        Pressure = 916.5,
        RainRate = 0,
        SkyQuality = 0,
        SkyBrightness = 70950,
        Temperature = 24.5,
        WindDirection = 184,
        WindSpeed = 1.8,
        WindGust = 1.9,
        StarCount = 7,
    };

    [Fact]
    public void CloudCover_throws_ValueNotSet_before_any_poll()
    {
        var device = new ObservingConditionsDevice(new WeatherState());

        var ex = Assert.Throws<AlpacaDeviceException>(() => device.CloudCover);
        Assert.Equal(AlpacaErrors.ValueNotSet, ex.ErrorNumber);
    }

    [Fact]
    public void Standard_properties_map_directly_from_the_latest_snapshot()
    {
        var state = new WeatherState();
        state.Update(SampleDto(), DateTimeOffset.UtcNow);
        var device = new ObservingConditionsDevice(state);

        Assert.Equal(21.4, device.CloudCover);
        Assert.Equal(14.57, device.DewPoint);
        Assert.Equal(54, device.Humidity);
        Assert.Equal(916.5, device.Pressure);
        Assert.Equal(0, device.RainRate);
        Assert.Equal(70950, device.SkyBrightness);
        Assert.Equal(0, device.SkyQuality); // passed through as-is, even when 0
        Assert.Equal(24.5, device.Temperature);
        Assert.Equal(184, device.WindDirection);
        Assert.Equal(1.9, device.WindGust);
        Assert.Equal(1.8, device.WindSpeed);
    }

    [Fact]
    public void Stale_but_present_data_is_still_returned_without_throwing()
    {
        var state = new WeatherState();
        state.Update(SampleDto(), DateTimeOffset.UtcNow.AddHours(-2));
        var device = new ObservingConditionsDevice(state);

        Assert.Equal(21.4, device.CloudCover);
    }

    [Fact]
    public void SkyTemperature_and_StarFWHM_always_throw_NotImplemented()
    {
        var state = new WeatherState();
        state.Update(SampleDto(), DateTimeOffset.UtcNow);
        var device = new ObservingConditionsDevice(state);

        var ex1 = Assert.Throws<AlpacaDeviceException>(() => device.SkyTemperature);
        Assert.Equal(AlpacaErrors.NotImplemented, ex1.ErrorNumber);

        var ex2 = Assert.Throws<AlpacaDeviceException>(() => device.StarFWHM);
        Assert.Equal(AlpacaErrors.NotImplemented, ex2.ErrorNumber);
    }

    [Fact]
    public void AveragePeriod_accepts_only_zero()
    {
        var device = new ObservingConditionsDevice(new WeatherState());

        device.AveragePeriod = 0;
        Assert.Equal(0, device.AveragePeriod);

        var ex = Assert.Throws<AlpacaDeviceException>(() => device.AveragePeriod = 5);
        Assert.Equal(AlpacaErrors.InvalidValue, ex.ErrorNumber);
    }

    [Fact]
    public void TimeSinceLastUpdate_reports_the_age_of_the_last_successful_poll()
    {
        var state = new WeatherState();
        state.Update(SampleDto(), DateTimeOffset.UtcNow.AddMinutes(-5));
        var device = new ObservingConditionsDevice(state);

        var age = device.TimeSinceLastUpdate("CloudCover");

        Assert.InRange(age, 295, 310);
    }

    [Fact]
    public void TimeSinceLastUpdate_with_empty_sensorName_reports_the_overall_latest_age()
    {
        var state = new WeatherState();
        state.Update(SampleDto(), DateTimeOffset.UtcNow.AddMinutes(-5));
        var device = new ObservingConditionsDevice(state);

        var age = device.TimeSinceLastUpdate("");

        Assert.InRange(age, 295, 310);
    }

    [Fact]
    public void TimeSinceLastUpdate_throws_NotImplemented_for_unsupported_sensors()
    {
        // The ASCOM spec requires value, description and time-since-last-update to be either
        // all implemented or all not implemented for a given sensor (Consistency check).
        var state = new WeatherState();
        state.Update(SampleDto(), DateTimeOffset.UtcNow);
        var device = new ObservingConditionsDevice(state);

        var ex1 = Assert.Throws<AlpacaDeviceException>(() => device.TimeSinceLastUpdate("SkyTemperature"));
        Assert.Equal(AlpacaErrors.NotImplemented, ex1.ErrorNumber);

        var ex2 = Assert.Throws<AlpacaDeviceException>(() => device.TimeSinceLastUpdate("StarFWHM"));
        Assert.Equal(AlpacaErrors.NotImplemented, ex2.ErrorNumber);
    }

    [Fact]
    public void TimeSinceLastUpdate_throws_InvalidValue_for_an_unknown_sensor_name()
    {
        var state = new WeatherState();
        state.Update(SampleDto(), DateTimeOffset.UtcNow);
        var device = new ObservingConditionsDevice(state);

        var ex = Assert.Throws<AlpacaDeviceException>(() => device.TimeSinceLastUpdate("NotASensor"));
        Assert.Equal(AlpacaErrors.InvalidValue, ex.ErrorNumber);
    }

    [Fact]
    public void Connected_defaults_to_true_and_is_settable()
    {
        var device = new ObservingConditionsDevice(new WeatherState());

        Assert.True(device.Connected);

        device.Connected = false;

        Assert.False(device.Connected);
    }

    [Fact]
    public void SensorDescription_returns_text_for_known_sensors_and_errors_for_others()
    {
        var state = new WeatherState();
        state.Update(SampleDto(), DateTimeOffset.UtcNow);
        var device = new ObservingConditionsDevice(state);

        Assert.False(string.IsNullOrWhiteSpace(device.SensorDescription("CloudCover")));

        var notImplemented = Assert.Throws<AlpacaDeviceException>(() => device.SensorDescription("StarFWHM"));
        Assert.Equal(AlpacaErrors.NotImplemented, notImplemented.ErrorNumber);

        var invalid = Assert.Throws<AlpacaDeviceException>(() => device.SensorDescription("NotASensor"));
        Assert.Equal(AlpacaErrors.InvalidValue, invalid.ErrorNumber);
    }

    [Fact]
    public void GetStarCount_returns_the_raw_value_as_a_string()
    {
        var state = new WeatherState();
        state.Update(SampleDto(), DateTimeOffset.UtcNow);
        var device = new ObservingConditionsDevice(state);

        Assert.Equal("7", device.GetStarCount());
    }
}
