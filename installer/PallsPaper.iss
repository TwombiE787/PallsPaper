#define MyAppName "PallsPaper"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "PallsPaper"
#define MyAppExeName "PallsPaper.exe"

#ifndef PublishDir
  #error "PublishDir must point to the self-contained publish output."
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts\installer"
#endif

[Setup]
AppId={{9A8C6F2D-1B74-4DB3-91AF-733BD554C1C1}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\PallsPaper
DefaultGroupName={#MyAppName}
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no
MinVersion=10.0
WizardStyle=modern
SetupIconFile=..\Assets\PallsPaper.ico
DisableProgramGroupPage=yes
Compression=lzma2
SolidCompression=yes
OutputDir={#OutputDir}
OutputBaseFilename=PallsPaper-Setup-{#MyAppVersion}-x64
SetupLogging=yes

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "Desktopverknüpfung erstellen"; GroupDescription: "Zusätzliche Verknüpfungen:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "MicrosoftEdgeWebView2Setup.exe"; Flags: dontcopy

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{#MyAppName} starten"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
const
  WebView2RuntimeKey = 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';

function IsWebView2Installed: Boolean;
var
  RuntimeVersion: String;
begin
  Result :=
    RegQueryStringValue(HKLM64, WebView2RuntimeKey, 'pv', RuntimeVersion) or
    RegQueryStringValue(HKLM32, WebView2RuntimeKey, 'pv', RuntimeVersion) or
    RegQueryStringValue(HKCU, WebView2RuntimeKey, 'pv', RuntimeVersion);
  Result := Result and (RuntimeVersion <> '') and (RuntimeVersion <> '0.0.0.0');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  InstallerResult: Integer;
begin
  Result := '';
  if IsWebView2Installed then
    Exit;

  ExtractTemporaryFile('MicrosoftEdgeWebView2Setup.exe');
  if not Exec(
    ExpandConstant('{tmp}\MicrosoftEdgeWebView2Setup.exe'),
    '/silent /install',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    InstallerResult) then
  begin
    Result := 'Der Microsoft-WebView2-Installer konnte nicht gestartet werden.';
    Exit;
  end;

  if (InstallerResult <> 0) and (InstallerResult <> 3010) then
    Result := 'Die Microsoft Edge WebView2 Runtime konnte nicht installiert werden. Installer-Code: ' +
      IntToStr(InstallerResult) + '.';

  NeedsRestart := InstallerResult = 3010;
end;
