# Build a FULL Windows 11 MSIX (not an external-location / sparse identity package).
# Run under Windows PowerShell in an x64 VS Developer Command Prompt; the CI workflow
# sets this up. Packaging never installs this app or trusts its signing certificate.
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string] $FFmpegDir,
    [Parameter(Mandatory = $true)][string] $FFmpegLicenseDir,
    [string] $FFmpegSourceUrl = $env:FFMPEG_URL,
    [string] $FFmpegArchiveSha256 = $env:FFMPEG_SHA256,
    [ValidatePattern('^\d{1,5}\.\d{1,5}\.\d{1,5}\.0$')][string] $PackageVersion = '1.0.0.0',
    [ValidateSet('Unsigned', 'Test')][string] $Signing = 'Unsigned'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = [System.IO.Path]::GetFullPath($PSScriptRoot)
$artifacts = Join-Path $root 'artifacts'
$buildId = [guid]::NewGuid().ToString('N')
$publish = Join-Path $artifacts "publish-$buildId"
$stage = Join-Path $artifacts "full-msix-stage-$buildId"
$unsigned = Join-Path $artifacts 'SlimClip.unsigned.msix'
$signed = Join-Path $artifacts 'SlimClip.test.msix'
$cer = Join-Path $artifacts 'SlimClip.test.cer'

