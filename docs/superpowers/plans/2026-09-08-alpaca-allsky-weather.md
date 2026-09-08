# AlpacaAllSkyWeather Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Windows tray application that polls `frankAllSkyCam`'s `weather.json` and exposes the data as an ASCOM Alpaca `ObservingConditions` device over HTTP, so N.I.N.A./SharpCap can use it as a weather source.

**Architecture:** A single .NET 8 (`net8.0-windows`) console/WinForms exe hosts an ASP.NET Core Kestrel server (Alpaca REST API) and a `BackgroundService` that polls the remote JSON every 60s into a thread-safe in-memory state. A WinForms `NotifyIcon` wraps the host for tray start/stop/exit. No ASCOM Platform / COM dependency.

**Tech Stack:** .NET 8, ASP.NET Core Minimal APIs, `System.Text.Json`, WinForms (`NotifyIcon`), xUnit, `Microsoft.AspNetCore.Mvc.Testing`, NuGet package `ASCOM.Common.Components` 4.0.0 (official ASCOM Initiative library — verified by direct assembly inspection to be net8.0-compatible, COM-free, and to contain `AlpacaErrors` and the Alpaca response envelope types used below).

**Spec:** [`docs/superpowers/specs/2026-09-08-alpaca-allsky-weather-design.md`](../specs/2026-09-08-alpaca-allsky-weather-design.md)

## Global Constraints

- Target framework: `net8.0-windows` for both the main project and the test project (WinForms requires Windows; the test project references the main project via `WebApplicationFactory<Program>` so must match).
- No ASCOM Platform / COM interop anywhere in the codebase.
- No UDP Alpaca discovery (out of scope per spec).
- HTTP port default: `51111`, overridable in `appsettings.json` under `AllSkyWeather:HttpPort`.
- Source JSON URL: `https://www.meteobrallo.com/webcam/allsky/weather.json`, overridable under `AllSkyWeather:SourceUrl`.
- All Alpaca device-level errors return **HTTP 200** with `ErrorNumber`/`ErrorMessage` populated in the JSON body (per the Alpaca API spec, HTTP error codes are reserved for transport-level failures, not device errors).
- `WindSpeed`/`WindGust` in the source JSON are already in m/s (confirmed by the user) — no unit conversion anywhere.
- `SkyQuality` is passed through as-is, including when it is `0` (confirmed by the user) — no special-casing.

---

### Task 1: Solution and project scaffolding

**Files:**
- Create: `AlpacaAllSkyWeather.sln`
- Create: `src/AlpacaAllSkyWeather/AlpacaAllSkyWeather.csproj`
- Create: `src/AlpacaAllSkyWeather/Program.cs`
- Create: `src/AlpacaAllSkyWeather/appsettings.json`
- Create: `tests/AlpacaAllSkyWeather.Tests/AlpacaAllSkyWeather.Tests.csproj`
- Create: `tests/AlpacaAllSkyWeather.Tests/Usings.cs`
- Create: `tests/AlpacaAllSkyWeather.Tests/PlaceholderTests.cs`

**Interfaces:**
- Produces: a buildable solution with a `WinExe` main project (`net8.0-windows`, WinForms + ASP.NET Core framework reference, `ASCOM.Common.Components` 4.0.0 package reference) and an `xunit` test project referencing it.

- [ ] **Step 1: Create the main project file**

`src/AlpacaAllSkyWeather/AlpacaAllSkyWeather.csproj`:

Uses `Microsoft.NET.Sdk.Web` (not the plain `Microsoft.NET.Sdk`) specifically so the
ASP.NET Core implicit global usings (`WebApplication`, `HttpRequest`, `IResult`,
`Results`, `IServiceCollection` extensions, etc.) are generated automatically —
Sdk.Web also brings in the `Microsoft.AspNetCore.App` framework reference on its
own, so it does not need to be declared explicitly. `UseWindowsForms` still works
under Sdk.Web; this WinForms-tray-plus-Kestrel combination is a supported pattern.

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>AlpacaAllSkyWeather</RootNamespace>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="ASCOM.Common.Components" Version="4.0.0" />
  </ItemGroup>

  <ItemGroup>
    <None Update="appsettings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
```

- [ ] **Step 2: Create a minimal placeholder `Program.cs` (replaced fully in Task 9)**

`src/AlpacaAllSkyWeather/Program.cs`:

```csharp
Console.WriteLine("AlpacaAllSkyWeather");

public partial class Program { }
```

The `public partial class Program { }` marker is required so `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>` (used by the integration tests in Tasks 6-8) can reference this type even though the file uses top-level statements.

- [ ] **Step 3: Create `appsettings.json`**

`src/AlpacaAllSkyWeather/appsettings.json`:

```json
{
  "AllSkyWeather": {
    "SourceUrl": "https://www.meteobrallo.com/webcam/allsky/weather.json",
    "PollIntervalSeconds": 60,
    "HttpPort": 51111,
    "DeviceName": "AllSky Weather"
  }
}
```

- [ ] **Step 3b: Create a shared `global using` file for the test project**

`xunit`'s `[Fact]`/`Assert` are not covered by `ImplicitUsings`, so every test file
would otherwise need its own `using Xunit;`. One shared file avoids repeating it:

`tests/AlpacaAllSkyWeather.Tests/Usings.cs`:

```csharp
global using Xunit;
```

- [ ] **Step 4: Create the test project file**

`tests/AlpacaAllSkyWeather.Tests/AlpacaAllSkyWeather.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.10" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\AlpacaAllSkyWeather\AlpacaAllSkyWeather.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Create a placeholder test so the test project builds and runs**

`tests/AlpacaAllSkyWeather.Tests/PlaceholderTests.cs`:

```csharp
namespace AlpacaAllSkyWeather.Tests;

public class PlaceholderTests
{
    [Fact]
    public void Placeholder_passes()
    {
        Assert.True(true);
    }
}
```

- [ ] **Step 6: Create the solution file and wire up the projects**

```bash
dotnet new sln -n AlpacaAllSkyWeather --format sln
dotnet sln add src/AlpacaAllSkyWeather/AlpacaAllSkyWeather.csproj
dotnet sln add tests/AlpacaAllSkyWeather.Tests/AlpacaAllSkyWeather.Tests.csproj
```

`--format sln` is required on the .NET 10 SDK, which otherwise defaults to the newer
`.slnx` format — that needs Visual Studio 2022 17.13+, so the classic `.sln` is the
safe choice for compatibility with whatever Visual Studio Community version is
installed.

- [ ] **Step 7: Build and run the placeholder test**

```bash
dotnet build
dotnet test
```

Expected: build succeeds, 1 test passes.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Scaffold solution: main WinForms+ASP.NET Core project and xunit test project"
```

---

### Task 2: Weather JSON DTO and parsing

**Files:**
- Create: `src/AlpacaAllSkyWeather/Weather/WeatherJsonDto.cs`
- Test: `tests/AlpacaAllSkyWeather.Tests/Weather/WeatherJsonDtoTests.cs`

**Interfaces:**
- Produces: `AlpacaAllSkyWeather.Weather.WeatherJsonDto` — a record with `double` properties `CloudCover, DewPoint, Humidity, Pressure, RainRate, SkyQuality, SkyBrightness, Temperature, WindDirection, WindSpeed, WindGust, StarCount`, and `public static WeatherJsonDto Parse(string json)`.

- [ ] **Step 1: Write the failing test**

`tests/AlpacaAllSkyWeather.Tests/Weather/WeatherJsonDtoTests.cs`:

```csharp
using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.Tests.Weather;

public class WeatherJsonDtoTests
{
    // Captured from https://www.meteobrallo.com/webcam/allsky/weather.json on 2026-09-08.
    private const string SampleJson = """
    {
      "timestamp": "2026-09-08T09:37:03Z",
      "timestamp_local": "2026-09-08T11:37:03+02:00",
      "NightStart": "2026-09-08T19:28:48Z",
      "NightEnd": "2026-09-09T03:12:24Z",
      "CloudCover": 21.4,
      "DewPoint": 14.57,
      "Humidity": 54,
      "Pressure": 916.5,
      "SeaLevelPressure": 1023.27,
      "RainRate": 0,
      "SkyQuality": 0,
      "SkyBrightness": 70950,
      "Temperature": 24.5,
      "UVIndex": 5,
      "WindDirection": 184,
      "WindSpeed": 1.8,
      "WindGust": 1.9,
      "StarCount": 0
    }
    """;

