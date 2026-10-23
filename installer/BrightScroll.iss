#define AppName "BrightScroll"
#ifndef AppVersion
  #error "AppVersion must be supplied by the release tag or command line"
#endif
#define AppPublisher "Madguts"
#define AppExeName "BrightScroll.exe"

[Setup]
AppId={{7A0892B8-60FB-4D46-9013-01033033839A}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\BrightScroll
DefaultGroupName={#AppName}
OutputDir=..\artifacts
OutputBaseFilename=BrightScroll-Setup
ArchitecturesInstallIn64BitMode=x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\{#AppExeName}

[Tasks]
Name: "startup"; Description: "Start BrightScroll when I sign in"; GroupDescription: "Additional options:"

[Files]
Source: "..\BrightScroll\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\BrightScroll"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BrightScroll"; ValueData: """{app}\{#AppExeName}"""; Tasks: startup; Flags: uninsdeletevalue
