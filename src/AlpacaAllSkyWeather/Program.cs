using AlpacaAllSkyWeather.Alpaca;
using AlpacaAllSkyWeather.Weather;

var builder = WebApplication.CreateBuilder(args);

// Alpaca requires exact PascalCase JSON property names (ClientTransactionID, ErrorNumber, ...).
// ASP.NET Core's Minimal API JSON defaults to camelCase, which would silently break the protocol.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
});

builder.Services.Configure<WeatherPollerOptions>(
    builder.Configuration.GetSection(WeatherPollerOptions.SectionName));
builder.Services.AddSingleton<WeatherState>();
builder.Services.AddSingleton<ObservingConditionsDevice>();
builder.Services.AddSingleton(sp =>
    new ObservingConditionsDeviceName(sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<WeatherPollerOptions>>().Value.DeviceName));
builder.Services.AddHttpClient();
builder.Services.AddHostedService<WeatherPollerService>();

var httpPort = builder.Configuration.GetValue(
    $"{WeatherPollerOptions.SectionName}:{nameof(WeatherPollerOptions.HttpPort)}", 51111);
builder.WebHost.UseUrls($"http://127.0.0.1:{httpPort}");

var app = builder.Build();

app.MapAlpacaCommonEndpoints();
app.MapObservingConditionsEndpoints();
app.MapManagementEndpoints();

app.Run();

public partial class Program { }