    [Fact]
    public void Parse_reads_all_mapped_fields_from_real_sample()
    {
        var dto = WeatherJsonDto.Parse(SampleJson);

        Assert.Equal(21.4, dto.CloudCover);
        Assert.Equal(14.57, dto.DewPoint);
        Assert.Equal(54, dto.Humidity);
        Assert.Equal(916.5, dto.Pressure);
        Assert.Equal(0, dto.RainRate);
        Assert.Equal(0, dto.SkyQuality);
        Assert.Equal(70950, dto.SkyBrightness);
        Assert.Equal(24.5, dto.Temperature);
        Assert.Equal(184, dto.WindDirection);
        Assert.Equal(1.8, dto.WindSpeed);
        Assert.Equal(1.9, dto.WindGust);
        Assert.Equal(0, dto.StarCount);
    }

    [Fact]
    public void Parse_ignores_unmapped_fields_without_throwing()
    {
        // timestamp/timestamp_local/NightStart/NightEnd/SeaLevelPressure/UVIndex
        // are present in the real payload but unused by this driver (see spec, YAGNI section).
        var dto = WeatherJsonDto.Parse(SampleJson);

        Assert.NotNull(dto);
    }

    [Fact]
    public void Parse_throws_JsonException_on_malformed_json()
    {
        Assert.Throws<System.Text.Json.JsonException>(() => WeatherJsonDto.Parse("{ not valid json"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test --filter WeatherJsonDtoTests
```

Expected: FAIL — `WeatherJsonDto` does not exist.

- [ ] **Step 3: Implement `WeatherJsonDto`**

`src/AlpacaAllSkyWeather/Weather/WeatherJsonDto.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlpacaAllSkyWeather.Weather;

public sealed record WeatherJsonDto
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [JsonPropertyName("CloudCover")]
    public double CloudCover { get; init; }

    [JsonPropertyName("DewPoint")]
    public double DewPoint { get; init; }

    [JsonPropertyName("Humidity")]
    public double Humidity { get; init; }

    [JsonPropertyName("Pressure")]
    public double Pressure { get; init; }

    [JsonPropertyName("RainRate")]
    public double RainRate { get; init; }

    [JsonPropertyName("SkyQuality")]
    public double SkyQuality { get; init; }

    [JsonPropertyName("SkyBrightness")]
    public double SkyBrightness { get; init; }

    [JsonPropertyName("Temperature")]
    public double Temperature { get; init; }

    [JsonPropertyName("WindDirection")]
    public double WindDirection { get; init; }

    [JsonPropertyName("WindSpeed")]
    public double WindSpeed { get; init; }

    [JsonPropertyName("WindGust")]
    public double WindGust { get; init; }

    [JsonPropertyName("StarCount")]
    public double StarCount { get; init; }

    public static WeatherJsonDto Parse(string json)
        => JsonSerializer.Deserialize<WeatherJsonDto>(json, Options)
           ?? throw new JsonException("weather.json deserialized to null");
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test --filter WeatherJsonDtoTests
```

Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AlpacaAllSkyWeather/Weather/WeatherJsonDto.cs tests/AlpacaAllSkyWeather.Tests/Weather/WeatherJsonDtoTests.cs
git commit -m "Add WeatherJsonDto with JSON parsing for weather.json"
```

---

### Task 3: Thread-safe shared weather state

**Files:**
- Create: `src/AlpacaAllSkyWeather/Weather/WeatherState.cs`
- Test: `tests/AlpacaAllSkyWeather.Tests/Weather/WeatherStateTests.cs`

**Interfaces:**
- Consumes: `WeatherJsonDto` (Task 2).
- Produces: `AlpacaAllSkyWeather.Weather.WeatherSnapshot` (record with the 12 mapped fields plus `DateTimeOffset PolledAtUtc`), and `AlpacaAllSkyWeather.Weather.WeatherState` with `void Update(WeatherJsonDto dto, DateTimeOffset polledAtUtc)` and `WeatherSnapshot? TryGetLatest()`. `TryGetLatest()` is the sole read path every later consumer (Task 5) uses.

- [ ] **Step 1: Write the failing test**

`tests/AlpacaAllSkyWeather.Tests/Weather/WeatherStateTests.cs`:

```csharp
using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.Tests.Weather;

public class WeatherStateTests
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
        StarCount = 0,
    };

    [Fact]
    public void TryGetLatest_returns_null_before_any_update()
    {
        var state = new WeatherState();

        Assert.Null(state.TryGetLatest());
    }

    [Fact]
    public void TryGetLatest_returns_mapped_snapshot_after_update()
    {
        var state = new WeatherState();
        var polledAt = new DateTimeOffset(2026, 9, 8, 9, 37, 3, TimeSpan.Zero);

        state.Update(SampleDto(), polledAt);
        var snapshot = state.TryGetLatest();

        Assert.NotNull(snapshot);
        Assert.Equal(21.4, snapshot!.CloudCover);
        Assert.Equal(1.8, snapshot.WindSpeed);
        Assert.Equal(polledAt, snapshot.PolledAtUtc);
    }

    [Fact]
    public void Update_overwrites_previous_snapshot()
    {
        var state = new WeatherState();
        state.Update(SampleDto(), DateTimeOffset.UtcNow.AddMinutes(-5));

        var newer = SampleDto() with { CloudCover = 80.0 };
        var polledAt = DateTimeOffset.UtcNow;
        state.Update(newer, polledAt);

        var snapshot = state.TryGetLatest();
        Assert.Equal(80.0, snapshot!.CloudCover);
        Assert.Equal(polledAt, snapshot.PolledAtUtc);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test --filter WeatherStateTests
```

Expected: FAIL — `WeatherState` does not exist.

- [ ] **Step 3: Implement `WeatherState`**

`src/AlpacaAllSkyWeather/Weather/WeatherState.cs`:

```csharp
namespace AlpacaAllSkyWeather.Weather;

public sealed record WeatherSnapshot(
    double CloudCover,
    double DewPoint,
    double Humidity,
    double Pressure,
    double RainRate,
    double SkyQuality,
    double SkyBrightness,
    double Temperature,
    double WindDirection,
    double WindSpeed,
    double WindGust,
    double StarCount,
    DateTimeOffset PolledAtUtc);

public sealed class WeatherState
{
    private readonly object _lock = new();
    private WeatherSnapshot? _latest;

    public void Update(WeatherJsonDto dto, DateTimeOffset polledAtUtc)
    {
        var snapshot = new WeatherSnapshot(
            dto.CloudCover,
            dto.DewPoint,
            dto.Humidity,
            dto.Pressure,
            dto.RainRate,
            dto.SkyQuality,
            dto.SkyBrightness,
            dto.Temperature,
            dto.WindDirection,
            dto.WindSpeed,
            dto.WindGust,
            dto.StarCount,
            polledAtUtc);

        lock (_lock)
        {
            _latest = snapshot;
        }
    }

    public WeatherSnapshot? TryGetLatest()
    {
        lock (_lock)
        {
            return _latest;
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test --filter WeatherStateTests
```

Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AlpacaAllSkyWeather/Weather/WeatherState.cs tests/AlpacaAllSkyWeather.Tests/Weather/WeatherStateTests.cs
git commit -m "Add thread-safe WeatherState shared between the poller and the Alpaca device"
```

---

### Task 4: Weather poller background service

**Files:**
- Create: `src/AlpacaAllSkyWeather/Weather/WeatherPollerOptions.cs`
- Create: `src/AlpacaAllSkyWeather/Weather/WeatherPollerService.cs`
- Create: `src/AlpacaAllSkyWeather/AssemblyInfo.cs`
- Test: `tests/AlpacaAllSkyWeather.Tests/Weather/WeatherPollerServiceTests.cs`

**Interfaces:**
- Consumes: `WeatherJsonDto.Parse` (Task 2), `WeatherState.Update`/`TryGetLatest` (Task 3).
- Produces: `AlpacaAllSkyWeather.Weather.WeatherPollerOptions` (bound from config section `"AllSkyWeather"`: `SourceUrl`, `PollIntervalSeconds`, `HttpPort`, `DeviceName`) and `AlpacaAllSkyWeather.Weather.WeatherPollerService : BackgroundService` with an `internal static Task PollOnceAsync(HttpClient httpClient, WeatherState state, string sourceUrl, ILogger logger, CancellationToken cancellationToken)` that later tasks and tests call directly.

- [ ] **Step 1: Write the failing tests**

`tests/AlpacaAllSkyWeather.Tests/Weather/WeatherPollerServiceTests.cs`:

```csharp
using System.Net;
using AlpacaAllSkyWeather.Weather;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlpacaAllSkyWeather.Tests.Weather;

public class WeatherPollerServiceTests
{
    private const string SampleJson = """
    {
      "CloudCover": 21.4, "DewPoint": 14.57, "Humidity": 54, "Pressure": 916.5,
      "RainRate": 0, "SkyQuality": 0, "SkyBrightness": 70950, "Temperature": 24.5,
      "WindDirection": 184, "WindSpeed": 1.8, "WindGust": 1.9, "StarCount": 0
    }
    """;

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string? _body;

        public StubHandler(HttpStatusCode statusCode, string? body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode);
            if (_body is not null)
            {
                response.Content = new StringContent(_body);
            }
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task PollOnceAsync_updates_state_on_success()
    {
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.OK, SampleJson));
        var state = new WeatherState();

        await WeatherPollerService.PollOnceAsync(
            httpClient, state, "http://example.invalid/weather.json",
            NullLogger.Instance, CancellationToken.None);

        var snapshot = state.TryGetLatest();
        Assert.NotNull(snapshot);
        Assert.Equal(21.4, snapshot!.CloudCover);
    }

    [Fact]
    public async Task PollOnceAsync_leaves_state_unchanged_on_http_error()
    {
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.InternalServerError, null));
        var state = new WeatherState();

        await WeatherPollerService.PollOnceAsync(
            httpClient, state, "http://example.invalid/weather.json",
            NullLogger.Instance, CancellationToken.None);

        Assert.Null(state.TryGetLatest());
    }

