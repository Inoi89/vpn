param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Version = "0.1.6",
    [string]$OutputRoot = "artifacts/client-publish",
    [switch]$ZipPackage
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$projectPath = Join-Path $repoRoot "UI\VpnClient.UI.csproj"
$updaterProjectPath = Join-Path $repoRoot "Updater\VpnClient.Updater.csproj"
$publishDirectory = Join-Path $repoRoot (Join-Path $OutputRoot $RuntimeIdentifier)
$updaterPublishDirectory = Join-Path $publishDirectory ".updater-staging"
$runtimeSourceDirectory = Join-Path $repoRoot "third_party\windows\wireguard"
$runtimeTargetDirectory = Join-Path $publishDirectory "runtime\wireguard"

if (Test-Path $publishDirectory) {
    Remove-Item $publishDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDirectory | Out-Null

dotnet publish $projectPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    /p:PublishSingleFile=false `
    /p:PublishTrimmed=false `
    /p:Version=$Version `
    -o $publishDirectory

dotnet publish $updaterProjectPath `
    -c $Configuration `
    -r $RuntimeIdentifier `
    --self-contained true `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=false `
    /p:Version=$Version `
    -o $updaterPublishDirectory

$updaterExecutablePath = Join-Path $updaterPublishDirectory "VpnClient.Updater.exe"
if (-not (Test-Path $updaterExecutablePath)) {
    throw "Updater launcher was not produced at '$updaterExecutablePath'."
}

Copy-Item $updaterExecutablePath (Join-Path $publishDirectory "VpnClient.Updater.exe") -Force
Remove-Item $updaterPublishDirectory -Recurse -Force

New-Item -ItemType Directory -Path $runtimeTargetDirectory -Force | Out-Null

if (Test-Path $runtimeSourceDirectory) {
    Get-ChildItem $runtimeSourceDirectory -File | ForEach-Object {
        Copy-Item $_.FullName (Join-Path $runtimeTargetDirectory $_.Name) -Force
    }
}

$requiredRuntimeFiles = @(
    "amneziawg.exe",
    "awg.exe",
    "wintun.dll"
)

$optionalRuntimeFiles = @(
    "wireguard-service.exe",
    "wireguard.dll",
    "tunnel.dll"
)

$missingRequired = @()
foreach ($file in $requiredRuntimeFiles) {
    if (-not (Test-Path (Join-Path $runtimeTargetDirectory $file))) {
        $missingRequired += $file
    }
}

$missingOptional = @()
foreach ($file in $optionalRuntimeFiles) {
    if (-not (Test-Path (Join-Path $runtimeTargetDirectory $file))) {
        $missingOptional += $file
    }
}

Write-Host ""
Write-Host "Published client to: $publishDirectory"
Write-Host "Bundled runtime directory: $runtimeTargetDirectory"
Write-Host "Bundled updater launcher: $(Join-Path $publishDirectory 'VpnClient.Updater.exe')"

if ($missingRequired.Count -gt 0) {
    Write-Warning ("Missing required bundled runtime files: " + ($missingRequired -join ", "))
    Write-Warning "The publish output will still run, but clean-machine connect will depend on system-installed WireGuard/Wintun."
}
else {
    Write-Host "Required bundled runtime files present: $($requiredRuntimeFiles -join ', ')"
}

if ($missingOptional.Count -gt 0) {
    Write-Host "Optional runtime files not bundled yet: $($missingOptional -join ', ')"
}

if ($ZipPackage) {
    $zipPath = Join-Path $repoRoot (Join-Path $OutputRoot ("VpnClient-" + $RuntimeIdentifier + ".zip"))
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    $archiveEntries = Get-ChildItem -Path $publishDirectory -Force |
        Where-Object { $_.Name -ne ".updater-staging" } |
        ForEach-Object { $_.FullName }

    if ($archiveEntries.Count -eq 0) {
        throw "No publish files were found to archive in '$publishDirectory'."
    }

    Compress-Archive -Path $archiveEntries -DestinationPath $zipPath
    Write-Host "Created zip package: $zipPath"
}