function Assert-Tool([string] $name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) { throw "Missing build tool: $name" }
}
function Assert-ChildOfArtifacts([string] $path) {
    $full = [System.IO.Path]::GetFullPath($path)
    $prefix = [System.IO.Path]::GetFullPath($artifacts).TrimEnd('\') + '\'
    if (-not $full.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside artifacts: $full"
    }
}
function Create-Stage([string] $path) {
    Assert-ChildOfArtifacts $path
    $resolved = [System.IO.Path]::GetFullPath($path)
    if (Test-Path -LiteralPath $artifacts) {
        $item = Get-Item -LiteralPath $artifacts -Force
        if (($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing a redirected artifacts directory: $artifacts"
        }
    }
    if (Test-Path -LiteralPath $resolved) { throw "Refusing to reuse an existing staging directory: $resolved" }
    New-Item -ItemType Directory -Path $resolved | Out-Null
}
function Create-Logo([string] $path, [int] $size) {
    Add-Type -AssemblyName System.Drawing
    $image = New-Object System.Drawing.Bitmap($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($image)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(22, 87, 125))
    $font = New-Object System.Drawing.Font('Segoe UI', ($size * 0.52), [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.FillRectangle($brush, 0, 0, $size, $size)
        $graphics.DrawString('S', $font, [System.Drawing.Brushes]::White, ($size * 0.18), ($size * 0.12))
        $image.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $font.Dispose(); $brush.Dispose(); $graphics.Dispose(); $image.Dispose() }
}
function Manifest-Field([xml] $xml, [string] $xpath, [string] $attribute) {
    $node = $xml.SelectSingleNode($xpath)
    if (-not $node) { throw "Missing manifest element: $xpath" }
    return $node.GetAttribute($attribute)
}

Assert-Tool dotnet
Assert-Tool cl.exe
Assert-Tool makeappx.exe
Assert-Tool dumpbin.exe
if ($Signing -eq 'Test') { Assert-Tool signtool.exe }
if (-not (@(& dotnet --list-sdks) | Where-Object { $_ -match '^8\.' })) { throw 'Missing .NET 8 SDK' }
if ($FFmpegSourceUrl -notmatch '^https://') { throw 'FFmpegSourceUrl must be an HTTPS release/archive URL' }
if ($FFmpegArchiveSha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'FFmpegArchiveSha256 must be a 64-digit SHA-256' }
$FFmpegDir = [System.IO.Path]::GetFullPath($FFmpegDir)
$FFmpegLicenseDir = [System.IO.Path]::GetFullPath($FFmpegLicenseDir)
foreach ($tool in @('ffmpeg.exe', 'ffprobe.exe')) {
    if (-not (Test-Path -LiteralPath (Join-Path $FFmpegDir $tool) -PathType Leaf)) { throw "Missing $tool in $FFmpegDir" }
}
if (-not (Test-Path -LiteralPath $FFmpegLicenseDir -PathType Container)) { throw 'FFmpeg license directory is missing' }
$licenseFiles = @(Get-ChildItem -LiteralPath $FFmpegLicenseDir -Recurse -File |
    Where-Object { $_.Name -match '^(LICENSE|COPYING|NOTICE|README)' })
if (-not @($licenseFiles | Where-Object { $_.Name -match '^(LICENSE|COPYING|NOTICE)' }).Count) {
    throw 'Provide the FFmpeg distribution license/COPYING/NOTICE files; never ship third-party binaries without their notices'
}
$versionParts = @($PackageVersion.Split('.') | ForEach-Object { [int]$_ })
if ($versionParts | Where-Object { $_ -gt 65535 }) { throw 'MSIX package version components must be <= 65535' }
$manifest = [xml](Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root 'shell\AppxManifest.xml'))
$appManifest = [xml](Get-Content -Raw -Encoding UTF8 -LiteralPath (Join-Path $root 'app.manifest'))
$publisher = Manifest-Field $manifest "//*[local-name()='Identity']" 'Publisher'
$packageName = Manifest-Field $manifest "//*[local-name()='Identity']" 'Name'
$appId = Manifest-Field $manifest "//*[local-name()='Application']" 'Id'
$architecture = Manifest-Field $manifest "//*[local-name()='Identity']" 'ProcessorArchitecture'
if ($architecture -ne 'x64') { throw 'Native Explorer DLL and package architecture must both be x64' }
if ($manifest.SelectNodes("//*[local-name()='AllowExternalContent']").Count -ne 0) { throw 'This must be a FULL MSIX, not a sparse package' }
$msixNode = $appManifest.SelectSingleNode("//*[local-name()='msix']")
if (-not $msixNode -or $msixNode.GetAttribute('publisher') -cne $publisher -or
    $msixNode.GetAttribute('packageName') -cne $packageName -or
    $msixNode.GetAttribute('applicationId') -cne $appId) {
    throw 'app.manifest msix identity must match shell/AppxManifest.xml exactly'
}
$comClass = Manifest-Field $manifest "//*[local-name()='Class' and @Path='SlimClipCommand.dll']" 'Id'
$verb = Manifest-Field $manifest "//*[local-name()='Verb' and @Id='SlimClipCompress']" 'Clsid'
if ($comClass -cne $verb -or $comClass -cne '83e9cd33-c9e0-44de-b265-f18f91a9a929') {
    throw 'Explorer command CLSID does not match the native DLL'
}

New-Item -ItemType Directory -Path $artifacts -Force | Out-Null
Create-Stage $publish
Create-Stage $stage
& dotnet publish (Join-Path $root 'SlimClip.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
if (-not (Test-Path -LiteralPath (Join-Path $publish 'SlimClip.exe') -PathType Leaf)) { throw 'Missing published SlimClip.exe' }
Copy-Item -Path (Join-Path $publish '*') -Destination $stage -Recurse -Force
Push-Location $stage
try {
    $exportDefinition = '/DEF:' + (Join-Path $root 'shell\SlimClipCommand.def')
    & cl.exe /nologo /W4 /EHsc /std:c++17 /utf-8 /LD (Join-Path $root 'shell\SlimClipCommand.cpp') /link /OUT:SlimClipCommand.dll $exportDefinition shlwapi.lib shell32.lib
    if ($LASTEXITCODE -ne 0) { throw 'Native Explorer command compilation failed' }
    $exports = & dumpbin.exe /exports (Join-Path $stage 'SlimClipCommand.dll')
    if ($LASTEXITCODE -ne 0 -or -not ($exports | Select-String 'DllGetClassObject') -or -not ($exports | Select-String 'DllCanUnloadNow')) {
        throw 'Native DLL lacks required COM exports'
    }
} finally { Pop-Location }
foreach ($tool in @('ffmpeg.exe', 'ffprobe.exe')) {
    Copy-Item -LiteralPath (Join-Path $FFmpegDir $tool) -Destination (Join-Path $stage $tool) -Force
}
$licenseOut = Join-Path $stage 'licenses\FFmpeg'
New-Item -ItemType Directory -Path $licenseOut -Force | Out-Null
foreach ($licenseFile in $licenseFiles) {
    $relative = $licenseFile.FullName.Substring($FFmpegLicenseDir.TrimEnd('\').Length).TrimStart('\')
    $destination = Join-Path $licenseOut $relative
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $licenseFile.FullName -Destination $destination -Force
}
@("FFmpeg archive: $FFmpegSourceUrl", "Archive SHA-256: $FFmpegArchiveSha256", 'Check FFmpeg and linked library redistribution obligations before a public release.') |
    Set-Content -LiteralPath (Join-Path $licenseOut 'BINARY-PROVENANCE.txt') -Encoding UTF8
$identityNode = $manifest.SelectSingleNode("//*[local-name()='Identity']")
$identityNode.SetAttribute('Version', $PackageVersion)
$manifest.Save((Join-Path $stage 'AppxManifest.xml'))
$assets = Join-Path $stage 'Assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
Create-Logo (Join-Path $assets 'StoreLogo.png') 50
Create-Logo (Join-Path $assets 'Square44x44Logo.png') 44
Create-Logo (Join-Path $assets 'Square150x150Logo.png') 150
foreach ($path in @($unsigned, $signed, $cer)) {
    Assert-ChildOfArtifacts $path
    if (Test-Path -LiteralPath $path) { throw "Refusing to overwrite an existing build artifact: $path" }
}
# The output filename says UNSIGNED; never call it an installable package.
& makeappx.exe pack /o /d $stage /p $unsigned
if ($LASTEXITCODE -ne 0) { throw 'FULL MSIX packaging failed' }
Write-Host "Unsigned build: $unsigned (NOT INSTALLABLE; no signature)"

if ($Signing -eq 'Test') {
    $testCert = $null
    try {
        # The private key stays non-exportable in the ephemeral CI account. Never
        # upload a PFX/private key; do not install the certificate into TrustedPeople.
        $testCert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $publisher `
            -CertStoreLocation 'Cert:\CurrentUser\My' -KeyExportPolicy NonExportable `
            -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 -NotAfter (Get-Date).AddDays(30)
        if (-not $testCert -or $testCert.Subject -cne $publisher) { throw 'Failed to generate matching test certificate' }
        Export-Certificate -Cert $testCert -FilePath $cer -Force | Out-Null
        Copy-Item -LiteralPath $unsigned -Destination $signed -Force
        & signtool.exe sign /fd SHA256 /s My /sha1 $testCert.Thumbprint $signed
        if ($LASTEXITCODE -ne 0) { throw 'Signing test MSIX failed; only unsigned artifact is usable as a build intermediate' }
        Write-Host "Self-signed TEST package: $signed; public certificate: $cer"
        Write-Host "Signer SHA-1 thumbprint: $($testCert.Thumbprint)"
        Write-Host 'NOT trusted on other PCs. The tester must explicitly trust the PUBLIC certificate first.'
    } finally {
        if ($testCert) {
            $ownedCertPath = 'Cert:\CurrentUser\My\' + $testCert.Thumbprint
            if ($testCert.Subject -ceq $publisher -and (Test-Path -LiteralPath $ownedCertPath)) {
                Remove-Item -LiteralPath $ownedCertPath -Force
            }
        }
    }
}