    [Fact]
    public async Task PollOnceAsync_leaves_state_unchanged_on_malformed_json()
    {
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.OK, "{ not valid"));
        var state = new WeatherState();

        await WeatherPollerService.PollOnceAsync(
            httpClient, state, "http://example.invalid/weather.json",
            NullLogger.Instance, CancellationToken.None);

        Assert.Null(state.TryGetLatest());
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test --filter WeatherPollerServiceTests
```

Expected: FAIL — `WeatherPollerService` does not exist.

- [ ] **Step 3: Implement `WeatherPollerOptions`**

`src/AlpacaAllSkyWeather/Weather/WeatherPollerOptions.cs`:

```csharp
namespace AlpacaAllSkyWeather.Weather;

public sealed class WeatherPollerOptions
{
    public const string SectionName = "AllSkyWeather";

    public string SourceUrl { get; set; } = "https://www.meteobrallo.com/webcam/allsky/weather.json";

    public int PollIntervalSeconds { get; set; } = 60;

    public int HttpPort { get; set; } = 51111;

    public string DeviceName { get; set; } = "AllSky Weather";
}
```

- [ ] **Step 3b: Make internal members visible to the test assembly**

`WeatherPollerService.PollOnceAsync` is `internal` (Step 4 below) so it isn't part
of the driver's public surface, but the test project is a separate assembly and
can't see `internal` members without this:

`src/AlpacaAllSkyWeather/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AlpacaAllSkyWeather.Tests")]
```

- [ ] **Step 4: Implement `WeatherPollerService`**

`src/AlpacaAllSkyWeather/Weather/WeatherPollerService.cs`:

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlpacaAllSkyWeather.Weather;

public sealed class WeatherPollerService : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WeatherState _state;
    private readonly WeatherPollerOptions _options;
    private readonly ILogger<WeatherPollerService> _logger;

    public WeatherPollerService(
        IHttpClientFactory httpClientFactory,
        WeatherState state,
        IOptions<WeatherPollerOptions> options,
        ILogger<WeatherPollerService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _state = state;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var httpClient = _httpClientFactory.CreateClient();

        while (!stoppingToken.IsCancellationRequested)
        {
            await PollOnceAsync(httpClient, _state, _options.SourceUrl, _logger, stoppingToken);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    internal static async Task PollOnceAsync(
        HttpClient httpClient,
        WeatherState state,
        string sourceUrl,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetAsync(sourceUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var dto = WeatherJsonDto.Parse(json);
            state.Update(dto, DateTimeOffset.UtcNow);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Failed to poll weather source {Url}", sourceUrl);
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test --filter WeatherPollerServiceTests
```

Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add src/AlpacaAllSkyWeather/Weather/WeatherPollerOptions.cs src/AlpacaAllSkyWeather/Weather/WeatherPollerService.cs tests/AlpacaAllSkyWeather.Tests/Weather/WeatherPollerServiceTests.cs
git commit -m "Add WeatherPollerService background polling with a testable PollOnceAsync core"
```

---

### Task 5: Alpaca device errors and the ObservingConditions device

**Files:**
- Create: `src/AlpacaAllSkyWeather/Alpaca/AlpacaDeviceException.cs`
- Create: `src/AlpacaAllSkyWeather/Alpaca/ObservingConditionsDevice.cs`
- Test: `tests/AlpacaAllSkyWeather.Tests/Alpaca/ObservingConditionsDeviceTests.cs`

**Interfaces:**
- Consumes: `WeatherState`/`WeatherSnapshot` (Task 3).
- Produces: `AlpacaAllSkyWeather.Alpaca.AlpacaDeviceException(ASCOM.Common.Alpaca.AlpacaErrors errorNumber, string message)` and `AlpacaAllSkyWeather.Alpaca.ObservingConditionsDevice(WeatherState state)`, exposing: `double AveragePeriod { get; set; }`, `double CloudCover/DewPoint/Humidity/Pressure/RainRate/SkyBrightness/SkyQuality/Temperature/WindDirection/WindGust/WindSpeed { get; }`, `double SkyTemperature/StarFWHM { get; }` (always throw), `double TimeSinceLastUpdate(string sensorName)`, `string SensorDescription(string sensorName)`, `string GetStarCount()`, `void Refresh()`. This is the type the REST endpoints in Tasks 6-7 call.

- [ ] **Step 1: Write the failing tests**

`tests/AlpacaAllSkyWeather.Tests/Alpaca/ObservingConditionsDeviceTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test --filter ObservingConditionsDeviceTests
```

Expected: FAIL — `ObservingConditionsDevice`/`AlpacaDeviceException` do not exist.

- [ ] **Step 3: Implement `AlpacaDeviceException`**

`src/AlpacaAllSkyWeather/Alpaca/AlpacaDeviceException.cs`:

```csharp
using ASCOM.Common.Alpaca;

namespace AlpacaAllSkyWeather.Alpaca;

public sealed class AlpacaDeviceException : Exception
{
    public AlpacaErrors ErrorNumber { get; }

    public AlpacaDeviceException(AlpacaErrors errorNumber, string message)
        : base(message)
    {
        ErrorNumber = errorNumber;
    }
}
```

- [ ] **Step 4: Implement `ObservingConditionsDevice`**

`src/AlpacaAllSkyWeather/Alpaca/ObservingConditionsDevice.cs`:

```csharp
using System.Globalization;
using ASCOM.Common.Alpaca;
using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.Alpaca;

public sealed class ObservingConditionsDevice
{
    private static readonly HashSet<string> KnownSensors = new(StringComparer.OrdinalIgnoreCase)
    {
        "CloudCover", "DewPoint", "Humidity", "Pressure", "RainRate", "SkyBrightness",
        "SkyQuality", "Temperature", "WindDirection", "WindGust", "WindSpeed",
    };

    private static readonly HashSet<string> UnsupportedSensors = new(StringComparer.OrdinalIgnoreCase)
    {
        "SkyTemperature", "StarFWHM",
    };

    private readonly WeatherState _state;
    private double _averagePeriod;

    public ObservingConditionsDevice(WeatherState state)
    {
        _state = state;
    }

    public double AveragePeriod
    {
        get => _averagePeriod;
        set
        {
            if (value != 0)
            {
                throw new AlpacaDeviceException(
                    AlpacaErrors.InvalidValue,
                    "AveragePeriod must be 0: this driver reports only the latest instantaneous reading.");
            }

            _averagePeriod = value;
        }
    }

