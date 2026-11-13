; Inno Setup script for Trimx. Build with build.ps1 (publishes first), or open this file in Inno Setup
; after running: dotnet publish src/Trimx/Trimx.csproj -p:PublishProfile=Release-win-x64
;
; The installer is per-user by default (no administrator rights needed); the user can choose
; "for all users" on the first page. The publish output already contains the .NET runtime.

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif

#define AppName "Trimx"
#define AppPublisher "shin32t"
#define AppExe "Trimx.exe"
#define PublishDir "..\src\Trimx\bin\publish\win-x64"

[Setup]
; Never change AppId: it is how upgrades find the previous install.
AppId={{6F2B1C3A-7D54-4E8B-9A1F-3C5D2E7B9A40}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppCopyright=Copyright (c) 2026 shin32t
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
LicenseFile=..\LICENSE
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=output
OutputBaseFilename=Trimx-{#AppVersion}-win-x64-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
