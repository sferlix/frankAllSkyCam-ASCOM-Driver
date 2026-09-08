# AlpacaAllSkyWeather — Design Spec

Data: 2026-09-08

## Contesto

L'utente gestisce una AllSkyCam (frankAllSkyCam) che pubblica dati meteo/cielo
come JSON su `https://www.meteobrallo.com/webcam/allsky/weather.json`.
Obiettivo: rendere questi dati disponibili a software di acquisizione
astronomica compatibili ASCOM Alpaca (N.I.N.A., SharpCap) come sorgente
"Observing Conditions" (meteo/qualità del cielo), in sola lettura.

Il driver gira sullo stesso PC Windows dove girano N.I.N.A/SharpCap (non sulla
stessa macchina della AllSkyCam). Niente discovery automatica: il client verrà
configurato manualmente su `127.0.0.1:<porta>`.

## Formato JSON sorgente (osservato l'8/9/2026)

```json
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
```

Nota: `SkyQuality` risulta spesso `0` di giorno (il sensore SQM non ha
lettura significativa in pieno sole) — il driver lo espone comunque
as-is; sarà l'utente finale (N.I.N.A./SharpCap) a interpretarlo.

## Mappatura sui campi standard Alpaca `ObservingConditions`

| Proprietà Alpaca standard | Campo JSON        | Note |
|---|---|---|
| `CloudCover` (%)          | `CloudCover`      | diretto |
| `DewPoint` (°C)           | `DewPoint`        | diretto |
| `Humidity` (%)            | `Humidity`        | diretto |
| `Pressure` (hPa)          | `Pressure`        | pressione locale (non SeaLevel) |
| `RainRate` (mm/h)         | `RainRate`        | diretto |
| `SkyBrightness` (Lux)     | `SkyBrightness`   | diretto |
| `SkyQuality` (mag/arcsec²)| `SkyQuality`      | diretto |
| `Temperature` (°C)        | `Temperature`     | diretto |
| `WindDirection` (°)       | `WindDirection`   | diretto |
| `WindGust` (m/s)          | `WindGust`        | diretto — confermato dall'utente: la sorgente scrive già m/s |
| `WindSpeed` (m/s)         | `WindSpeed`       | diretto — confermato dall'utente: la sorgente scrive già m/s |
| `SkyTemperature`          | *(assente)*       | throw `MethodNotImplementedException` |
| `StarFWHM`                | *(assente)*       | throw `MethodNotImplementedException` |
| `AveragePeriod`           | *(fisso a 0)*     | nessuna media storica supportata; `set` accetta solo `0`, altrimenti `InvalidValueException` |
| `TimeSinceLastUpdate(sensorName)` | calcolato | `now - timestamp` dell'ultimo poll riuscito |

Campo extra non standard `StarCount`: esposto tramite il meccanismo Alpaca
"Custom Actions" (`PUT /api/v1/observingconditions/0/action` con
`Action=starcount`), che ritorna il valore come stringa.

`timestamp`/`timestamp_local`/`NightStart`/`NightEnd`/`SeaLevelPressure`/
`UVIndex` non sono usati dal driver in questa versione (YAGNI — nessun
client Alpaca standard li richiede).

## Architettura

Progetto .NET 8, applicazione console con:

0. **Dipendenza NuGet `ASCOM.Common.Components` (v4.0.0)**: libreria ufficiale
   ASCOM Initiative, multi-target `net8.0`/`net9.0`/`net10.0`, senza alcuna
   dipendenza dalla piattaforma COM ASCOM. Verificata per ispezione diretta
   dell'assembly (`ASCOM.Common.dll`). Fornisce già, pronti all'uso:
   - `ASCOM.Common.Alpaca.AlpacaErrors` (enum con i codici di errore Alpaca
     corretti: `NotImplemented = 0x400`, `InvalidValue = 0x401`,
     `ValueNotSet = 0x402`, `NotConnected = 0x407`, ecc.)
   - Le classi busta-risposta `DoubleResponse`, `StringResponse`,
     `BoolResponse`, `MethodResponse`, `ErrorResponse` (tutte derivano da
     `Response`, che espone `ClientTransactionID`, `ServerTransactionID`,
     `ErrorNumber`, `ErrorMessage` — esattamente la busta JSON richiesta dal
     protocollo Alpaca, verificato che serializzano in PascalCase senza
     `JsonPropertyName` custom)
   - `ASCOM.Common.DeviceInterfaces.IObservingConditions` come riferimento
     per i nomi esatti delle proprietà/metodi da implementare

   Non si usa `ASCOM.Alpaca.Device` (contiene solo il responder di discovery
   UDP, non necessario) né il template `ASCOM.Alpaca.Templates` (in stato
   alpha, basato su Blazor, sovradimensionato per un device di sola
   lettura).

1. **`WeatherPollerService`** (`BackgroundService`): ogni 60s (configurabile)
   esegue `HttpClient.GetAsync` sull'URL sorgente, deserializza con
   `System.Text.Json`, aggiorna uno stato condiviso (`WeatherState`, oggetto
   thread-safe con `lock` interno) contenente gli ultimi valori e
   `DateTimeOffset LastSuccessfulPollUtc`. In caso di errore di rete/parsing,
   logga e riprova al giro successivo senza aggiornare `LastSuccessfulPollUtc`.
   La logica di singolo poll è isolata in un metodo testabile
   (`PollOnceAsync`), separato dal loop temporizzato, così da poterla
   testare senza attese reali.

