#define MyAppName "WinDock"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "WinDock"
#define MyAppExeName "WinDock.exe"
#define MyPublishDir "..\src\WinDock.App\bin\Release\net10.0-windows10.0.22621.0\win-x64\publish"

; SignTool: when you have an Authenticode certificate, add a SignTool entry in
; Inno Setup Compiler (Tools → Configure Sign Tools), for example:
;   Name: signtool
;   Command: signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a $f
; Then uncomment:
;   SignTool=signtool
; Do not check certificates into this repo.

[Setup]
AppId={{8C7E2F1A-4B9D-4E11-9F3A-WINDOCK00001}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=WinDock-setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

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
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WinDock"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: startup
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "WinDock"; Flags: uninsdeletevalue dontcreatekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 WinDock"; Flags: nowait postinstall skipifsilent

; Uninstall keeps %AppData%\WinDock and %LocalAppData%\WinDock\logs.
