# Windows Packaging

Snapshot date: `2026-03-19`

This document describes the Windows packaging path for the desktop VPN client as an autonomous product.

## 1. Product Goal

The client must be able to run on a clean Windows machine without requiring a preinstalled Amnezia or WireGuard desktop app.

That means the packaged product must include:

- self-contained .NET desktop publish output
- app-local VPN runtime assets
- administrator elevation
- a stable runtime layout that can be wrapped by a real installer

## 2. Current Runtime Layout

The publish output now expects bundled native/runtime files under:

`runtime/wireguard`

Current required files for the autonomous Windows runtime:

- `amneziawg.exe`
- `awg.exe`
- `wintun.dll`

The source staging directory in the repo is:

`third_party/windows/wireguard`

## 3. Publish Path

Publish profile:

- [win-x64-selfcontained.pubxml](/c:/Users/rrese/source/repos/vpn/UI/Properties/PublishProfiles/win-x64-selfcontained.pubxml)

Publish script:

- [publish-win-x64.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/publish-win-x64.ps1)

Typical command:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\client\publish-win-x64.ps1 -Configuration Release -RuntimeIdentifier win-x64 -Version 0.1.1 -ZipPackage
```

Current release command:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\client\publish-win-x64.ps1 -Configuration Release -RuntimeIdentifier win-x64 -Version 0.1.4 -ZipPackage
```

Primary output:

`artifacts/client-publish/win-x64`

Installer build script:

- [build-msi.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/build-msi.ps1)

Installer output:

`artifacts/client-installer/win-x64/YourVpnClient-0.1.4.msi`

Update manifest generator:

- [generate-update-manifest.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/generate-update-manifest.ps1)

## 4. Why Folder Publish Instead Of Single File

The product needs to ship native runtime assets and keep their paths stable.

Folder publish is the correct choice because:

- app-local native files remain visible and versionable
- `wintun.dll` and the bundled AmneziaWG binaries do not need extraction tricks
- installer tooling can package the folder as-is
- debugging clean-machine failures is simpler than with single-file self-extract behavior
- the external updater launcher can sit next to the main executable without extra extraction logic

## 5. Elevation

The desktop UI manifest now requests administrator rights:

- [app.manifest](/c:/Users/rrese/source/repos/vpn/UI/app.manifest)

This is intentional.

The client creates adapters, configures DNS, sets MTU, and programs routes.
That is not a normal unprivileged desktop workload.

## 6. Runtime Asset Resolution

The client now resolves app-local runtime assets through:

- [IWindowsRuntimeAssetLocator.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/IWindowsRuntimeAssetLocator.cs)

Current behavior:

- prefer bundled `runtime/wireguard/amneziawg.exe`
- prefer bundled `runtime/wireguard/awg.exe`
- prefer bundled `runtime/wireguard/wintun.dll`
- fall back only when the autonomous runtime is not present
- surface warnings when bundled assets are absent

## 7. Production Direction

There are two layers of productization:

### Current milestone

- self-contained desktop publish
- bundled official AmneziaWG runtime binaries
- tunnel-service orchestration through `amneziawg.exe /installtunnelservice`
- status probing through `awg.exe show ... dump`

### Current packaging state

- self-contained publish works
- zip package works
- WiX-based per-machine MSI now builds successfully
- the installer packages bundled `amneziawg.exe`, `awg.exe`, and `wintun.dll`
- the installer now uses a real `InstallDir` flow instead of a hidden fixed target path
- the installer now offers a checked-by-default desktop shortcut option and persists that preference through upgrades
- the publish output includes `VpnClient.Updater.exe`
- the release flow can now emit a JSON update manifest for hosted MSI updates

### Next milestone

- install/update runtime assets deterministically across upgrades
- decide whether to embed the full upstream manager-service stack or keep only the tunnel-service path behind our own UI
- validate the bundled runtime path on a completely clean Windows machine

## 8. Upstream References

The packaging and runtime direction is grounded in the upstream projects already checked into local research:

- [build_windows.bat](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/deploy/build_windows.bat)
- [componentscript.js](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/deploy/installer/packages/org.amneziavpn.package/meta/componentscript.js)
- [daemonlocalserver.cpp](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/client/daemon/daemonlocalserver.cpp)
- [utilities.cpp](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/client/utilities.cpp)

These confirm that Amnezia on Windows is a real packaged application with an installer/service/runtime story, not just a loose executable.

## 9. Practical Run Guide

On a clean Windows machine:

1. Install:
   - `artifacts/client-installer/win-x64/YourVpnClient-0.1.4.msi`
2. Launch the installed app from Start Menu or `Program Files`
3. Import `.vpn` or `.conf`
4. Connect

Installer behavior:

- default target is still `Program Files\YourVpnClient`
- the user can change the install folder in the MSI wizard
- `Create a desktop shortcut` is enabled by default
- the shortcut now targets the current user desktop through `DesktopFolder`
- the shortcut preference is stored and reused on future MSI upgrades

If you want a portable test bundle instead of MSI:

1. Extract:
   - `artifacts/client-publish/VpnClient-win-x64.zip`
2. Run:
   - `VpnClient.UI.exe`

The MSI path is the preferred product path because it preserves elevation, runtime asset layout, and per-machine installation semantics.

## 10. Self-Update

The desktop client now has a first production update path:

- the UI reads `Updates:ManifestUrl`
- it checks a hosted JSON manifest
- it downloads a newer MSI into `%LocalAppData%/YourVpnClient/Updates`
- it verifies SHA-256 and optionally the signer thumbprint
- it launches `VpnClient.Updater.exe`
- the updater waits for the UI process to exit, runs `msiexec`, then relaunches the app

The current repo default now points to the live origin:

- `https://vpn.udni.ru/vpn-client/stable/update-manifest.json`

See:

- [appsettings.json](/c:/Users/rrese/source/repos/vpn/UI/appsettings.json)
- [JsonManifestAppUpdateService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Updates/JsonManifestAppUpdateService.cs)
- [Program.cs](/c:/Users/rrese/source/repos/vpn/Updater/Program.cs)
- [update-strategy.md](/c:/Users/rrese/source/repos/vpn/docs/update-strategy.md)

Current hosted origin:

- intended HTTPS URL: `https://vpn.udni.ru/vpn-client/stable/update-manifest.json`
- backing server: `37.1.197.163`
- HTTPS is live through Let's Encrypt on `vpn.udni.ru`
