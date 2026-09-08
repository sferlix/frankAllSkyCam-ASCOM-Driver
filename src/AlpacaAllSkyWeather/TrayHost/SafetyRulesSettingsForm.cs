using System.Globalization;
using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.TrayHost;

/// <summary>Lets the user toggle and tune every SafetyMonitor rule, persisting to
/// appsettings.json on Save (picked up live by SafetyMonitorDevice via IOptionsMonitor).</summary>
public sealed class SafetyRulesSettingsForm : Form
{
    private static readonly Color Background = Color.FromArgb(0x14, 0x16, 0x1F);
    private static readonly Color FieldBackground = Color.FromArgb(0x1E, 0x21, 0x2D);
    private static readonly Color TextPrimary = Color.FromArgb(0xF5, 0xF6, 0xFA);
    private static readonly Color TextMuted = Color.FromArgb(0x8A, 0x8F, 0xA3);

    private readonly string _appSettingsPath;
    private readonly TextBox _maxDataAgeTextBox;
    private readonly List<RuleRow> _rows = new();
    private readonly CheckBox _nightWindowCheckBox;

    public SafetyRulesSettingsForm(SafetyRulesOptions current, string appSettingsPath)
    {
        _appSettingsPath = appSettingsPath;

        Text = "Safety threshold settings";
        BackColor = Background;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        ClientSize = new Size(500, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Icon = TrayIconFactory.CreateIcon();

        var headerLabel = new Label
        {
            Text = "Each threshold can be enabled or disabled individually.\nUnsafe when the value goes above (>) or below (<) the threshold.",
            ForeColor = TextMuted,
            AutoSize = false,
            Size = new Size(470, 34),
            Location = new Point(15, 12),
        };

        var scrollPanel = new Panel
        {
            Location = new Point(15, 52),
            Size = new Size(470, 306),
            AutoScroll = true,
            BackColor = Background,
        };

        var flow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            BackColor = Background,
            Location = Point.Empty,
        };

        var maxAgeRow = new Panel { Size = new Size(445, 28), BackColor = Background };
        var maxAgeLabel = new Label { Text = "Weather data too old after", ForeColor = TextPrimary, AutoSize = true, Location = new Point(0, 5) };
        _maxDataAgeTextBox = new TextBox
        {
            Text = current.MaxDataAgeMinutes.ToString(CultureInfo.InvariantCulture),
            BackColor = FieldBackground,
            ForeColor = TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Right,
            Location = new Point(310, 2),
            Size = new Size(50, 22),
        };
        var maxAgeUnit = new Label { Text = "min", ForeColor = TextMuted, AutoSize = true, Location = new Point(365, 5) };
        maxAgeRow.Controls.Add(maxAgeLabel);
        maxAgeRow.Controls.Add(_maxDataAgeTextBox);
        maxAgeRow.Controls.Add(maxAgeUnit);
        flow.Controls.Add(maxAgeRow);

        AddRow(flow, "Cloud cover >", "%", current.CloudCover, (o, r) => o.CloudCover = r);
        AddRow(flow, "Rain >", "mm/h", current.RainRate, (o, r) => o.RainRate = r);
        AddRow(flow, "Wind gust >", "m/s", current.WindGust, (o, r) => o.WindGust = r);
        AddRow(flow, "Wind speed >", "m/s", current.WindSpeed, (o, r) => o.WindSpeed = r);
        AddRow(flow, "Sky brightness >", "lux", current.SkyBrightness, (o, r) => o.SkyBrightness = r);
        AddRow(flow, "Humidity >", "%", current.Humidity, (o, r) => o.Humidity = r);
        AddRow(flow, "Dew point >", "°C", current.DewPoint, (o, r) => o.DewPoint = r);
        AddRow(flow, "Sky quality <", "mag/arcsec²", current.SkyQuality, (o, r) => o.SkyQuality = r);
        AddRow(flow, "Temperature <", "°C", current.TemperatureMin, (o, r) => o.TemperatureMin = r);
        AddRow(flow, "Pressure <", "hPa", current.PressureMin, (o, r) => o.PressureMin = r);
        AddRow(flow, "Stars detected <", "", current.StarCountMin, (o, r) => o.StarCountMin = r);

        var nightRow = new Panel { Size = new Size(445, 28), BackColor = Background };
        _nightWindowCheckBox = new CheckBox { AutoSize = true, Location = new Point(0, 5), Checked = current.NightWindowEnabled };
        var nightLabel = new Label
        {
            Text = "Outside the night window (Night Start/End)",
            ForeColor = TextPrimary,
            AutoSize = true,
            Location = new Point(22, 6),
        };
        nightRow.Controls.Add(_nightWindowCheckBox);
        nightRow.Controls.Add(nightLabel);
        flow.Controls.Add(nightRow);

        scrollPanel.Controls.Add(flow);

        var saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = FieldBackground,
            ForeColor = TextPrimary,
            Location = new Point(304, 378),
            Size = new Size(80, 28),
        };
        saveButton.Click += (_, _) => Save();

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            FlatStyle = FlatStyle.Flat,
            BackColor = FieldBackground,
            ForeColor = TextMuted,
            Location = new Point(400, 378),
            Size = new Size(80, 28),
        };

        Controls.Add(headerLabel);
        Controls.Add(scrollPanel);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;
    }

    private void AddRow(FlowLayoutPanel flow, string label, string unit, ThresholdRule initial, Action<SafetyRulesOptions, ThresholdRule> setter)
    {
        var row = new RuleRow(label, unit, initial, setter);
        _rows.Add(row);
        flow.Controls.Add(row);
    }

    private void Save()
    {
        var options = new SafetyRulesOptions
        {
            MaxDataAgeMinutes = ParseOrDefault(_maxDataAgeTextBox.Text, 10),
            NightWindowEnabled = _nightWindowCheckBox.Checked,
        };

        foreach (var row in _rows)
        {
            row.ApplyTo(options);
        }

        AppSettingsWriter.UpdateSafetyRules(_appSettingsPath, options);
    }

    private static double ParseOrDefault(string text, double fallback)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;

    /// <summary>One "[x] Label _____ unit" row, wired via <paramref name="setter"/> (passed at
    /// construction) to know which SafetyRulesOptions property it writes on Save.</summary>
    private sealed class RuleRow : Panel
    {
        private readonly CheckBox _enabledCheckBox;
        private readonly TextBox _thresholdTextBox;
        private readonly Action<SafetyRulesOptions, ThresholdRule> _setter;

        public RuleRow(string label, string unit, ThresholdRule current, Action<SafetyRulesOptions, ThresholdRule> setter)
        {
            _setter = setter;
            Size = new Size(445, 28);
            BackColor = Background;

            _enabledCheckBox = new CheckBox { AutoSize = true, Location = new Point(0, 5), Checked = current.Enabled };
            var nameLabel = new Label { Text = label, ForeColor = TextPrimary, AutoSize = true, Location = new Point(22, 6) };
            _thresholdTextBox = new TextBox
            {
                Text = current.Threshold.ToString(CultureInfo.InvariantCulture),
                BackColor = FieldBackground,
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                TextAlign = HorizontalAlignment.Right,
                Location = new Point(310, 2),
                Size = new Size(50, 22),
            };
            var unitLabel = new Label { Text = unit, ForeColor = TextMuted, AutoSize = true, Location = new Point(365, 6) };

            Controls.Add(_enabledCheckBox);
            Controls.Add(nameLabel);
            Controls.Add(_thresholdTextBox);
            Controls.Add(unitLabel);
        }

        public void ApplyTo(SafetyRulesOptions options)
        {
            var rule = new ThresholdRule
            {
                Enabled = _enabledCheckBox.Checked,
                Threshold = ParseOrDefault(_thresholdTextBox.Text, 0),
            };
            _setter(options, rule);
        }
    }
}