    public double CloudCover => GetValue(s => s.CloudCover);
    public double DewPoint => GetValue(s => s.DewPoint);
    public double Humidity => GetValue(s => s.Humidity);
    public double Pressure => GetValue(s => s.Pressure);
    public double RainRate => GetValue(s => s.RainRate);
    public double SkyBrightness => GetValue(s => s.SkyBrightness);
    public double SkyQuality => GetValue(s => s.SkyQuality);
    public double Temperature => GetValue(s => s.Temperature);
    public double WindDirection => GetValue(s => s.WindDirection);
    public double WindGust => GetValue(s => s.WindGust);
    public double WindSpeed => GetValue(s => s.WindSpeed);

    public double SkyTemperature => throw NotImplemented("SkyTemperature");
    public double StarFWHM => throw NotImplemented("StarFWHM");

    public double TimeSinceLastUpdate(string sensorName)
    {
        var snapshot = GetSnapshotOrThrow();
        return (DateTimeOffset.UtcNow - snapshot.PolledAtUtc).TotalSeconds;
    }

    public string SensorDescription(string sensorName)
    {
        if (UnsupportedSensors.Contains(sensorName))
        {
            throw NotImplemented(sensorName);
        }

        if (!KnownSensors.Contains(sensorName))
        {
            throw new AlpacaDeviceException(AlpacaErrors.InvalidValue, $"Unknown sensor name '{sensorName}'.");
        }

        return $"{sensorName} reported by frankAllSkyCam via weather.json";
    }

    public string GetStarCount()
    {
        var snapshot = GetSnapshotOrThrow();
        return snapshot.StarCount.ToString(CultureInfo.InvariantCulture);
    }

    public void Refresh()
    {
        // No-op: WeatherPollerService already refreshes the shared state on a fixed
        // interval, so there is no separate "on demand" fetch to trigger here.
    }

    private double GetValue(Func<WeatherSnapshot, double> selector)
        => selector(GetSnapshotOrThrow());

    private WeatherSnapshot GetSnapshotOrThrow()
        => _state.TryGetLatest()
           ?? throw new AlpacaDeviceException(
               AlpacaErrors.ValueNotSet,
               "No successful poll of the weather source has completed yet.");

    private static AlpacaDeviceException NotImplemented(string sensorName)
        => new(AlpacaErrors.NotImplemented, $"{sensorName} sensor is not available on this frankAllSkyCam.");
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test --filter ObservingConditionsDeviceTests
```

Expected: PASS (8 tests).

- [ ] **Step 6: Commit**

```bash
git add src/AlpacaAllSkyWeather/Alpaca/AlpacaDeviceException.cs src/AlpacaAllSkyWeather/Alpaca/ObservingConditionsDevice.cs tests/AlpacaAllSkyWeather.Tests/Alpaca/ObservingConditionsDeviceTests.cs
git commit -m "Add ObservingConditionsDevice mapping weather state to Alpaca semantics"
```

---

### Task 6: Alpaca transaction helper and Common device endpoints

**Files:**
- Create: `src/AlpacaAllSkyWeather/Alpaca/AlpacaTransaction.cs`
- Create: `src/AlpacaAllSkyWeather/Alpaca/AlpacaEndpointHelpers.cs`
- Create: `src/AlpacaAllSkyWeather/Alpaca/AlpacaCommonEndpoints.cs`
- Modify: `src/AlpacaAllSkyWeather/Program.cs` (minimal wiring so the test host can boot: DI registrations + endpoint mapping; full composition happens in Task 9)
- Test: `tests/AlpacaAllSkyWeather.Tests/Alpaca/AlpacaCommonEndpointsTests.cs`

**Interfaces:**
- Consumes: `ObservingConditionsDevice` (Task 5), `WeatherPollerOptions`/`WeatherState`/`WeatherPollerService` (Tasks 3-4).
- Produces: `AlpacaAllSkyWeather.Alpaca.AlpacaTransaction` (`NextServerTransactionId()`, `GetClientTransactionIdAsync(HttpRequest)`), `AlpacaAllSkyWeather.Alpaca.AlpacaEndpointHelpers` (`HandleDoubleAsync`, `HandleStringAsync`, `HandleBoolAsync`, `HandleIntAsync`, `HandleMethodAsync`, `HandleStringListAsync` — all `Task<IResult>`, all taking `HttpRequest` plus a value-producing/side-effecting delegate that may throw `AlpacaDeviceException`), and `AlpacaAllSkyWeather.Alpaca.AlpacaCommonEndpoints.MapAlpacaCommonEndpoints(this WebApplication app)` mapping `connected` (GET/PUT), `description`, `driverinfo`, `driverversion`, `interfaceversion`, `name`, `supportedactions`, `action` (PUT) under `/api/v1/observingconditions/0/`. Task 7 reuses `AlpacaEndpointHelpers` for the device-specific sensor routes.

- [ ] **Step 1: Write the failing tests**

`tests/AlpacaAllSkyWeather.Tests/Alpaca/AlpacaCommonEndpointsTests.cs`:

```csharp
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.Tests.Alpaca;

public class AlpacaCommonEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AlpacaCommonEndpointsTests(WebApplicationFactory<Program> factory)
    {
        // The real app also starts WeatherPollerService, which would try to hit the
        // real internet during tests. Integration tests only need the HTTP surface,
        // so the hosted service is removed and the test seeds WeatherState directly.
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.RemoveAll<IHostedService>()));
    }

    private sealed record ConnectedBody(bool Value, uint ClientTransactionID, uint ServerTransactionID, int ErrorNumber, string ErrorMessage);
    private sealed record StringBody(string Value, uint ClientTransactionID, uint ServerTransactionID, int ErrorNumber, string ErrorMessage);
    private sealed record StringListBody(List<string> Value, uint ClientTransactionID, uint ServerTransactionID, int ErrorNumber, string ErrorMessage);

    [Fact]
    public async Task Connected_always_returns_true()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<ConnectedBody>(
            "/api/v1/observingconditions/0/connected?ClientID=1&ClientTransactionID=1");

        Assert.NotNull(body);
        Assert.True(body!.Value);
        Assert.Equal(0, body.ErrorNumber);
    }

    [Fact]
    public async Task Connected_json_uses_exact_PascalCase_property_names_required_by_Alpaca()
    {
        // Minimal API JSON defaults to camelCase; Alpaca requires exact PascalCase.
        // This guards Program.cs's ConfigureHttpJsonOptions override against regressions.
        var client = _factory.CreateClient();

        var raw = await client.GetStringAsync(
            "/api/v1/observingconditions/0/connected?ClientID=1&ClientTransactionID=1");

        Assert.Contains("\"ClientTransactionID\"", raw);
        Assert.Contains("\"ErrorNumber\"", raw);
        Assert.DoesNotContain("\"clientTransactionID\"", raw);
    }

    [Fact]
    public async Task Description_returns_driver_description()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<StringBody>(
            "/api/v1/observingconditions/0/description");

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Value));
    }

    [Fact]
    public async Task SupportedActions_lists_starcount()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<StringListBody>(
            "/api/v1/observingconditions/0/supportedactions");

        Assert.NotNull(body);
        Assert.Contains("starcount", body!.Value, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Action_starcount_returns_the_star_count_from_state()
    {
        var state = _factory.Services.GetRequiredService<WeatherState>();
        state.Update(new WeatherJsonDto { StarCount = 12 }, DateTimeOffset.UtcNow);
        var client = _factory.CreateClient();

        var response = await client.PutAsync(
            "/api/v1/observingconditions/0/action",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Action"] = "starcount",
                ["Parameters"] = "",
                ["ClientID"] = "1",
                ["ClientTransactionID"] = "1",
            }));

        var body = await response.Content.ReadFromJsonAsync<StringBody>();
        Assert.NotNull(body);
        Assert.Equal("12", body!.Value);
        Assert.Equal(0, body.ErrorNumber);
    }

    [Fact]
    public async Task Action_unknown_returns_ActionNotImplemented_error()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsync(
            "/api/v1/observingconditions/0/action",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Action"] = "notarealaction",
                ["Parameters"] = "",
            }));

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StringBody>();
        Assert.NotNull(body);
        Assert.Equal(0x40C, body!.ErrorNumber); // ActionNotImplementedException
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test --filter AlpacaCommonEndpointsTests
```

Expected: FAIL to compile — `Program` has no ASP.NET Core app to test against yet, and the endpoint/helper types do not exist.

- [ ] **Step 3: Implement `AlpacaTransaction`**

`src/AlpacaAllSkyWeather/Alpaca/AlpacaTransaction.cs`:

```csharp
namespace AlpacaAllSkyWeather.Alpaca;

