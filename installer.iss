[Setup]
AppId={{C6D2C4D3-A9CE-4CF4-84A5-E1A4D4A4B6B4}
AppName=osu!tag
AppVersion={#AppVersion}
AppPublisher=kaancat
DefaultDirName={localappdata}\osu!tag
DefaultGroupName=osu!tag
AllowNoIcons=yes
OutputDir=.
OutputBaseFilename=osu-tag-v{#AppVersion}-win-setup
SetupIconFile=src\osu!tag\Assets\app.ico
Compression=lzma
SolidCompression=yes
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "publish\osu!tag.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\osu!tag"; Filename: "{app}\osu!tag.exe"
Name: "{commondesktop}\osu!tag"; Filename: "{app}\osu!tag.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\osu!tag.exe"; Description: "{cm:LaunchProgram,osu!tag}"; Flags: nowait postinstall skipifsilent
