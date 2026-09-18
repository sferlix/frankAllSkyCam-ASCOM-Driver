using System.Text.Json.Nodes;
using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.Tests.Weather;

public class AppSettingsWriterTests
{
    [Fact]
    public void UpdateNotificationSettings_writes_a_new_Notifications_section()
    {
        var path = Path.Combine(Path.GetTempPath(), $"appsettings-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{ "AllSkyWeather": { "HttpPort": 51111 } }""");

            AppSettingsWriter.UpdateNotificationSettings(path, new NotificationOptions
            {
                Enabled = true,
                TelegramBotToken = "123:ABC",
                TelegramChatId = "999",
                AllSkyCamImageUrl = "https://example.com/allsky.jpg",
                NotifyOnlyAtNight = true,
            });

            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var notifications = root["Notifications"]!.AsObject();
            Assert.True(notifications["Enabled"]!.GetValue<bool>());
            Assert.Equal("123:ABC", notifications["TelegramBotToken"]!.GetValue<string>());
            Assert.Equal("999", notifications["TelegramChatId"]!.GetValue<string>());
            Assert.Equal("https://example.com/allsky.jpg", notifications["AllSkyCamImageUrl"]!.GetValue<string>());
            Assert.True(notifications["NotifyOnlyAtNight"]!.GetValue<bool>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UpdateNotificationSettings_preserves_other_sections()
    {
        var path = Path.Combine(Path.GetTempPath(), $"appsettings-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{ "AllSkyWeather": { "HttpPort": 51111, "DeviceName": "AllSky Weather" } }""");

            AppSettingsWriter.UpdateNotificationSettings(path, new NotificationOptions { Enabled = false });

            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var allSkyWeather = root["AllSkyWeather"]!.AsObject();
            Assert.Equal(51111, allSkyWeather["HttpPort"]!.GetValue<int>());
            Assert.Equal("AllSky Weather", allSkyWeather["DeviceName"]!.GetValue<string>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UpdateNotificationSettings_overwrites_a_previously_written_section()
    {
        var path = Path.Combine(Path.GetTempPath(), $"appsettings-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{ "Notifications": { "Enabled": true, "TelegramBotToken": "old", "TelegramChatId": "old" } }""");

            AppSettingsWriter.UpdateNotificationSettings(path, new NotificationOptions
            {
                Enabled = false,
                TelegramBotToken = "new",
                TelegramChatId = "new",
            });

            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var notifications = root["Notifications"]!.AsObject();
            Assert.False(notifications["Enabled"]!.GetValue<bool>());
            Assert.Equal("new", notifications["TelegramBotToken"]!.GetValue<string>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UpdateNotificationSettings_creates_the_file_when_it_does_not_exist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"appsettings-{Guid.NewGuid():N}.json");
        try
        {
            Assert.False(File.Exists(path));

            AppSettingsWriter.UpdateNotificationSettings(path, new NotificationOptions { Enabled = true });

            Assert.True(File.Exists(path));
            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            Assert.True(root["Notifications"]!["Enabled"]!.GetValue<bool>());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void UpdateSafetyRules_writes_every_rule_and_preserves_other_sections()
    {
        var path = Path.Combine(Path.GetTempPath(), $"appsettings-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """{ "AllSkyWeather": { "HttpPort": 51111 } }""");

            var options = new SafetyRulesOptions
            {
                MaxDataAgeMinutes = 20,
                CloudCover = new ThresholdRule { Enabled = false, Threshold = 70 },
                NightWindowEnabled = true,
            };
            AppSettingsWriter.UpdateSafetyRules(path, options);

            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var allSkyWeather = root["AllSkyWeather"]!.AsObject();
            Assert.Equal(51111, allSkyWeather["HttpPort"]!.GetValue<int>());

            var safety = root["SafetyMonitor"]!.AsObject();
            Assert.Equal(20, safety["MaxDataAgeMinutes"]!.GetValue<double>());
            Assert.False(safety["CloudCover"]!["Enabled"]!.GetValue<bool>());
            Assert.Equal(70, safety["CloudCover"]!["Threshold"]!.GetValue<double>());
            Assert.True(safety["NightWindowEnabled"]!.GetValue<bool>());
            Assert.True(safety["RainRate"]!["Enabled"]!.GetValue<bool>()); // default from a fresh SafetyRulesOptions
        }
        finally
        {
            File.Delete(path);
        }
    }
}
