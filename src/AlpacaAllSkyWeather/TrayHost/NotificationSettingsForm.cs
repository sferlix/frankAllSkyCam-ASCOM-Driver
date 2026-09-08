using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.TrayHost;

/// <summary>Lets the user enter Telegram bot credentials and test them, persisting to
/// appsettings.json on Save (picked up live by SafetyAlertService via IOptionsMonitor).</summary>
public sealed class NotificationSettingsForm : Form
{
    private static readonly Color Background = Color.FromArgb(0x14, 0x16, 0x1F);
    private static readonly Color FieldBackground = Color.FromArgb(0x1E, 0x21, 0x2D);
    private static readonly Color TextPrimary = Color.FromArgb(0xF5, 0xF6, 0xFA);
    private static readonly Color TextMuted = Color.FromArgb(0x8A, 0x8F, 0xA3);
    private static readonly Color AccentWind = Color.FromArgb(0x66, 0xD9, 0xC2);
    private static readonly Color StatusOk = Color.FromArgb(0x4C, 0xD9, 0x7B);
    private static readonly Color StatusError = Color.FromArgb(0xE5, 0x73, 0x73);

    private readonly TelegramNotifier _notifier;
    private readonly string _appSettingsPath;
    private readonly CheckBox _enabledCheckBox;
    private readonly TextBox _botTokenTextBox;
    private readonly TextBox _chatIdTextBox;
    private readonly Label _statusLabel;

    public NotificationSettingsForm(NotificationOptions current, TelegramNotifier notifier, string appSettingsPath)
    {
        _notifier = notifier;
        _appSettingsPath = appSettingsPath;

        Text = "Impostazioni notifiche";
        BackColor = Background;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        ClientSize = new Size(380, 320);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Icon = TrayIconFactory.CreateIcon();

        var infoLabel = new Label
        {
            Text = "Ricevi un messaggio Telegram quando le condizioni diventano non sicure (e quando tornano sicure).",
            ForeColor = TextMuted,
            AutoSize = false,
            Size = new Size(340, 40),
            Location = new Point(20, 16),
        };

        _enabledCheckBox = new CheckBox
        {
            Text = "Abilita notifiche Telegram",
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(20, 64),
            Checked = current.Enabled,
        };

        var tokenLabel = new Label { Text = "Bot Token", ForeColor = TextMuted, AutoSize = true, Location = new Point(20, 96) };
        _botTokenTextBox = new TextBox
        {
            Text = current.TelegramBotToken,
            BackColor = FieldBackground,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(20, 116),
            Size = new Size(340, 24),
        };

        var chatIdLabel = new Label { Text = "Chat ID", ForeColor = TextMuted, AutoSize = true, Location = new Point(20, 148) };
        _chatIdTextBox = new TextBox
        {
            Text = current.TelegramChatId,
            BackColor = FieldBackground,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(20, 168),
            Size = new Size(340, 24),
        };

        var testButton = new Button
        {
            Text = "Invia messaggio di prova",
            FlatStyle = FlatStyle.Flat,
            BackColor = FieldBackground,
            ForeColor = AccentWind,
            Location = new Point(20, 200),
            Size = new Size(180, 28),
        };
        testButton.FlatAppearance.BorderColor = AccentWind;
        testButton.Click += async (_, _) => await TestAsync();

        _statusLabel = new Label
        {
            ForeColor = TextMuted,
            AutoSize = false,
            Size = new Size(340, 76),
            Location = new Point(20, 236),
        };

        var saveButton = new Button
        {
            Text = "Salva",
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = FieldBackground,
            ForeColor = TextPrimary,
            Location = new Point(184, 200),
            Size = new Size(80, 28),
        };
        saveButton.Click += (_, _) => Save();

        var cancelButton = new Button
        {
            Text = "Annulla",
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat,
            BackColor = FieldBackground,
            ForeColor = TextMuted,
            Location = new Point(280, 200),
            Size = new Size(80, 28),
        };

        Controls.Add(infoLabel);
        Controls.Add(_enabledCheckBox);
        Controls.Add(tokenLabel);
        Controls.Add(_botTokenTextBox);
        Controls.Add(chatIdLabel);
        Controls.Add(_chatIdTextBox);
        Controls.Add(testButton);
        Controls.Add(_statusLabel);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private async Task TestAsync()
    {
        _statusLabel.ForeColor = TextMuted;
        _statusLabel.Text = "Invio in corso...";
        var result = await _notifier.SendAsync(
            _botTokenTextBox.Text.Trim(), _chatIdTextBox.Text.Trim(), "🔭 AllSky Weather: messaggio di prova.");
        _statusLabel.ForeColor = result.Success ? StatusOk : StatusError;
        _statusLabel.Text = result.Success ? "Messaggio inviato." : $"Invio fallito: {result.ErrorDetail}";
    }

    private void Save()
    {
        var options = new NotificationOptions
        {
            Enabled = _enabledCheckBox.Checked,
            TelegramBotToken = _botTokenTextBox.Text.Trim(),
            TelegramChatId = _chatIdTextBox.Text.Trim(),
        };
        AppSettingsWriter.UpdateNotificationSettings(_appSettingsPath, options);
    }
}
