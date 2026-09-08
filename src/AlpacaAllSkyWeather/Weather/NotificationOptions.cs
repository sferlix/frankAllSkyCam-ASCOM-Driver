namespace AlpacaAllSkyWeather.Weather;

/// <summary>Telegram alert settings. Free (no billing, no per-user account cost) — see
/// docs/superpowers/plans for why Telegram was chosen over WhatsApp/SMS providers.</summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public bool Enabled { get; set; } = false;

    public string TelegramBotToken { get; set; } = "";

    public string TelegramChatId { get; set; } = "";
}
