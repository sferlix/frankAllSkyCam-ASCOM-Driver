# Watchdog for the AllSky Weather tray app. Pings its own local Alpaca management endpoint and,
# if it's unreachable or unresponsive, kills and relaunches the exe. Run periodically by a
# per-user Scheduled Task created at install time (see AlpacaAllSkyWeather.iss, task "watchdog")
# - not meant to be run directly. Lives next to AlpacaAllSkyWeather.exe in the install folder.
#
# Note: this does not distinguish "crashed" from "you chose Exit from the tray menu" - either
# way, it will be relaunched on the next check (every 5 minutes by default). Disable the
# Scheduled Task (Task Scheduler > "<app name> Watchdog") before intentionally leaving the app
# stopped for a while, e.g. during a manual upgrade.

$ErrorActionPreference = 'Stop'
$appDir = $PSScriptRoot
$exePath = Join-Path $appDir 'AlpacaAllSkyWeather.exe'
$settingsPath = Join-Path $appDir 'appsettings.json'
$logPath = Join-Path $appDir 'watchdog.log'

function Write-Log([string]$message) {
    $line = '{0:yyyy-MM-dd HH:mm:ss} {1}' -f (Get-Date), $message
    Add-Content -Path $logPath -Value $line
    # Keep the log from growing without bound - cap it at the most recent 500 lines.
    $lines = @(Get-Content -Path $logPath -ErrorAction SilentlyContinue)
    if ($lines.Count -gt 500) {
        $lines[-500..-1] | Set-Content -Path $logPath
    }
}

$port = 51111
if (Test-Path $settingsPath) {
    try {
        $config = Get-Content -Path $settingsPath -Raw | ConvertFrom-Json
        if ($config.AllSkyWeather.HttpPort) {
            $port = [int]$config.AllSkyWeather.HttpPort
        }
    } catch {
        # Malformed/unreadable settings file: fall back to the default port rather than aborting
        # the whole check - a bad JSON edit shouldn't disable the watchdog too.
    }
}

$alive = $false
try {
    $response = Invoke-WebRequest -Uri "http://127.0.0.1:$port/management/apiversions" -UseBasicParsing -TimeoutSec 5
    $alive = $response.StatusCode -ge 200 -and $response.StatusCode -lt 500
} catch {
    $alive = $false
}

if ($alive) {
    exit 0
}

Write-Log "App unresponsive on port $port - restarting."

Get-Process -Name 'AlpacaAllSkyWeather' -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -eq $exePath } |
    Stop-Process -Force -ErrorAction SilentlyContinue

Start-Sleep -Seconds 2
Start-Process -FilePath $exePath -WorkingDirectory $appDir
Write-Log 'Restart issued.'
