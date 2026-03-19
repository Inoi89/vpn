# Windows Packaging

This folder contains the Windows packaging scaffold for the desktop VPN client.

Current goal:

- produce a self-contained Avalonia build
- stage app-local AmneziaWG runtime assets under `runtime/wireguard`
- package that publish layout into a real per-machine MSI installer without changing runtime paths

## Current publish entry point

Use:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\client\publish-win-x64.ps1 -Configuration Release -RuntimeIdentifier win-x64 -Version 0.1.3 -ZipPackage
```

Output:

`artifacts/client-publish/win-x64`

Zip package:

`artifacts/client-publish/VpnClient-win-x64.zip`

Bundled updater:

`artifacts/client-publish/win-x64/VpnClient.Updater.exe`

## MSI installer entry point

Use:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\client\build-msi.ps1 -Configuration Release -RuntimeIdentifier win-x64 -Version 0.1.3
```

Output:

`artifacts/client-installer/win-x64/YourVpnClient-0.1.3.msi`

## Update manifest entry point

Use:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\client\generate-update-manifest.ps1 -Version 0.1.3 -PackagePath artifacts\client-installer\win-x64\YourVpnClient-0.1.3.msi -PackageBaseUrl https://downloads.example.com/vpn-client -OutputPath artifacts\client-installer\win-x64\update-manifest.json
```

Output:

`artifacts/client-installer/win-x64/update-manifest.json`

Direct publish helper for the current update origin:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\client\publish-update-origin.ps1 -Version 0.1.3 -ServerPassword <root-password> -ReleaseNotes "0.1.3 minimal update action in desktop UI." -UploadZip
```

Current origin target:

- `vpn.udni.ru`
- `/srv/vpn-updates/vpn-client/stable`

## Expected runtime assets

Drop official Windows runtime files into:

`third_party/windows/wireguard`

Required for clean-machine runtime:

- `amneziawg.exe`
- `awg.exe`
- `wintun.dll`

## Packaging notes

The publish layout remains intentionally folder-based.

Why:

- Avalonia desktop publish is straightforward in folder mode
- app-local native assets are easier to reason about
- the MSI can package the folder as-is without changing client code or runtime asset resolution

The MSI build now:

- republishes the requested self-contained payload
- harvests the publish folder with WiX
- marks harvested components as 64-bit
- installs the app per-machine under `Program Files` by default
- lets the user choose a different install directory during setup
- creates a Start Menu shortcut
- offers a checked desktop shortcut option and remembers the choice for upgrades
- bundles the external updater launcher used for self-update

## Self-update notes

The desktop client does not patch files in place from the main process.

Instead it:

- checks a hosted JSON manifest
- downloads the MSI into `%LocalAppData%/YourVpnClient/Updates`
- verifies the package
- launches `VpnClient.Updater.exe`
- lets that external elevated process run `msiexec`

That fits a VPN product better than trying to overwrite `Program Files` while the main UI is still running.

Current infrastructure note:

- `37.1.197.163` already serves the update payloads over HTTP
- `vpn.udni.ru` now serves the update payloads over HTTPS
