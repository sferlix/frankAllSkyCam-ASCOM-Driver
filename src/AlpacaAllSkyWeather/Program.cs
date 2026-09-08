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

var app = builder.Build();

app.MapAlpacaCommonEndpoints();
app.MapObservingConditionsEndpoints();

app.Run();

public partial class Program { }
