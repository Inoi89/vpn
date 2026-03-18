param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [string]$PackageBaseUrl,

    [string]$Channel = "stable",
    [string]$ApplicationId = "YourVpnClient",
    [string]$OutputPath = "artifacts/client-installer/win-x64/update-manifest.json",
    [string]$ReleaseNotes = "",
    [string]$MinimumSupportedVersion = "",
    [string]$PackageCertificateThumbprint = "",
    [switch]$Mandatory
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$resolvedPackagePath = if ([System.IO.Path]::IsPathRooted($PackagePath)) {
    $PackagePath
} else {
    Join-Path $repoRoot $PackagePath
}

if (-not (Test-Path $resolvedPackagePath)) {
    throw "Package not found: $resolvedPackagePath"
}

$outputFullPath = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
    $OutputPath
} else {
    Join-Path $repoRoot $OutputPath
}

$hash = Get-FileHash -Path $resolvedPackagePath -Algorithm SHA256
$packageFile = Split-Path $resolvedPackagePath -Leaf
$packageUrl = ($PackageBaseUrl.TrimEnd('/') + "/" + $packageFile)
$packageSize = (Get-Item $resolvedPackagePath).Length

$manifest = [ordered]@{
    applicationId = $ApplicationId
    channel = $Channel
    release = [ordered]@{
        version = $Version
        packageUrl = $packageUrl
        sha256 = $hash.Hash.ToLowerInvariant()
        sizeBytes = $packageSize
        publishedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
        releaseNotes = $(if ([string]::IsNullOrWhiteSpace($ReleaseNotes)) { $null } else { $ReleaseNotes })
        isMandatory = [bool]$Mandatory
        minimumSupportedVersion = $(if ([string]::IsNullOrWhiteSpace($MinimumSupportedVersion)) { $null } else { $MinimumSupportedVersion })
        channel = $Channel
        packageCertificateThumbprint = $(if ([string]::IsNullOrWhiteSpace($PackageCertificateThumbprint)) { $null } else { $PackageCertificateThumbprint })
    }
}

$outputDirectory = Split-Path -Parent $outputFullPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
}

$manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $outputFullPath -Encoding UTF8

Write-Host "Generated update manifest: $outputFullPath"
Write-Host "Package URL: $packageUrl"
Write-Host "SHA256: $($hash.Hash.ToLowerInvariant())"
