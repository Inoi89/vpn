# Desktop Update Strategy

Snapshot date: `2026-03-19`

This document describes the current self-update mechanism for the Windows desktop VPN client.

## 1. Goal

The client should be able to update itself as a packaged product without requiring:

- a preinstalled Amnezia desktop app
- a separate updater service already present on the machine
- manual file replacement in `Program Files`

The current strategy is:

- publish a signed per-machine MSI
- host a small JSON manifest over HTTPS
- let the client check that manifest
- download the MSI
- verify the SHA-256 digest
- optionally verify the Authenticode signer thumbprint
- hand off installation to an external elevated updater launcher

## 2. Why MSI + External Updater

The client is installed per-machine and requires elevation anyway because it:

- creates VPN adapters
- changes DNS
- adjusts MTU
- manages routes

That makes an MSI-based product path natural. Updating in-place from the main UI process is fragile because the app is running from the directory being upgraded. The external launcher avoids that race.

## 3. Main Components

Update contracts:

- [IAppUpdateService.cs](/c:/Users/rrese/source/repos/vpn/Core/Interfaces/IAppUpdateService.cs)
- [AppUpdateState.cs](/c:/Users/rrese/source/repos/vpn/Core/Models/Updates/AppUpdateState.cs)
- [AppUpdateRelease.cs](/c:/Users/rrese/source/repos/vpn/Core/Models/Updates/AppUpdateRelease.cs)

Use cases:

- [CheckForAppUpdatesUseCase.cs](/c:/Users/rrese/source/repos/vpn/Application/Updates/CheckForAppUpdatesUseCase.cs)
- [PrepareAppUpdateUseCase.cs](/c:/Users/rrese/source/repos/vpn/Application/Updates/PrepareAppUpdateUseCase.cs)
- [LaunchPreparedAppUpdateUseCase.cs](/c:/Users/rrese/source/repos/vpn/Application/Updates/LaunchPreparedAppUpdateUseCase.cs)

Manifest-driven update service:

- [JsonManifestAppUpdateService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Updates/JsonManifestAppUpdateService.cs)
- [UpdateManifestDocument.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Updates/UpdateManifestDocument.cs)
- [UpdatePackageVerifier.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Updates/UpdatePackageVerifier.cs)
- [AppVersionParser.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Updates/AppVersionParser.cs)

External launcher:

- [Program.cs](/c:/Users/rrese/source/repos/vpn/Updater/Program.cs)

## 4. Runtime Flow

1. The UI reads `Updates:ManifestUrl` and `Updates:Channel`.
2. The client downloads the manifest JSON.
3. The client validates `applicationId` and compares versions.
4. If a newer release exists, the client downloads the MSI to:
   - `%LocalAppData%/YourVpnClient/Updates/...`
5. The package hash is verified.
6. If configured, the package signer thumbprint is verified.
7. The client launches `VpnClient.Updater.exe`.
8. The updater waits for the main UI process to exit.
9. The updater runs:
   - `msiexec /i package.msi /passive /norestart`
10. After success, the updater relaunches the main application.

## 5. Manifest Format

Example:

```json
{
  "applicationId": "YourVpnClient",
  "channel": "stable",
  "release": {
    "version": "0.2.0",
    "packageUrl": "https://downloads.example.com/YourVpnClient-0.2.0.msi",
    "sha256": "0123456789abcdef...",
    "sizeBytes": 123456789,
    "publishedAtUtc": "2026-03-18T18:40:00.0000000+00:00",
    "releaseNotes": "Bundled runtime fixes and UX polish.",
    "isMandatory": false,
    "minimumSupportedVersion": "0.1.0",
    "channel": "stable",
    "packageCertificateThumbprint": "ABCDEF0123456789ABCDEF0123456789ABCDEF01"
  }
}
```

## 6. Release Flow

1. Build publish payload:
   - [publish-win-x64.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/publish-win-x64.ps1)
2. Build MSI:
   - [build-msi.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/build-msi.ps1)
3. Generate manifest:
   - [generate-update-manifest.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/generate-update-manifest.ps1)
4. Upload the MSI and JSON manifest to HTTPS hosting.
5. Set `Updates:ManifestUrl` in the client.

Example:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\client\generate-update-manifest.ps1 `
  -Version 0.1.4 `
  -PackagePath artifacts\client-installer\win-x64\YourVpnClient-0.1.4.msi `
  -PackageBaseUrl https://downloads.example.com/vpn-client `
  -OutputPath artifacts\client-installer\win-x64\update-manifest.json `
  -ReleaseNotes "0.1.4 desktop shortcut fix and minimal update action."
```

There is also a direct upload helper for the current origin:

- [publish-update-origin.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/publish-update-origin.ps1)

Current target layout:

- domain: `vpn.udni.ru`
- server: `37.1.197.163`
- path: `https://vpn.udni.ru/vpn-client/stable/update-manifest.json`
- current client default: [appsettings.json](/c:/Users/rrese/source/repos/vpn/UI/appsettings.json)

## 7. Current Production Flow

There are now three concrete flows in production terms.

### 7.1 Client check flow

1. The desktop client starts.
2. It reads:
   - `Updates:ManifestUrl`
   - `Updates:Channel`
3. It performs a background check against:
   - `https://vpn.udni.ru/vpn-client/stable/update-manifest.json`
4. If the manifest version is newer than the local version:
   - the UI switches to `UpdateAvailable`
5. When the user confirms:
   - the MSI is downloaded
   - the SHA-256 is verified
   - `VpnClient.Updater.exe` is launched
6. The launcher waits for the UI process to exit, runs `msiexec`, then relaunches the app.
7. A newer MSI with the same `UpgradeCode` upgrades an existing install in place, so `0.1.1` replaces `0.1.0-local` instead of side-by-side installation.

### 7.2 Release build flow

1. Build portable/self-contained payload:
   - [publish-win-x64.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/publish-win-x64.ps1)
2. Build installer:
   - [build-msi.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/build-msi.ps1)
3. Generate manifest:
   - [generate-update-manifest.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/generate-update-manifest.ps1)
4. Upload everything to origin:
   - [publish-update-origin.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/publish-update-origin.ps1)

### 7.3 Hosted origin flow

1. `nginx` serves static files from:
   - `/srv/vpn-updates/vpn-client/stable`
2. `vpn.udni.ru` terminates HTTPS on:
   - `37.1.197.163`
3. The manifest is served with `no-store`.
4. MSI/ZIP payloads are served as static files with short caching.

## 8. Operational Notes

Current server:

- host: `37.1.197.163`
- role: update/download origin
- public domain: `vpn.udni.ru`
- TLS: Let's Encrypt via `certbot --nginx`

Current hosted files:

- `https://vpn.udni.ru/vpn-client/stable/update-manifest.json`
- `https://vpn.udni.ru/vpn-client/stable/YourVpnClient-0.1.4.msi`
- `https://vpn.udni.ru/vpn-client/stable/VpnClient-win-x64.zip`

Operational warning:

- the updater trusts HTTPS + manifest hash
- production signing is still strongly recommended for MSI
- if the manifest is wrong, the client will still reject the MSI when the hash does not match

## 9. Current Gaps

- delta updates are not implemented
- rollback is left to MSI/uninstall semantics
- code-signing policy still depends on release infrastructure
- only one `stable` channel is live right now
- package signing thumbprint validation is still optional until release signing is in place

## 10. Product Readiness

This is a reasonable first production path because it is:

- deterministic
- inspectable
- compatible with per-machine VPN installation semantics
- simple to host privately

It is intentionally not an app-store-style updater. That would add more moving parts without helping the VPN runtime story.
