# Install and uninstall the per-user unsigned EXE on a disposable GitHub Windows runner.
# Never run on a user's workstation: it inspects/removes this user's SlimClip registration.
[CmdletBinding()]
param([Parameter(Mandatory)][string] $InstallerPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'EXE install smoke is restricted to disposable GitHub Windows runners.'
}
$setup = (Resolve-Path -LiteralPath $InstallerPath -ErrorAction Stop).ProviderPath
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\SlimClip'
$verb = 'Registry::HKEY_CURRENT_USER\Software\Classes\*\shell\SlimClip.Compress'
$com = 'Registry::HKEY_CURRENT_USER\Software\Classes\CLSID\{83e9cd33-c9e0-44de-b265-f18f91a9a929}\InprocServer32'
$guid = '{83e9cd33-c9e0-44de-b265-f18f91a9a929}'
$uninstallRoot = 'Registry::HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall'
function Find-UninstallEntries {
    @(Get-ChildItem -LiteralPath $uninstallRoot -ErrorAction Stop | Where-Object { $_.GetValue('DisplayName') -eq 'SlimClip' })
}
if (@(Find-UninstallEntries).Count -ne 0) { throw 'Runner already has a SlimClip uninstall entry.' }
if ((Test-Path -LiteralPath $installDir) -or (Test-Path -LiteralPath $verb) -or (Test-Path -LiteralPath $com)) {
    throw 'Runner already contains SlimClip installation/registration; refusing to overwrite.'
}
$uninstaller = Join-Path $installDir 'unins000.exe'
try {
    $installer = Start-Process -FilePath $setup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-') -PassThru -Wait
    if ($installer.ExitCode -ne 0) { throw "Inno Setup failed with exit code $($installer.ExitCode)" }
    foreach ($name in @('SlimClip.exe','SlimClipCommand.dll','LICENSE.txt','unins000.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $installDir $name) -PathType Leaf)) { throw "Installed file missing: $name" }
    }
    if (@(Find-UninstallEntries).Count -ne 1) { throw 'Settings app uninstall registration missing or duplicated.' }
    $included = @(Get-ChildItem -LiteralPath $installDir -Recurse -File | Where-Object { $_.Name -in @('ffmpeg.exe', 'ffprobe.exe') })
    if ($included.Count) { throw 'Installer unlawfully bundled FFmpeg/ffprobe binaries.' }
    if (-not (Test-Path -LiteralPath $verb) -or -not (Test-Path -LiteralPath $com)) { throw 'Classic Explorer COM registration missing.' }
    $actualDll = (Get-Item -LiteralPath $com).GetValue('')
    $threading = (Get-Item -LiteralPath $com).GetValue('ThreadingModel')
    $handler = (Get-Item -LiteralPath $verb).GetValue('ExplorerCommandHandler')
    $selection = (Get-Item -LiteralPath $verb).GetValue('MultiSelectModel')
    if ($actualDll -ine (Join-Path $installDir 'SlimClipCommand.dll') -or $threading -ne 'Apartment' -or $handler -ine $guid -or $selection -ne 'Player') {
        throw "Classic Explorer handler mismatch: DLL=$actualDll; threading=$threading; CLSID=$handler; selection=$selection"
    }
    Write-Host 'PASS: per-user EXE install, classic COM submenu registration, no bundled FFmpeg.'
} finally {
    if (Test-Path -LiteralPath $uninstaller -PathType Leaf) {
        $remove = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -PassThru -Wait
        if ($remove.ExitCode -ne 0) { throw "Inno uninstall failed with exit code $($remove.ExitCode)" }
    }
}
if ((Test-Path -LiteralPath $verb) -or (Test-Path -LiteralPath $com) -or (Test-Path -LiteralPath (Join-Path $installDir 'SlimClip.exe')) -or @(Find-UninstallEntries).Count -ne 0) {
    throw 'Uninstall left application, uninstall entry or classic Explorer COM registration behind.'
}
Write-Host 'PASS: EXE uninstall removed application and per-user menu registration.'
