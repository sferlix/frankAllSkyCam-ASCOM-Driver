; Inno Setup script for the frankAllSkyCam ASCOM Alpaca driver.
; Build with: dotnet publish (self-contained win-x64) into ..\publish\win-x64, then compile this
; script with ISCC.exe (Inno Setup Compiler). See docs/superpowers/plans for the full build steps.

#define MyAppName "frankAllSkyCam ASCOM Driver"
#define MyAppVersion "1.3.3"
#define MyAppPublisher "sferlazza"
#define MyAppExeName "AlpacaAllSkyWeather.exe"

[Setup]
AppId={{B4049B49-2986-4248-884C-CF2B75475AE4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
; Per-user install (no admin/UAC prompt needed), matching the per-user HKCU
; autostart entry below - a machine-wide install under Program Files would
; need admin rights while still only affecting the installing user's autostart.
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=frankAllSkyCam-ASCOM-Driver-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "Launch {#MyAppName} at Windows startup"; GroupDescription: "Additional tasks:"
Name: "watchdog"; Description: "Automatically restart {#MyAppName} if it becomes unresponsive"; GroupDescription: "Additional tasks:"

[Files]
; appsettings.json is excluded from the bulk copy and installed separately with
; onlyifdoesntexist: an upgrade must never overwrite the user's saved thresholds and
; Telegram credentials (entered via the tray Settings UI, written back into this file).
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Excludes: "appsettings.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\publish\win-x64\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "Watchdog.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "Watchdog.cmd"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
; Runs only when the "watchdog" task is checked; /F overwrites a stale task left by a previous
; install (e.g. a reinstall or repair). No elevation needed: a per-user task that runs only while
; logged on (no /RU) matches the app's own per-user, no-admin-prompt install model.
Filename: "{sys}\schtasks.exe"; Parameters: "/Create /TN ""{#MyAppName} Watchdog"" /SC MINUTE /MO 5 /TR ""{app}\Watchdog.cmd"" /F"; Flags: runhidden; Tasks: watchdog
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName} now"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Unconditional (not gated on the task selection) so a leftover scheduled task never survives an
; uninstall even if watchdog was enabled after this install, then later uninstalled with the
; checkbox unticked (tasks aren't re-evaluated during uninstall). Deleting a task that doesn't
; exist just fails quietly here - runhidden, and uninstall doesn't require this step to succeed.
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN ""{#MyAppName} Watchdog"" /F"; Flags: runhidden; RunOnceId: "DeleteWatchdogTask"
