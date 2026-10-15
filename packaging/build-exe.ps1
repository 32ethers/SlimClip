# Run on Windows with the .NET 8 SDK, an x64 MSVC Developer environment and Inno Setup 6.
# No FFmpeg binaries, MSIX identity, signing certificate or trust-store changes are used.
[CmdletBinding()]
param(
    [string] $IsccPath,
    [ValidatePattern('^\d+\.\d+\.\d+$')][string] $Version = '1.0.0',
    [string] $OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = [System.IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $root 'artifacts' }
$output = [System.IO.Path]::GetFullPath($OutputDirectory)
$setup = Join-Path $output 'SlimClip-Setup.exe'
$stage = Join-Path $output ('exe-stage-' + [guid]::NewGuid().ToString('N'))
$native = Join-Path $output ('exe-native-' + [guid]::NewGuid().ToString('N'))

function Require-Tool([string] $name) {
    $tool = Get-Command $name -ErrorAction SilentlyContinue
    if (-not $tool) { throw "Missing build tool: $name" }
    return $tool.Source
}
function Require-Exit([string] $step) {
    if ($LASTEXITCODE -ne 0) { throw "$step failed (exit code $LASTEXITCODE)" }
}

$dotnet = Require-Tool 'dotnet.exe'
$cl = Require-Tool 'cl.exe'
$dumpbin = Require-Tool 'dumpbin.exe'
if (-not (@(& $dotnet --list-sdks | Where-Object { $_ -match '^8\.' }).Count)) {
    throw 'Missing .NET 8 SDK'
}
if ($IsccPath) {
    $iscc = (Resolve-Path -LiteralPath $IsccPath -ErrorAction Stop).ProviderPath
    if (-not (Test-Path -LiteralPath $iscc -PathType Leaf)) { throw "ISCC is not a file: $iscc" }
} else {
    $iscc = (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source)
    if (-not $iscc) {
        foreach ($base in @(${env:ProgramFiles(x86)}, ${env:ProgramFiles})) {
            if (-not $base) { continue }
            $candidate = Join-Path $base 'Inno Setup 6\ISCC.exe'
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { $iscc = $candidate; break }
        }
    }
    if (-not $iscc) { throw 'Missing Inno Setup 6 ISCC.exe; pass -IsccPath' }
}
if (-not (Test-Path -LiteralPath $output)) { New-Item -ItemType Directory -Path $output | Out-Null }
if (Test-Path -LiteralPath $setup) { throw "Refusing to overwrite existing installer: $setup" }
# Unique stage/native paths; never delete an existing directory or touch user content.
New-Item -ItemType Directory -Path $stage, $native | Out-Null

& $dotnet publish (Join-Path $root 'SlimClip.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $stage
Require-Exit 'Self-contained .NET publish'
if (-not (Test-Path -LiteralPath (Join-Path $stage 'SlimClip.exe') -PathType Leaf)) { throw 'Publish did not create SlimClip.exe' }
# Fail closed if project dependencies ever introduce third-party media executables.
$forbidden = @(Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object { $_.Name -in @('ffmpeg.exe', 'ffprobe.exe') })
if ($forbidden.Count) { throw 'Publish contains FFmpeg/ffprobe binaries; refusing to package' }

Push-Location $native
try {
    $definition = '/DEF:' + (Join-Path $root 'shell\SlimClipCommand.def')
    $dll = Join-Path $stage 'SlimClipCommand.dll'
    & $cl /nologo /W4 /EHsc /std:c++17 /utf-8 /MT /LD (Join-Path $root 'shell\SlimClipCommand.cpp') /link ('/OUT:' + $dll) $definition shlwapi.lib shell32.lib ole32.lib
    Require-Exit 'x64 Explorer COM DLL compilation'
    $exports = & $dumpbin /exports $dll
    Require-Exit 'COM export inspection'
    if (-not ($exports | Select-String 'DllGetClassObject') -or -not ($exports | Select-String 'DllCanUnloadNow')) {
        throw 'Native DLL lacks required COM exports'
    }
    # Explorer must load the DLL on a clean Windows 11 PC without a VC++ redistributable.
    $dependencies = & $dumpbin /dependents $dll
    Require-Exit 'Native runtime dependency inspection'
    if (@($dependencies | Select-String -Pattern '(?i)\b(VCRUNTIME|MSVCP|ucrtbase)[\w.-]*\.dll\b').Count) {
        throw 'Explorer DLL unexpectedly depends on a separately installed VC++ runtime'
    }
    $selfTest = Join-Path $native 'SlimClipCommandSelfTest.exe'
    & $cl /nologo /W4 /EHsc /std:c++17 /utf-8 (Join-Path $root 'shell\SlimClipCommandSelfTest.cpp') /link ('/OUT:' + $selfTest) ole32.lib uuid.lib
    Require-Exit 'Native COM self-test compilation'
    & $selfTest $dll
    Require-Exit 'Native COM self-test'
} finally { Pop-Location }

Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination (Join-Path $stage 'LICENSE.txt')
# The MIT notice accompanies the separate Simplified Chinese Inno translation embedded in Setup.
Copy-Item -LiteralPath (Join-Path $root 'packaging\translations\LICENSE.txt') -Destination (Join-Path $stage 'INNO-CHINESE-TRANSLATION-LICENSE.txt')
# The test runner and C++ intermediate files remain in exe-native-*, not the installer stage.
& $iscc ('/DStageDir=' + $stage) ('/DAppVersion=' + $Version) ('/DLogoFile=' + (Join-Path $root 'assets\SlimClip.ico')) ('/DTranslationFile=' + (Join-Path $root 'packaging\translations\ChineseSimplified.isl')) ('/O' + $output) '/FSlimClip-Setup' (Join-Path $PSScriptRoot 'SlimClip.iss')
Require-Exit 'Inno Setup compilation'
if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) { throw "Missing expected installer: $setup" }
Write-Host "Installer: $setup (unsigned; no bundled FFmpeg; no certificate/trust changes)"
Write-Host 'FFmpeg/ffprobe must be configured separately by the user in application settings.'
