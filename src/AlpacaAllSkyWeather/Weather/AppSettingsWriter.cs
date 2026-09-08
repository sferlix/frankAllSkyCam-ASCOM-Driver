using System.Text.Json;
using System.Text.Json.Nodes;

namespace AlpacaAllSkyWeather.Weather;

/// <summary>Persists settings edited from the tray UI back into appsettings.json, preserving
/// every other section. The install directory is per-user (see the installer), so it's always
/// writable without elevation.</summary>
public static class AppSettingsWriter
{
    public static void UpdateNotificationSettings(string appSettingsPath, NotificationOptions options)
    {
        var root = File.Exists(appSettingsPath)
            ? JsonNode.Parse(File.ReadAllText(appSettingsPath))!.AsObject()
            : new JsonObject();

        root[NotificationOptions.SectionName] = new JsonObject
        {
            ["Enabled"] = options.Enabled,
            ["TelegramBotToken"] = options.TelegramBotToken,
            ["TelegramChatId"] = options.TelegramChatId,
        };

        File.WriteAllText(appSettingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }
}
