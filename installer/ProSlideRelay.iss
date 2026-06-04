; ProSlideRelay Installer
; Requires Inno Setup 6 — https://jrsoftware.org/isdl.php
;
; Build via:  installer\build.ps1
; Or manually: ISCC.exe /DAppVersion=1.0.0 installer\ProSlideRelay.iss

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName      "ProSlideRelay"
#define AppExe       "ProSlideRelay.Tray.exe"
#define AppPublisher "ProSlideRelay"
; Keep this GUID stable — Inno Setup uses it to recognise upgrades
#define AppId        "{{F3A1B2C4-D5E6-4890-BCDE-F01234567891}"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=ProSlideRelay-{#AppVersion}-Setup
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
; Require admin so we can install to Program Files and add firewall rule
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
; Restart the app after install if it was running
CloseApplications=yes
CloseApplicationsFilter=*.exe
RestartApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; \
  Description: "Create a &desktop shortcut"; \
  GroupDescription: "Additional icons:"; \
  Flags: unchecked
Name: "autostart"; \
  Description: "Start ProSlideRelay &automatically when Windows starts"; \
  GroupDescription: "Startup:"; \
  Flags: unchecked

[Files]
Source: "..\publish\windows\{#AppExe}"; \
  DestDir: "{app}"; \
  Flags: ignoreversion

[Icons]
; Start Menu
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
; Desktop (optional task)
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
; Startup folder (optional task)
Name: "{userstartup}\{#AppName}";  Filename: "{app}\{#AppExe}"; Tasks: autostart

[Run]
; Add private-network firewall rule so phones on Wi-Fi can connect
Filename: "netsh"; \
  Parameters: "advfirewall firewall add rule name=""{#AppName}"" dir=in action=allow program=""{app}\{#AppExe}"" enable=yes profile=private,domain"; \
  Flags: runhidden; \
  StatusMsg: "Configuring firewall rule..."

; Offer to launch after install
Filename: "{app}\{#AppExe}"; \
  Description: "Launch {#AppName} now"; \
  Flags: nowait postinstall skipifsilent

[UninstallRun]
; Remove firewall rule on uninstall
Filename: "netsh"; \
  Parameters: "advfirewall firewall delete rule name=""{#AppName}"""; \
  Flags: runhidden

[Code]

// Terminate a running instance before upgrading or uninstalling
procedure KillRunningInstance();
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('taskkill'), '/F /IM {#AppExe}', '', SW_HIDE,
       ewWaitUntilTerminated, ResultCode);
  Sleep(500);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    KillRunningInstance();
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    KillRunningInstance();
end;