public static class AlpacaTransaction
{
    private static long _serverTransactionCounter;

    public static uint NextServerTransactionId()
        => unchecked((uint)Interlocked.Increment(ref _serverTransactionCounter));

    public static async Task<uint> GetClientTransactionIdAsync(HttpRequest request)
    {
        var raw = HttpMethods.IsPut(request.Method)
            ? (await request.ReadFormAsync())["ClientTransactionID"].ToString()
            : request.Query["ClientTransactionID"].ToString();

        return uint.TryParse(raw, out var value) ? value : 0;
    }
}
```

- [ ] **Step 4: Implement `AlpacaEndpointHelpers`**

`src/AlpacaAllSkyWeather/Alpaca/AlpacaEndpointHelpers.cs`:

```csharp
using ASCOM.Common.Alpaca;

namespace AlpacaAllSkyWeather.Alpaca;

public static class AlpacaEndpointHelpers
{
    public static async Task<IResult> HandleDoubleAsync(HttpRequest request, Func<double> getValue)
    {
        var (clientId, serverId) = await IdsAsync(request);
        try
        {
            return Results.Ok(new DoubleResponse(clientId, serverId, getValue()));
        }
        catch (AlpacaDeviceException ex)
        {
            return Results.Ok(new DoubleResponse(clientId, serverId, ex.Message, ex.ErrorNumber));
        }
    }

    public static async Task<IResult> HandleStringAsync(HttpRequest request, Func<string> getValue)
    {
        var (clientId, serverId) = await IdsAsync(request);
        try
        {
            return Results.Ok(new StringResponse(clientId, serverId, getValue()));
        }
        catch (AlpacaDeviceException ex)
        {
            return Results.Ok(new StringResponse(clientId, serverId, ex.Message, ex.ErrorNumber));
        }
    }

    public static async Task<IResult> HandleStringListAsync(HttpRequest request, Func<IList<string>> getValue)
    {
        var (clientId, serverId) = await IdsAsync(request);
        try
        {
            return Results.Ok(new StringListResponse(clientId, serverId, getValue()));
        }
        catch (AlpacaDeviceException ex)
        {
            return Results.Ok(new StringListResponse(clientId, serverId, ex.Message, ex.ErrorNumber));
        }
    }

    public static async Task<IResult> HandleBoolAsync(HttpRequest request, Func<bool> getValue)
    {
        var (clientId, serverId) = await IdsAsync(request);
        try
        {
            return Results.Ok(new BoolResponse(clientId, serverId, getValue()));
        }
        catch (AlpacaDeviceException ex)
        {
            return Results.Ok(new BoolResponse(clientId, serverId, ex.Message, ex.ErrorNumber));
        }
    }

    public static async Task<IResult> HandleIntAsync(HttpRequest request, Func<int> getValue)
    {
        var (clientId, serverId) = await IdsAsync(request);
        try
        {
            return Results.Ok(new IntResponse(clientId, serverId, getValue()));
        }
        catch (AlpacaDeviceException ex)
        {
            return Results.Ok(new IntResponse(clientId, serverId, ex.Message, ex.ErrorNumber));
        }
    }

    public static async Task<IResult> HandleMethodAsync(HttpRequest request, Action action)
    {
        var (clientId, serverId) = await IdsAsync(request);
        try
        {
            action();
            return Results.Ok(new MethodResponse(clientId, serverId));
        }
        catch (AlpacaDeviceException ex)
        {
            return Results.Ok(new MethodResponse(clientId, serverId) { ErrorNumber = ex.ErrorNumber, ErrorMessage = ex.Message });
        }
    }

    private static async Task<(uint ClientId, uint ServerId)> IdsAsync(HttpRequest request)
        => (await AlpacaTransaction.GetClientTransactionIdAsync(request), AlpacaTransaction.NextServerTransactionId());
}
```

- [ ] **Step 5: Implement `AlpacaCommonEndpoints`**

`src/AlpacaAllSkyWeather/Alpaca/AlpacaCommonEndpoints.cs`:

```csharp
using ASCOM.Common.Alpaca;

namespace AlpacaAllSkyWeather.Alpaca;

public static class AlpacaCommonEndpoints
{
    private const string BaseRoute = "/api/v1/observingconditions/0";

    public static void MapAlpacaCommonEndpoints(this WebApplication app)
    {
        app.MapGet($"{BaseRoute}/connected", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleBoolAsync(r, () => true));

        app.MapPut($"{BaseRoute}/connected", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleMethodAsync(r, () => { /* always connected; nothing to change */ }));

        app.MapGet($"{BaseRoute}/description", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleStringAsync(r, () =>
                "AllSky Weather - bridges frankAllSkyCam's weather.json to ASCOM Alpaca ObservingConditions"));

        app.MapGet($"{BaseRoute}/driverinfo", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleStringAsync(r, () => "AlpacaAllSkyWeather"));

        app.MapGet($"{BaseRoute}/driverversion", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleStringAsync(r, () => "1.0"));

        app.MapGet($"{BaseRoute}/interfaceversion", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleIntAsync(r, () => 2));

        app.MapGet($"{BaseRoute}/name", (HttpRequest r, ObservingConditionsDeviceName deviceName) =>
            AlpacaEndpointHelpers.HandleStringAsync(r, () => deviceName.Value));

        app.MapGet($"{BaseRoute}/supportedactions", (HttpRequest r) =>
            AlpacaEndpointHelpers.HandleStringListAsync(r, () => new List<string> { "starcount" }));

        app.MapPut($"{BaseRoute}/action", async (HttpRequest r, ObservingConditionsDevice device) =>
        {
            var form = await r.ReadFormAsync();
            var action = form["Action"].ToString();

            return await AlpacaEndpointHelpers.HandleStringAsync(r, () => action.Equals("starcount", StringComparison.OrdinalIgnoreCase)
                ? device.GetStarCount()
                : throw new AlpacaDeviceException(AlpacaErrors.ActionNotImplementedException, $"Action '{action}' is not supported."));
        });
    }
}

/// <summary>Thin wrapper so the configured device name can be injected without exposing all of WeatherPollerOptions to the endpoint.</summary>
public sealed record ObservingConditionsDeviceName(string Value);
```

- [ ] **Step 6: Wire up `Program.cs` enough for the test host to boot**

Replace the contents of `src/AlpacaAllSkyWeather/Program.cs` with:

```csharp
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

app.Run();

public partial class Program { }
```

(Task 7 adds the ObservingConditions sensor routes, Task 8 adds the management routes, Task 9 replaces this file with the final tray-aware composition root.)

- [ ] **Step 7: Run the tests to verify they pass**

```bash
dotnet test --filter AlpacaCommonEndpointsTests
```

Expected: PASS (6 tests).

- [ ] **Step 8: Commit**

```bash
git add src/AlpacaAllSkyWeather/Alpaca/AlpacaTransaction.cs src/AlpacaAllSkyWeather/Alpaca/AlpacaEndpointHelpers.cs src/AlpacaAllSkyWeather/Alpaca/AlpacaCommonEndpoints.cs src/AlpacaAllSkyWeather/Program.cs tests/AlpacaAllSkyWeather.Tests/Alpaca/AlpacaCommonEndpointsTests.cs
git commit -m "Add Alpaca Common Method endpoints (connected, description, action, ...)"
```

---

### Task 7: ObservingConditions sensor endpoints

**Files:**
- Create: `src/AlpacaAllSkyWeather/Alpaca/ObservingConditionsEndpoints.cs`
- Modify: `src/AlpacaAllSkyWeather/Program.cs` (map the new endpoints)
- Test: `tests/AlpacaAllSkyWeather.Tests/Alpaca/ObservingConditionsEndpointsTests.cs`

**Interfaces:**
- Consumes: `ObservingConditionsDevice` (Task 5), `AlpacaEndpointHelpers` (Task 6).
- Produces: `AlpacaAllSkyWeather.Alpaca.ObservingConditionsEndpoints.MapObservingConditionsEndpoints(this WebApplication app)` mapping GET routes for all 13 sensor properties, GET/PUT `averageperiod`, GET `timesincelastupdate`, GET `sensordescription` under `/api/v1/observingconditions/0/`.

- [ ] **Step 1: Write the failing tests**

`tests/AlpacaAllSkyWeather.Tests/Alpaca/ObservingConditionsEndpointsTests.cs`:

```csharp
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.Tests.Alpaca;

