param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$ServerPassword,

    [string]$ServerHost = "37.1.197.163",
    [string]$ServerUser = "root",
    [string]$Domain = "vpn.udni.ru",
    [string]$RemoteDirectory = "/srv/vpn-updates/vpn-client/stable",
    [string]$PackageBaseUrl = "https://vpn.udni.ru/vpn-client/stable",
    [string]$InstallerOutputRoot = "artifacts/client-installer/win-x64",
    [string]$PublishOutputRoot = "artifacts/client-publish",
    [string]$ReleaseNotes = "",
    [switch]$UploadZip
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$msiPath = Join-Path $repoRoot (Join-Path $InstallerOutputRoot ("YourVpnClient-" + $Version + ".msi"))
$zipPath = Join-Path $repoRoot (Join-Path $PublishOutputRoot "VpnClient-win-x64.zip")
$manifestPath = Join-Path $repoRoot (Join-Path $InstallerOutputRoot "update-manifest.json")
$plink = "C:\Program Files\PuTTY\plink.exe"
$pscp = "C:\Program Files\PuTTY\pscp.exe"

if (-not (Test-Path $plink)) {
    throw "plink.exe was not found at '$plink'."
}

if (-not (Test-Path $pscp)) {
    throw "pscp.exe was not found at '$pscp'."
}

if (-not (Test-Path $msiPath)) {
    throw "MSI package was not found at '$msiPath'."
}

if ($UploadZip -and -not (Test-Path $zipPath)) {
    throw "ZIP package was not found at '$zipPath'."
}

$generatorScript = Join-Path $repoRoot "deploy\client\generate-update-manifest.ps1"
& $generatorScript `
    -Version $Version `
    -PackagePath $msiPath `
    -PackageBaseUrl $PackageBaseUrl `
    -OutputPath $manifestPath `
    -ReleaseNotes $ReleaseNotes

& $plink -batch -ssh -pw $ServerPassword "$ServerUser@$ServerHost" "mkdir -p $RemoteDirectory"
& $pscp -batch -pw $ServerPassword $msiPath "$ServerUser@$ServerHost`:$RemoteDirectory/"
& $pscp -batch -pw $ServerPassword $manifestPath "$ServerUser@$ServerHost`:$RemoteDirectory/"

if ($UploadZip) {
    & $pscp -batch -pw $ServerPassword $zipPath "$ServerUser@$ServerHost`:$RemoteDirectory/"
}

& $plink -batch -ssh -pw $ServerPassword "$ServerUser@$ServerHost" "ls -lh $RemoteDirectory"

Write-Host ""
Write-Host "Published update origin to $Domain"
Write-Host "Manifest URL: $PackageBaseUrl/update-manifest.json"
