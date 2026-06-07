; GTA5 Optimizer Installer Script
; Requires Inno Setup 6+

#define MyAppName "GTA5 Optimizer"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "GTA5Optimizer Team"
#define MyAppURL "https://github.com/gta5optimizer"

[Setup]
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={pf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=..\installer
OutputBaseFilename=GTA5Optimizer_Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverrides=dialog
SetupLogging=true

[Languages]
English.name=English
Russian.name=Russian

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\GTA5Optimizer.UI.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\appsettings.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\publish\Styles\Themes.xaml"; DestDir: "{app}\Styles"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\ARCHITECTURE.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\GTA5Optimizer.UI.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\GTA5Optimizer.UI.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\GTA5Optimizer.UI.exe"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
begin
    // Check for .NET 8 Desktop Runtime
    Result := True;
end;

[Messages]
CreateDesktopIcon=Create desktop icon
LaunchProgram=Launch %1 now
AdditionalIcons=Additional icons