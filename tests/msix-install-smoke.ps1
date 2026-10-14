param(
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [Parameter(Mandatory = $true)][string]$CertificatePath
)
$ErrorActionPreference = 'Stop'
$PackagePath = [System.IO.Path]::GetFullPath($PackagePath)
$CertificatePath = [System.IO.Path]::GetFullPath($CertificatePath)
if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf) -or
    -not (Test-Path -LiteralPath $CertificatePath -PathType Leaf)) {
    throw 'The signed MSIX or its public test certificate is missing.'
}
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_OS -ne 'Windows') {
    throw 'Only a disposable Windows Actions runner may import the test certificate.'
}
$packageName = 'SlimClip.Menu'
if (@(Get-AppxPackage -Name $packageName).Count) { throw 'Runner already has SlimClip installed; refusing to touch it.' }
$certificate = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($CertificatePath)
if ($certificate.Subject -cne 'CN=SlimClip Development') { throw 'Test signer subject does not match the MSIX manifest publisher.' }
$thumbprint = $certificate.Thumbprint
$trustedPath = 'Cert:\LocalMachine\TrustedPeople\' + $thumbprint
if (Test-Path -LiteralPath $trustedPath) { throw 'Test signer is already trusted on runner; refusing to remove a pre-existing certificate.' }
$imported = $false
try {
    # Explicit trust ONLY on an ephemeral CI machine; never run this on a user's PC.
    $trusted = Import-Certificate -FilePath $CertificatePath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople'
    if ($trusted.Thumbprint -ne $thumbprint) { throw 'Imported certificate fingerprint mismatch.' }
    $imported = $true
    Add-AppxPackage -Path $PackagePath -ErrorAction Stop
    $installed = @(Get-AppxPackage -Name $packageName)
    if ($installed.Count -ne 1 -or $installed[0].Publisher -cne 'CN=SlimClip Development') {
        throw 'Installed package identity or publisher did not match SlimClip.'
    }
    Write-Host "Installed package: $($installed[0].PackageFullName)"
} finally {
    # Remove only the known package and the exact public certificate we imported.
    foreach ($item in @(Get-AppxPackage -Name $packageName -ErrorAction SilentlyContinue)) {
        Remove-AppxPackage -Package $item.PackageFullName -ErrorAction Stop
    }
    if ($imported -and (Test-Path -LiteralPath $trustedPath)) {
        Remove-Item -LiteralPath $trustedPath -Force -ErrorAction Stop
    }
}
if (@(Get-AppxPackage -Name $packageName).Count -or (Test-Path -LiteralPath $trustedPath)) {
    throw 'MSIX or temporary test certificate remained after uninstall.'
}
Write-Host 'PASS: signed MSIX installed and uninstalled on disposable Windows runner.'
