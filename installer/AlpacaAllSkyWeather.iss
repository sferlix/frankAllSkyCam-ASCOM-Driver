; Inno Setup script for the frankAllSkyCam ASCOM Alpaca driver.
; Build with: dotnet publish (self-contained win-x64) into ..\publish\win-x64, then compile this
; script with ISCC.exe (Inno Setup Compiler). See docs/superpowers/plans for the full build steps.

#define MyAppName "frankAllSkyCam ASCOM Driver"
#define MyAppVersion "1.2.0"
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
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "autostart"; Description: "Avvia {#MyAppName} all'avvio di Windows"; GroupDescription: "Attività aggiuntive:"

[Files]
; appsettings.json is excluded from the bulk copy and installed separately with
; onlyifdoesntexist: an upgrade must never overwrite the user's saved thresholds and
; Telegram credentials (entered via the tray Settings UI, written back into this file).
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Excludes: "appsettings.json"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\publish\win-x64\appsettings.json"; DestDir: "{app}"; Flags: onlyifdoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Disinstalla {#MyAppName}"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Avvia {#MyAppName} ora"; Flags: nowait postinstall skipifsilent
