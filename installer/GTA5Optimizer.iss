; GTA5 Optimizer Installer Script
; Inno Setup 6.x
; Compile with: iscc GTA5Optimizer.iss

#define MyAppName "GTA5 Optimizer"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "GTA5Optimizer Team"
#define MyAppURL "https://github.com/your-repo/GTA5Optimizer"
#define MyAppExeName "GTA5Optimizer.exe"
#define SourceDir "..\GTA5Optimizer\publish"

[Setup]
AppId={{B8A3C4D5-E6F7-4890-A1B2-C3D4E5F67890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\GTA5Optimizer
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=..\LICENSE
OutputDir=..\Output
OutputBaseFilename=GTA5Optimizer-Setup-v{#MyAppVersion}
SetupIconFile=..\GTA5Optimizer\src\GTA5Optimizer.UI\Resources\app.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
MinVersion=10.0.17763
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
ShowLanguageDialog=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "{cm:AutoStartProgram,{#MyAppName}}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsDotNetInstalled(net8, 0) then begin
    MsgBox('GTA5 Optimizer requires .NET 8 Desktop Runtime.'#13#10#13#10
      'It will be downloaded and installed automatically.', mbInformation, MB_OK);
  end;
end;