public class ObservingConditionsEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ObservingConditionsEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.RemoveAll<IHostedService>()));
    }

    private sealed record DoubleBody(double Value, uint ClientTransactionID, uint ServerTransactionID, int ErrorNumber, string ErrorMessage);
    private sealed record StringBody(string Value, uint ClientTransactionID, uint ServerTransactionID, int ErrorNumber, string ErrorMessage);

    private void SeedState(double cloudCover = 21.4)
    {
        var state = _factory.Services.GetRequiredService<WeatherState>();
        state.Update(new WeatherJsonDto { CloudCover = cloudCover, WindSpeed = 1.8 }, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task CloudCover_returns_the_seeded_value()
    {
        SeedState(cloudCover: 33.3);
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<DoubleBody>("/api/v1/observingconditions/0/cloudcover");

        Assert.NotNull(body);
        Assert.Equal(33.3, body!.Value);
        Assert.Equal(0, body.ErrorNumber);
    }

    [Fact]
    public async Task CloudCover_returns_ValueNotSet_error_before_any_poll()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<DoubleBody>("/api/v1/observingconditions/0/cloudcover");

        Assert.NotNull(body);
        Assert.Equal(0x402, body!.ErrorNumber); // ValueNotSet
    }

    [Fact]
    public async Task SkyTemperature_returns_NotImplemented_error()
    {
        SeedState();
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<DoubleBody>("/api/v1/observingconditions/0/skytemperature");

        Assert.NotNull(body);
        Assert.Equal(0x400, body!.ErrorNumber); // NotImplemented
    }

    [Fact]
    public async Task AveragePeriod_put_zero_then_get_round_trips()
    {
        var client = _factory.CreateClient();

        var putResponse = await client.PutAsync(
            "/api/v1/observingconditions/0/averageperiod",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["AveragePeriod"] = "0" }));
        putResponse.EnsureSuccessStatusCode();

        var body = await client.GetFromJsonAsync<DoubleBody>("/api/v1/observingconditions/0/averageperiod");
        Assert.NotNull(body);
        Assert.Equal(0, body!.Value);
    }

    [Fact]
    public async Task TimeSinceLastUpdate_reports_a_small_positive_age_just_after_a_poll()
    {
        SeedState();
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<DoubleBody>(
            "/api/v1/observingconditions/0/timesincelastupdate?SensorName=CloudCover");

        Assert.NotNull(body);
        Assert.InRange(body!.Value, 0, 5);
    }

    [Fact]
    public async Task SensorDescription_returns_text_for_a_known_sensor()
    {
        SeedState();
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<StringBody>(
            "/api/v1/observingconditions/0/sensordescription?SensorName=CloudCover");

        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Value));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test --filter ObservingConditionsEndpointsTests
```

Expected: FAIL — routes return 404 (not mapped yet).

- [ ] **Step 3: Implement `ObservingConditionsEndpoints`**

`src/AlpacaAllSkyWeather/Alpaca/ObservingConditionsEndpoints.cs`:

```csharp
namespace AlpacaAllSkyWeather.Alpaca;

public static class ObservingConditionsEndpoints
{
    private const string BaseRoute = "/api/v1/observingconditions/0";

    public static void MapObservingConditionsEndpoints(this WebApplication app)
    {
        MapDouble(app, "cloudcover", d => d.CloudCover);
        MapDouble(app, "dewpoint", d => d.DewPoint);
        MapDouble(app, "humidity", d => d.Humidity);
        MapDouble(app, "pressure", d => d.Pressure);
        MapDouble(app, "rainrate", d => d.RainRate);
        MapDouble(app, "skybrightness", d => d.SkyBrightness);
        MapDouble(app, "skyquality", d => d.SkyQuality);
        MapDouble(app, "skytemperature", d => d.SkyTemperature);
        MapDouble(app, "starfwhm", d => d.StarFWHM);
        MapDouble(app, "temperature", d => d.Temperature);
        MapDouble(app, "winddirection", d => d.WindDirection);
        MapDouble(app, "windgust", d => d.WindGust);
        MapDouble(app, "windspeed", d => d.WindSpeed);

        app.MapGet($"{BaseRoute}/averageperiod", (HttpRequest r, ObservingConditionsDevice d) =>
            AlpacaEndpointHelpers.HandleDoubleAsync(r, () => d.AveragePeriod));

        app.MapPut($"{BaseRoute}/averageperiod", async (HttpRequest r, ObservingConditionsDevice d) =>
        {
            var form = await r.ReadFormAsync();
            var raw = form["AveragePeriod"].ToString();
            return await AlpacaEndpointHelpers.HandleMethodAsync(r, () =>
            {
                if (!double.TryParse(raw, out var value))
                {
                    throw new AlpacaDeviceException(ASCOM.Common.Alpaca.AlpacaErrors.InvalidValue, $"'{raw}' is not a valid AveragePeriod.");
                }
                d.AveragePeriod = value;
            });
        });

        app.MapGet($"{BaseRoute}/timesincelastupdate", (HttpRequest r, ObservingConditionsDevice d) =>
            AlpacaEndpointHelpers.HandleDoubleAsync(r, () => d.TimeSinceLastUpdate(r.Query["SensorName"].ToString())));

        app.MapGet($"{BaseRoute}/sensordescription", (HttpRequest r, ObservingConditionsDevice d) =>
            AlpacaEndpointHelpers.HandleStringAsync(r, () => d.SensorDescription(r.Query["SensorName"].ToString())));
    }

    private static void MapDouble(WebApplication app, string route, Func<ObservingConditionsDevice, double> selector)
    {
        app.MapGet($"{BaseRoute}/{route}", (HttpRequest r, ObservingConditionsDevice d) =>
            AlpacaEndpointHelpers.HandleDoubleAsync(r, () => selector(d)));
    }
}
```

- [ ] **Step 4: Map the new endpoints in `Program.cs`**

In `src/AlpacaAllSkyWeather/Program.cs`, change:

```csharp
app.MapAlpacaCommonEndpoints();
```

to:

```csharp
app.MapAlpacaCommonEndpoints();
app.MapObservingConditionsEndpoints();
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test --filter ObservingConditionsEndpointsTests
```

Expected: PASS (6 tests).

- [ ] **Step 6: Run the full test suite**

```bash
dotnet test
```

Expected: PASS, all tests green.

- [ ] **Step 7: Commit**

```bash
git add src/AlpacaAllSkyWeather/Alpaca/ObservingConditionsEndpoints.cs src/AlpacaAllSkyWeather/Program.cs tests/AlpacaAllSkyWeather.Tests/Alpaca/ObservingConditionsEndpointsTests.cs
git commit -m "Add ObservingConditions sensor endpoints (cloudcover, humidity, ...)"
```

---

### Task 8: Alpaca Management API endpoints

**Files:**
- Create: `src/AlpacaAllSkyWeather/Alpaca/ManagementEndpoints.cs`
- Modify: `src/AlpacaAllSkyWeather/Program.cs` (map the new endpoints)
- Test: `tests/AlpacaAllSkyWeather.Tests/Alpaca/ManagementEndpointsTests.cs`

**Interfaces:**
- Produces: `AlpacaAllSkyWeather.Alpaca.ManagementEndpoints.MapManagementEndpoints(this WebApplication app)` mapping `GET /management/apiversions`, `GET /management/v1/description`, `GET /management/v1/configureddevices`.

- [ ] **Step 1: Write the failing tests**

`tests/AlpacaAllSkyWeather.Tests/Alpaca/ManagementEndpointsTests.cs`:

```csharp
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace AlpacaAllSkyWeather.Tests.Alpaca;

