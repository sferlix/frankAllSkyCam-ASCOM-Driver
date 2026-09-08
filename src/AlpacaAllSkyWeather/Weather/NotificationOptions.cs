namespace AlpacaAllSkyWeather.Weather;

/// <summary>Telegram alert settings. Free (no billing, no per-user account cost) — see
/// docs/superpowers/plans for why Telegram was chosen over WhatsApp/SMS providers.</summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    public bool Enabled { get; set; } = false;

    public string TelegramBotToken { get; set; } = "";

    public string TelegramChatId { get; set; } = "";

    /// <summary>HTTP(S) address of the AllSkyCam's live image. When set, TelegramCommandListener
    /// sends a copy of it (in addition to the status screenshot) on "now"/"status".</summary>
    public string AllSkyCamImageUrl { get; set; } = "";
}