2. **`ObservingConditionsDevice`**: implementa la stessa superficie di
   `IObservingConditions` leggendo da `WeatherState`.
   - Se non è mai arrivato **nessun** poll riuscito da quando il driver è
     partito, le proprietà restituiscono errore Alpaca `ValueNotSet`
     (`AlpacaErrors.ValueNotSet`) — dato mai disponibile.
   - Se un poll è arrivato ma è "vecchio" (oltre `StalenessThreshold`,
     default 10 minuti), il driver **continua a restituire l'ultimo valore
     noto** (niente eccezione, niente `Connected=false` fittizio): è compito
     di `TimeSinceLastUpdate(sensorName)` comunicare onestamente quanto è
     vecchio il dato. Questo evita di dichiarare un device "connesso" ma poi
     rifiutare ogni lettura, comportamento che i tool di conformità Alpaca
     (ConformU) segnalerebbero come inconsistente.
   - `SkyQuality` viene passato as-is anche quando vale `0` (es. lettura
     diurna) — nessuna interpretazione lato driver, come da conferma
     dell'utente.

3. **Host ASP.NET Core (Kestrel)**: espone le route REST Alpaca standard
   sotto `/api/v1/observingconditions/0/...` per il device sopra, più i
   Common Method (`connected`, `description`, `driverinfo`, `driverversion`,
   `interfaceversion`, `name`, `supportedactions`, `action`) e la
   Management API minima richiesta dal protocollo Alpaca:
   - `GET /management/apiversions`
   - `GET /management/v1/description`
   - `GET /management/v1/configureddevices`

   Nessun responder UDP di discovery (porta 32227) in questa versione.

4. **Tray host**: un piccolo `NotifyIcon` (Windows Forms) che avvolge
   l'host ASP.NET Core, con menu Start/Stop/Esci e stato ultimo poll
   visibile nel tooltip. L'app resta in primo piano/tray, non è un servizio
   Windows. Per non bloccare il message loop di WinForms, l'avvio è
   `await host.StartAsync()` seguito da `Application.Run(trayContext)` — 
   **non** `host.Run()`, che bloccherebbe il thread e impedirebbe alla tray
   icon di rispondere ai click.

5. **Configurazione** (`appsettings.json`):
   ```json
   {
     "AllSkyWeather": {
       "SourceUrl": "https://www.meteobrallo.com/webcam/allsky/weather.json",
       "PollIntervalSeconds": 60,
       "StalenessThresholdMinutes": 10,
       "HttpPort": 51111,
       "DeviceName": "AllSky Weather"
     }
   }
   ```
   Porta di default `51111` scelta deliberatamente diversa da `11111`
   (default storico di ASCOM Remote Server) per evitare conflitti se in
   futuro sulla stessa macchina girasse anche quello.

## Struttura progetto

```
AlpacaAllSkyWeather/
  AlpacaAllSkyWeather.sln
  src/
    AlpacaAllSkyWeather/
      AlpacaAllSkyWeather.csproj
      Program.cs
      appsettings.json
      Weather/
        WeatherState.cs
        WeatherPollerService.cs
        WeatherJsonDto.cs
      Alpaca/
        ObservingConditionsDevice.cs
        AlpacaEndpoints.cs        (mapping delle route Common + device-specific)
        ManagementEndpoints.cs
      TrayHost/
        TrayApplicationContext.cs
  docs/
    superpowers/specs/2026-09-08-alpaca-allsky-weather-design.md
```

## Error handling

- Errori di rete/parsing sul poll: loggati, non propagati al client Alpaca
  finché lo stato non è "mai arrivato un poll riuscito" (vedi sopra).
- Dato mai arrivato (nessun poll riuscito dall'avvio): `AlpacaErrors.ValueNotSet`
  (`0x402`).
- Richieste Alpaca a `ClientTransactionID`/`ClientID` mancanti o
  malformate: risposta con `ErrorNumber`/`ErrorMessage` secondo lo spec
  Alpaca (HTTP 400).
- Proprietà non supportate (`SkyTemperature`, `StarFWHM`): `AlpacaErrors.NotImplemented`
  (`0x400`).

## Testing

1. Unit test su `WeatherPollerService`/parsing JSON con dati di esempio
   (incluso il file reale salvato come fixture).
2. Test manuale con `curl` sugli endpoint REST (`connected`, `cloudcover`,
   `humidity`, ecc.).
3. Validazione con **ConformU** (tool ufficiale ASCOM Conformance Checker)
   contro il device `ObservingConditions` esposto.
4. Verifica end-to-end collegando N.I.N.A o SharpCap come sorgente
   Weather/Observing Conditions puntata su `127.0.0.1:51111`.

## Fuori scope (YAGNI)

- Discovery UDP automatica.
- Esecuzione come Windows Service.
- Supporto multi-camera/multi-sorgente.
- Storico/medie (`AveragePeriod` > 0).
- Autenticazione sulle API Alpaca (non richiesta dal protocollo per uso
  locale).