public class ManagementEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ManagementEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services => services.RemoveAll<IHostedService>()));
    }

    private sealed record ApiVersionsBody(int[] Value);
    private sealed record DescriptionBody(DescriptionValue Value);
    private sealed record DescriptionValue(string ServerName, string Manufacturer, string ManufacturerVersion, string Location);
    private sealed record ConfiguredDevice(string DeviceName, string DeviceType, int DeviceNumber, string UniqueID);
    private sealed record ConfiguredDevicesBody(List<ConfiguredDevice> Value);

    [Fact]
    public async Task ApiVersions_reports_version_1()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<ApiVersionsBody>("/management/apiversions");

        Assert.NotNull(body);
        Assert.Contains(1, body!.Value);
    }

    [Fact]
    public async Task Description_reports_server_name()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<DescriptionBody>("/management/v1/description");

        Assert.NotNull(body);
        Assert.Equal("AlpacaAllSkyWeather", body!.Value.ServerName);
    }

    [Fact]
    public async Task ConfiguredDevices_lists_the_single_ObservingConditions_device()
    {
        var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<ConfiguredDevicesBody>("/management/v1/configureddevices");

        Assert.NotNull(body);
        var device = Assert.Single(body!.Value);
        Assert.Equal("ObservingConditions", device.DeviceType);
        Assert.Equal(0, device.DeviceNumber);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
dotnet test --filter ManagementEndpointsTests
```

Expected: FAIL — routes return 404.

- [ ] **Step 3: Implement `ManagementEndpoints`**

`src/AlpacaAllSkyWeather/Alpaca/ManagementEndpoints.cs`:

```csharp
using ASCOM.Alpaca.Discovery;

namespace AlpacaAllSkyWeather.Alpaca;

public static class ManagementEndpoints
{
    // Fixed so Alpaca clients that cache configured-device identity see a stable value across restarts.
    private const string DeviceUniqueId = "a2a99117-fe7e-4621-a88a-72e787ffee10";

    public static void MapManagementEndpoints(this WebApplication app)
    {
        app.MapGet("/management/apiversions", async (HttpRequest r) =>
        {
            var clientId = await AlpacaTransaction.GetClientTransactionIdAsync(r);
            var serverId = AlpacaTransaction.NextServerTransactionId();
            return Results.Ok(new ASCOM.Common.Alpaca.IntArray1DResponse(clientId, serverId, new[] { 1 }));
        });

        app.MapGet("/management/v1/description", async (HttpRequest r) =>
        {
            var clientId = await AlpacaTransaction.GetClientTransactionIdAsync(r);
            var serverId = AlpacaTransaction.NextServerTransactionId();
            var description = new AlpacaDeviceDescription("AlpacaAllSkyWeather", "sferlazza", "1.0", "N/A");
            return Results.Ok(new ManagementDescriptionResponse(description, clientId, serverId));
        });

        app.MapGet("/management/v1/configureddevices", async (HttpRequest r, ObservingConditionsDeviceName deviceName) =>
        {
            var clientId = await AlpacaTransaction.GetClientTransactionIdAsync(r);
            var serverId = AlpacaTransaction.NextServerTransactionId();
            var devices = new List<AlpacaConfiguredDevice>
            {
                new(deviceName.Value, ASCOM.Common.DeviceTypes.ObservingConditions.ToString(), 0, DeviceUniqueId),
            };
            return Results.Ok(new ASCOM.Alpaca.Discovery.AlpacaConfiguredDevicesResponse(clientId, serverId, devices));
        });
    }
}

public sealed record ManagementDescriptionResponse(AlpacaDeviceDescription Value, uint ClientTransactionID, uint ServerTransactionID);
```

- [ ] **Step 4: Map the new endpoints in `Program.cs`**

In `src/AlpacaAllSkyWeather/Program.cs`, change:

```csharp
app.MapAlpacaCommonEndpoints();
app.MapObservingConditionsEndpoints();
```

to:

```csharp
app.MapAlpacaCommonEndpoints();
app.MapObservingConditionsEndpoints();
app.MapManagementEndpoints();
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
dotnet test --filter ManagementEndpointsTests
```

Expected: PASS (3 tests).

- [ ] **Step 6: Run the full test suite**

```bash
dotnet test
```

Expected: PASS, all tests green.

- [ ] **Step 7: Commit**

```bash
git add src/AlpacaAllSkyWeather/Alpaca/ManagementEndpoints.cs src/AlpacaAllSkyWeather/Program.cs tests/AlpacaAllSkyWeather.Tests/Alpaca/ManagementEndpointsTests.cs
git commit -m "Add Alpaca Management API endpoints (apiversions, description, configureddevices)"
```

---

### Task 9: Composition root wiring (port, HttpClient base address, final Program.cs)

**Files:**
- Modify: `src/AlpacaAllSkyWeather/Program.cs`

**Interfaces:**
- Consumes: everything from Tasks 2-8.
- Produces: a `Program.cs` that binds Kestrel to the configured port and is ready for Task 10 to wrap with a tray host.

This task has no new automated tests (it is composition-root wiring); it is verified by manually running the app and hitting it with `curl`.

- [ ] **Step 1: Replace `Program.cs` with the final composition (minus the tray, added in Task 10)**

`src/AlpacaAllSkyWeather/Program.cs`:

```csharp
using AlpacaAllSkyWeather.Alpaca;
using AlpacaAllSkyWeather.Weather;
using Microsoft.Extensions.Options;

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
    new ObservingConditionsDeviceName(sp.GetRequiredService<IOptions<WeatherPollerOptions>>().Value.DeviceName));
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
```

- [ ] **Step 2: Run the full test suite (the port/Kestrel change must not break the in-memory test host)**

```bash
dotnet test
```

Expected: PASS, all tests green (`WebApplicationFactory` overrides the test server's own address, so `UseUrls` does not affect the tests).

- [ ] **Step 3: Manual smoke test**

```bash
dotnet run --project src/AlpacaAllSkyWeather
```

In another terminal:

```bash
curl "http://127.0.0.1:51111/api/v1/observingconditions/0/connected?ClientID=1&ClientTransactionID=1"
curl "http://127.0.0.1:51111/api/v1/observingconditions/0/cloudcover?ClientID=1&ClientTransactionID=1"
curl "http://127.0.0.1:51111/management/apiversions"
```

Expected: `connected` returns `{"Value":true,...}`. `cloudcover` returns `ErrorNumber:1026` (`0x402`, `ValueNotSet`) immediately after startup, then a real value once the first poll completes (within `PollIntervalSeconds`, default 60s — wait or temporarily lower it in `appsettings.json` to check sooner). `apiversions` returns `{"Value":[1],...}`. Stop the app with Ctrl+C when done.

- [ ] **Step 4: Commit**

```bash
git add src/AlpacaAllSkyWeather/Program.cs
git commit -m "Wire up the final composition root: configurable Kestrel port on 127.0.0.1"
```

---

### Task 10: WinForms tray host

> **As-built note:** during implementation the user asked for a proper visual
> design instead of the bare-bones version originally planned here — a custom
> tray icon and a legible, styled status window. What actually shipped (all
> under `src/AlpacaAllSkyWeather/TrayHost/`):
> - `WeatherIcons.cs` — hand-drawn flat vector icons (thermometer, droplet,
>   cloud, sun, star, gauge, wind) used on the status cards.
> - `TrayIconFactory.cs` — builds the tray/window icon (cloud + star badge)
>   as a real multi-resolution `.ico` with PNG-compressed frames (16-256px),
>   so it keeps true alpha transparency; `Bitmap.GetHicon()` was tried first
>   and rejected — it produces a hard 1-bit mask with visibly jagged edges.
> - `MetricCard.cs` — a dark-themed, owner-drawn card (icon + value + unit +
>   label) reused for the 11 non-directional metrics.
> - `CompassCard.cs` — a dedicated card for wind direction: an 8-point
>   cardinal rose (N/NE/E/SE/S/SO/O/NO) with tick marks and a needle overlay
>   rotated to the live value, added after the user asked for cardinal
>   points on the compass rather than a plain rotating arrow.
> - `StatusForm.cs` — hosts a 3x4 grid of those cards plus a header (device
>   name, a freshness dot, last-poll time), refreshing every 5s from
>   `WeatherState`.
> - `TrayApplicationContext.cs` — as originally planned, plus a "Mostra
>   stato" menu item (and tray icon double-click) that opens/focuses a
>   singleton `StatusForm`.
>
> Visual design was iterated offline first: a throwaway console harness in
> the scratchpad rendered the same drawing code to PNG files, inspected via
> the Read tool, before porting the approved look into the project — and a
> second harness rendered the *actual* `StatusForm`/`TrayIconFactory` classes
> to confirm the real app matches the mockup. Kept below is the original
> bounded scope for reference.

**Files:**
- Create: `src/AlpacaAllSkyWeather/TrayHost/TrayApplicationContext.cs`
- Modify: `src/AlpacaAllSkyWeather/Program.cs`

**Interfaces:**
- Consumes: the built `WebApplication` from Task 9's `Program.cs`.
- Produces: `AlpacaAllSkyWeather.TrayHost.TrayApplicationContext(WebApplication app, int httpPort) : ApplicationContext`, with a `NotifyIcon` exposing Start/Stop/Exit and a tooltip showing the last successful poll time.

This task is verified manually (WinForms UI); there is no automated test for the tray icon itself.

- [ ] **Step 1: Implement `TrayApplicationContext`**

`src/AlpacaAllSkyWeather/TrayHost/TrayApplicationContext.cs`:

```csharp
using AlpacaAllSkyWeather.Weather;

