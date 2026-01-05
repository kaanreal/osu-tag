#define MyAppName "osu!tag"
#define MyAppPublisher "osu!tag"
#ifndef MyAppVersion
#define MyAppVersion "0.0.0"
#endif

#ifndef WizardBmpPath
#define WizardBmpPath ""
#endif

#ifndef SetupIconPath
#define SetupIconPath ""
#endif

; Use /DSourcePath="..." and /DMyAppVersion=1.2.3 when compiling with ISCC

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
VersionInfoVersion={#MyAppVersion}.0
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputBaseFilename=OsuTag-Setup-{#MyAppVersion}
; Use the app icon from the publish folder as the Setup program icon
SetupIconFile={#SourcePath}\\app.ico
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Setup]
; Optional wizard images (can be passed via WizardBmpPath)
#if FileExists('{#WizardBmpPath}')
WizardImageFile={#WizardBmpPath}
WizardSmallImageFile={#WizardBmpPath}
#endif

; Fallback to using app.ico from the publish folder or the explicit SetupIconPath
#if FileExists('{#SetupIconPath}')
SetupIconFile={#SetupIconPath}
#else
#if FileExists('{#SourcePath}\\app.ico')
SetupIconFile={#SourcePath}\\app.ico
#endif
#endif

[Files]
; Exclude README/LICENSE, app.ico and wizard image from the general payload (they are kept in the publish ZIP but not installed into {app})
Source: "{#SourcePath}\*"; Excludes: "README.md;LICENSE;installer_wizard.bmp;app.ico"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; MinVersion: 6.1
; If you want to include docs, add a separate entry (e.g., install to {app}\Docs)
; app.ico is referenced by SetupIconFile but not installed into the application folder

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\OsuTag.exe"; WorkingDir: "{app}"; IconFilename: "{app}\app.ico"

[Run]
Filename: "{app}\OsuTag.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
// no custom script
