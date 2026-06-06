; ──────────────────────────────────────────────────────────────────────
; PromptResponse — Windows installer (Inno Setup)
;
; Build on a Windows machine (or CI windows runner) after publishing:
;   dotnet publish src/PromptResponse.Desktop -c Release -r win-x64 ^
;     --self-contained true -p:PublishSingleFile=true ^
;     -p:IncludeNativeLibrariesForSelfExtract=true -o publish\win-x64
;   dotnet publish src/PromptResponse.Cli -c Release -r win-x64 ^
;     --self-contained true -p:PublishSingleFile=true -o publish\win-x64
;   iscc /DMyAppVersion=0.3.0 /DPublishDir=publish\win-x64 /DRepoRoot=. ^
;        packaging\windows\promptresponse.iss
;
; Self-contained — no .NET runtime required on the target machine.
; Registers .apr / .aprt / .aprf file associations.
; ──────────────────────────────────────────────────────────────────────

#ifndef MyAppVersion
#define MyAppVersion "0.0.0"
#endif
#ifndef PublishDir
#error "PublishDir must be set with /DPublishDir=path-to-publish-output"
#endif
#ifndef RepoRoot
#error "RepoRoot must be set with /DRepoRoot=path-to-repo-root"
#endif

#define MyAppName       "PromptResponse"
#define MyAppPublisher  "Marc Jones"
#define MyAppURL        "https://github.com/marctjones/promptresponse"
#define MyAppExeName    "promptresponse.exe"
#define MyAppCliExeName "apr.exe"
#define MyAppId         "{{B2E9B6A1-7C3D-4F2A-9E1B-3A6C2D5E8F40}}"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile={#RepoRoot}\LICENSE
OutputBaseFilename=promptresponse-{#MyAppVersion}-win-x64-setup
SetupIconFile={#RepoRoot}\src\PromptResponse.Desktop\Assets\app-icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
ChangesAssociations=yes

[Files]
Source: "{#PublishDir}\{#MyAppExeName}";    DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\{#MyAppCliExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#RepoRoot}\LICENSE";              DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}";    Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked
Name: "associate";   Description: "Associate .apr, .aprt and .aprf files with PromptResponse"; GroupDescription: "File associations:"

[Registry]
; ProgID
Root: HKA; Subkey: "Software\Classes\PromptResponse.Document"; ValueType: string; ValueName: ""; ValueData: "PromptResponse Form"; Flags: uninsdeletekey; Tasks: associate
Root: HKA; Subkey: "Software\Classes\PromptResponse.Document\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: associate
Root: HKA; Subkey: "Software\Classes\PromptResponse.Document\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: associate
; Extensions → ProgID
Root: HKA; Subkey: "Software\Classes\.apr";  ValueType: string; ValueName: ""; ValueData: "PromptResponse.Document"; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Classes\.aprt"; ValueType: string; ValueName: ""; ValueData: "PromptResponse.Document"; Flags: uninsdeletevalue; Tasks: associate
Root: HKA; Subkey: "Software\Classes\.aprf"; ValueType: string; ValueName: ""; ValueData: "PromptResponse.Document"; Flags: uninsdeletevalue; Tasks: associate

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch PromptResponse"; Flags: nowait postinstall skipifsilent
