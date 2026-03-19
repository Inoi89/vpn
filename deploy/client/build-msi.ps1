param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Version = "0.1.4",
    [string]$PublishRoot = "artifacts/client-publish",
    [string]$OutputRoot = "artifacts/client-installer",
    [string]$ProductName = "YourVpnClient",
    [string]$Manufacturer = "YourVpnClient",
    [string]$UpgradeCode = "A2A3B8B1-4D4D-49FA-B84B-61D70C5A2E11"
)

$ErrorActionPreference = "Stop"

function Convert-ToMsiVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InputVersion
    )

    $match = [regex]::Match($InputVersion, '^(?<major>\d+)(?:\.(?<minor>\d+))?(?:\.(?<patch>\d+))?(?:\.(?<revision>\d+))?')
    if (-not $match.Success) {
        throw "Unable to convert version '$InputVersion' to an MSI-compatible version."
    }

    $parts = @(
        [int]$match.Groups["major"].Value,
        $(if ($match.Groups["minor"].Success) { [int]$match.Groups["minor"].Value } else { 0 }),
        $(if ($match.Groups["patch"].Success) { [int]$match.Groups["patch"].Value } else { 0 }),
        $(if ($match.Groups["revision"].Success) { [int]$match.Groups["revision"].Value } else { 0 })
    )

    foreach ($part in $parts) {
        if ($part -lt 0 -or $part -gt 65534) {
            throw "MSI version part '$part' from '$InputVersion' is out of range."
        }
    }

    return ($parts -join ".")
}

function Invoke-ExternalTool {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & $ExecutablePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $ExecutablePath $($Arguments -join ' ')"
    }
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$publishScript = Join-Path $repoRoot "deploy\client\publish-win-x64.ps1"
$publishDirectory = Join-Path $repoRoot (Join-Path $PublishRoot $RuntimeIdentifier)
$installerOutputDirectory = Join-Path $repoRoot (Join-Path $OutputRoot $RuntimeIdentifier)
$workingDirectory = Join-Path $installerOutputDirectory "wix"
$wixRoot = Join-Path $repoRoot ".tmp\wix314"
$wixToolsDirectory = Join-Path $wixRoot "bin"
$templatePath = Join-Path $repoRoot "deploy\client\wix\Product.wxs"
$licensePath = Join-Path $repoRoot "deploy\client\wix\license.rtf"
$harvestPath = Join-Path $workingDirectory "Harvest.wxs"
$msiPath = Join-Path $installerOutputDirectory ("{0}-{1}.msi" -f $ProductName, $Version)
$msiVersion = Convert-ToMsiVersion -InputVersion $Version

powershell -ExecutionPolicy Bypass -File $publishScript -Configuration $Configuration -RuntimeIdentifier $RuntimeIdentifier -Version $Version

if (-not (Test-Path (Join-Path $wixToolsDirectory "candle.exe"))) {
    New-Item -ItemType Directory -Force -Path $wixRoot | Out-Null
    $zipPath = Join-Path $wixRoot "wix314-binaries.zip"
    Invoke-WebRequest -Uri "https://github.com/wixtoolset/wix3/releases/download/wix3141rtm/wix314-binaries.zip" -OutFile $zipPath
    if (Test-Path $wixToolsDirectory) {
        Remove-Item $wixToolsDirectory -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $wixToolsDirectory | Out-Null
    tar -xf $zipPath -C $wixToolsDirectory
}

if (Test-Path $installerOutputDirectory) {
    Remove-Item $installerOutputDirectory -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $workingDirectory | Out-Null

Invoke-ExternalTool -ExecutablePath (Join-Path $wixToolsDirectory "heat.exe") -Arguments @(
    "dir",
    $publishDirectory,
    "-nologo",
    "-gg",
    "-scom",
    "-sreg",
    "-srd",
    "-dr", "INSTALLFOLDER",
    "-cg", "AppFiles",
    "-var", "var.PublishDir",
    "-out", $harvestPath
)

$harvestContent = Get-Content -Path $harvestPath -Raw
$harvestContent = $harvestContent -replace '<Component ', '<Component Win64="yes" '
Set-Content -Path $harvestPath -Value $harvestContent -Encoding UTF8

Invoke-ExternalTool -ExecutablePath (Join-Path $wixToolsDirectory "candle.exe") -Arguments @(
    "-nologo",
    "-ext", "WixUIExtension",
    "-dPublishDir=$publishDirectory",
    "-dProductVersion=$msiVersion",
    "-dProductName=$ProductName",
    "-dManufacturer=$Manufacturer",
    "-dUpgradeCode=$UpgradeCode",
    "-dWixLicenseRtf=$licensePath",
    "-out", (Join-Path $workingDirectory ""),
    $templatePath,
    $harvestPath
)

Invoke-ExternalTool -ExecutablePath (Join-Path $wixToolsDirectory "light.exe") -Arguments @(
    "-nologo",
    "-ext", "WixUIExtension",
    "-sice:ICE03",
    "-sice:ICE61",
    "-out", $msiPath,
    (Join-Path $workingDirectory "Product.wixobj"),
    (Join-Path $workingDirectory "Harvest.wixobj")
)

if (-not (Test-Path $msiPath)) {
    throw "MSI build finished without creating '$msiPath'."
}

Write-Host ""
Write-Host "MSI created: $msiPath"
