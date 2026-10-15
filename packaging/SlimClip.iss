; Build via packaging/build-exe.ps1 (supplies StageDir and AppVersion).
; Classic Explorer verb only: Windows 11 exposes it under "Show more options".
; No MSIX, certificate installation, regsvr32, FFmpeg or machine-wide registry writes.
#ifndef StageDir
  #error StageDir must be supplied by build-exe.ps1
#endif
#ifndef AppVersion
  #error AppVersion must be supplied by build-exe.ps1
#endif
#ifndef LogoFile
  #error LogoFile must be supplied by build-exe.ps1
#endif

[Setup]
AppId={{266761BB-3916-45F4-9E9F-6E880324331E}
AppName=SlimClip
AppVersion={#AppVersion}
AppPublisher=SlimClip
DefaultDirName={localappdata}\Programs\SlimClip
DefaultGroupName=SlimClip
OutputBaseFilename=SlimClip-Setup
SetupIconFile={#LogoFile}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
UninstallDisplayIcon={app}\SlimClip.exe
UninstallDisplayName=SlimClip
CloseApplications=yes
RestartApplications=no
ChangesAssociations=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[CustomMessages]
english.DesktopShortcut=Create a desktop shortcut
chinesesimplified.DesktopShortcut=创建桌面快捷方式

[Files]
Source: "{#StageDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "ffmpeg.exe,ffprobe.exe,SlimClipCommandSelfTest.exe"

[Icons]
Name: "{group}\SlimClip"; Filename: "{app}\SlimClip.exe"
Name: "{userdesktop}\SlimClip"; Filename: "{app}\SlimClip.exe"; Tasks: desktopicon

[Tasks]
Name: desktopicon; Description: "{cm:DesktopShortcut}"; Flags: unchecked

[Registry]
; HKCU\Software\Classes is the per-user part of HKCR. Only these SlimClip-owned
; keys are removed on uninstall: never delete sibling verbs, user settings or tools.
; Inno's {{ escapes the literal opening brace in a GUID-containing registry path.
Root: HKCU; Subkey: "Software\Classes\CLSID\{{83e9cd33-c9e0-44de-b265-f18f91a9a929}"; ValueType: string; ValueName: ""; ValueData: "SlimClip Explorer command"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\CLSID\{{83e9cd33-c9e0-44de-b265-f18f91a9a929}\InprocServer32"; ValueType: string; ValueName: ""; ValueData: "{app}\SlimClipCommand.dll"
Root: HKCU; Subkey: "Software\Classes\CLSID\{{83e9cd33-c9e0-44de-b265-f18f91a9a929}\InprocServer32"; ValueType: string; ValueName: "ThreadingModel"; ValueData: "Apartment"
; ExplorerCommandHandler.GetTitle localizes the menu for the Windows UI language.
; Standard ExplorerCommandHandler verb: unlike a static command, Explorer passes
; the full IShellItemArray to the COM implementation, including multi-selection.
Root: HKCU; Subkey: "Software\Classes\*\shell\SlimClip.Compress"; ValueType: string; ValueName: ""; ValueData: "Compress Video"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\*\shell\SlimClip.Compress"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\SlimClip.exe,0"
Root: HKCU; Subkey: "Software\Classes\*\shell\SlimClip.Compress"; ValueType: string; ValueName: "ExplorerCommandHandler"; ValueData: "{{83e9cd33-c9e0-44de-b265-f18f91a9a929}"
Root: HKCU; Subkey: "Software\Classes\*\shell\SlimClip.Compress"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"
Root: HKCU; Subkey: "Software\Classes\*\shell\SlimClip.Compress"; ValueType: string; ValueName: "NeverDefault"; ValueData: ""