namespace AlpacaAllSkyWeather.TrayHost;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly WebApplication _app;
    private readonly WeatherState _state;
    private readonly NotifyIcon _notifyIcon;
    private readonly System.Windows.Forms.Timer _tooltipTimer;
    private bool _running = true;

    public TrayApplicationContext(WebApplication app, int httpPort)
    {
        _app = app;
        _state = app.Services.GetRequiredService<WeatherState>();

        var menu = new ContextMenuStrip();
        var toggleItem = new ToolStripMenuItem("Ferma");
        toggleItem.Click += async (_, _) =>
        {
            if (_running)
            {
                await _app.StopAsync();
                toggleItem.Text = "Avvia";
            }
            else
            {
                await _app.StartAsync();
                toggleItem.Text = "Ferma";
            }
            _running = !_running;
        };
        var exitItem = new ToolStripMenuItem("Esci");
        exitItem.Click += (_, _) => ExitThread();
        menu.Items.Add(toggleItem);
        menu.Items.Add(exitItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = $"AllSky Weather (porta {httpPort})",
            ContextMenuStrip = menu,
            Visible = true,
        };

        _tooltipTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _tooltipTimer.Tick += (_, _) => UpdateTooltip(httpPort);
        _tooltipTimer.Start();
        UpdateTooltip(httpPort);

        ThreadExit += async (_, _) =>
        {
            _tooltipTimer.Stop();
            _notifyIcon.Visible = false;
            await _app.StopAsync();
        };
    }

    private void UpdateTooltip(int httpPort)
    {
        var snapshot = _state.TryGetLatest();
        var lastPoll = snapshot is null
            ? "nessun poll riuscito ancora"
            : $"ultimo poll: {snapshot.PolledAtUtc.ToLocalTime():HH:mm:ss}";
        // NotifyIcon.Text has a 63-character limit.
        var text = $"AllSky Weather :{httpPort} - {lastPoll}";
        _notifyIcon.Text = text.Length > 63 ? text[..63] : text;
    }
}
```

- [ ] **Step 2: Wire the tray host into `Program.cs`**

Replace the tail of `src/AlpacaAllSkyWeather/Program.cs` (from `app.Run();` onward) with:

```csharp
await app.StartAsync();

ApplicationConfiguration.Initialize();

using var trayContext = new TrayHost.TrayApplicationContext(app, httpPort);
Application.Run(trayContext);

await app.StopAsync();

public partial class Program { }
```

`ApplicationConfiguration.Initialize()` requires `<ImplicitUsings>enable</ImplicitUsings>` (already set in Task 1) and the WinForms designer bootstrap; if the compiler cannot find it, replace that single line with the manual equivalent:

```csharp
Application.SetHighDpiMode(HighDpiMode.SystemAware);
Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
```

- [ ] **Step 3: Run the full test suite**

```bash
dotnet test
```

Expected: PASS, all tests green (the test host never runs `Program`'s top-level statements past `WebApplicationFactory`'s own entry point, so the WinForms code path is not exercised by tests).

- [ ] **Step 4: Manual verification**

```bash
dotnet run --project src/AlpacaAllSkyWeather
```

Expected: no console window stays attached in a blocking way beyond startup; an icon appears in the Windows system tray. Right-click it: "Ferma" stops the HTTP server (`curl` to any endpoint now fails to connect), clicking it again ("Avvia") restarts it (`curl` succeeds again), "Esci" closes the app and removes the tray icon.

- [ ] **Step 5: Commit**

```bash
git add src/AlpacaAllSkyWeather/TrayHost/TrayApplicationContext.cs src/AlpacaAllSkyWeather/Program.cs
git commit -m "Add system tray host wrapping the Alpaca HTTP server"
```

---

### Task 11: ConformU validation and real-client verification

**Files:** none (verification-only task).

This is the acceptance gate from the spec's Testing section. No code changes are expected unless ConformU or a real client surfaces a protocol bug — if that happens, fix it in the relevant file from Tasks 5-9 and re-run the affected task's tests before re-validating.

> **Result (2026-09-08):** ConformU v4.5.0 run against the real driver (Windows
> 11, actual internet-backed data from frankAllSkyCam) surfaced 4 real issues
> across two passes, all fixed in commits `c22664b` (and the port-only
> false-start before it):
> - `interfaceversion` claimed `2` but only the v1 (`IObservingConditions`)
>   surface was implemented — `IAscomDeviceV2`'s `Connect`/`Disconnect`/
>   `Connecting`/`DeviceState` were missing. Fixed by declaring `1`, which
>   matches what's actually built (see the Global Constraints/spec's
>   YAGNI section — async connect handling was never in scope for an
>   always-on passive bridge).
> - `PUT .../refresh` was never mapped (`ObservingConditionsDevice.Refresh()`
>   existed but nothing routed to it) → 404.
> - `TimeSinceLastUpdate` ignored `sensorName` entirely, so it silently
>   "succeeded" even for `SkyTemperature`/`StarFWHM`, violating the spec's
>   requirement that value/description/time-since-update be all implemented
>   or all not. Now mirrors `SensorDescription`'s validation.
> - `Connected` GET always returned `true` and PUT was a no-op — fixed with
>   real mutable per-device state.
>
> Final run: **"Congratulations, no errors, warnings or issues found: your
> driver passes ASCOM validation!!"**, all members within FAST target
> response times. Real-client verification (N.I.N.A./SharpCap) is deferred:
> this dev machine doesn't have either installed — do that step when the
> driver runs on the actual acquisition PC.

- [ ] **Step 1: Install ConformU**

Download the latest **ConformU** (ASCOM Conformance Checker, cross-platform CLI) from the ASCOM Initiative's GitHub releases: https://github.com/ASCOM-Initiative/ConformU/releases — grab the Windows x64 zip, extract it anywhere (e.g. `C:\Tools\ConformU`).

- [ ] **Step 2: Run the driver and point ConformU at it**

```bash
dotnet run --project src/AlpacaAllSkyWeather
```

In ConformU, choose "Alpaca" as the connection type, enter `127.0.0.1` and port `51111`, device type `ObservingConditions`, device number `0`, then run the full conformance check.

- [ ] **Step 3: Fix any reported issues**

If ConformU reports a protocol deviation, note the exact check that failed and fix it in the corresponding file (most likely `AlpacaEndpointHelpers.cs`, `AlpacaCommonEndpoints.cs`, or `ObservingConditionsEndpoints.cs`), re-run that task's unit/integration tests, then re-run ConformU until it reports zero errors.

- [ ] **Step 4: Connect a real Alpaca client**

In N.I.N.A. (or SharpCap), add a Weather/Observing Conditions device of type "ASCOM Alpaca", pointed at `127.0.0.1:51111`, device `ObservingConditions` #0. Confirm CloudCover/Temperature/Humidity/Wind values appear and update roughly once a minute.

- [ ] **Step 5: Commit any fixes made in Step 3**

```bash
git add -A
git commit -m "Fix Alpaca protocol issues found by ConformU"
```

(Skip this step if no fixes were needed.)
