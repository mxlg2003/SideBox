#define MyAppName "SideBox"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "SideBox"
#define MyAppExeName "SideBox.exe"
#define MyPublishDir "..\src\WinDock.App\bin\Release\net10.0-windows10.0.22621.0\win-x64\publish"
#define MySetupIcon "..\src\WinDock.App\Assets\WinDock.ico"

; SignTool: when you have an Authenticode certificate, add a SignTool entry in
; Inno Setup Compiler (Tools → Configure Sign Tools), for example:
;   Name: signtool
;   Command: signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a $f
; Then uncomment:
;   SignTool=signtool
; Do not check certificates into this repo.

[Setup]
AppId={{3F8C1D2A-7B44-4E91-9C6A-2D5F8B1E4A70}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=SideBox-setup
SetupIconFile={#MySetupIcon}
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
CloseApplicationsFilter=*.exe,*.dll
RestartApplications=no

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "开机启动"; GroupDescription: "附加任务"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SideBox"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "SideBox"; Flags: uninsdeletevalue dontcreatekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Sidebox"; Flags: uninsdeletevalue dontcreatekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "WinDock"; Flags: uninsdeletevalue dontcreatekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 SideBox"; Flags: nowait postinstall skipifsilent; Check: not ShouldLaunchSilent
Filename: "{app}\{#MyAppExeName}"; Flags: nowait; Check: ShouldLaunchSilent

; Uninstall keeps %AppData%\SideBox and %LocalAppData%\SideBox\logs.

[Code]
function ShouldLaunchSilent: Boolean;
begin
  Result := WizardSilent;
end;
