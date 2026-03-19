# Client Operational State

Snapshot date: `2026-03-19`

This document is the working state file for the desktop VPN client.
It is meant to answer four practical questions quickly:

1. what exists
2. what runs
3. what is verified
4. what is still risky

## 1. Current Scope

The desktop client currently supports:

- import `.vpn`
- import `.conf`
- product account login
- local account session restore
- local device identity generation
- managed device enrollment
- managed access issuance and local profile save
- local profile storage
- profile rename
- profile delete
- active profile selection
- connect
- disconnect
- connection state display
- handshake display
- traffic counters
- import diagnostics
- runtime warnings
- MSI-based self-update checks
- MSI-based in-place upgrade from an existing installation
- tray-based connect/disconnect/update/restore actions
- single-instance activation and restore from a second launch

It intentionally does not support:

- peer issuance
- node list
- control plane API work
- user management on servers
- self-service billing

## 2. Main Entry Points

Solution:

- [VpnClient.sln](/c:/Users/rrese/source/repos/vpn/VpnClient.sln)

Executable:

- [VpnClient.UI.exe](/c:/Users/rrese/source/repos/vpn/UI/bin/Release/net8.0/VpnClient.UI.exe)

Packaged portable build:

- [VpnClient-win-x64.zip](/c:/Users/rrese/source/repos/vpn/artifacts/client-publish/VpnClient-win-x64.zip)

Packaged installer:

- [YourVpnClient-0.1.7.msi](/c:/Users/rrese/source/repos/vpn/artifacts/client-installer/win-x64/YourVpnClient-0.1.7.msi)

Self-contained publish profile:

- [win-x64-selfcontained.pubxml](/c:/Users/rrese/source/repos/vpn/UI/Properties/PublishProfiles/win-x64-selfcontained.pubxml)

Windows packaging script:

- [publish-win-x64.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/publish-win-x64.ps1)
- [build-msi.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/build-msi.ps1)
- [generate-update-manifest.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/generate-update-manifest.ps1)

UI composition root:

- [Program.cs](/c:/Users/rrese/source/repos/vpn/UI/Program.cs)

Main shell:

- [MainWindowViewModel.cs](/c:/Users/rrese/source/repos/vpn/UI/ViewModels/MainWindowViewModel.cs)
- [MainWindow.axaml](/c:/Users/rrese/source/repos/vpn/UI/Views/MainWindow.axaml)

Updater launcher:

- [Program.cs](/c:/Users/rrese/source/repos/vpn/Updater/Program.cs)

Future product layer:

- [mvp-platform-plan.md](/c:/Users/rrese/source/repos/vpn/docs/mvp-platform-plan.md)

Update strategy:

- [update-strategy.md](/c:/Users/rrese/source/repos/vpn/docs/update-strategy.md)

## 3. Persistence

Profiles are stored in:

`%AppData%/YourVpnClient/profiles.json`

Product account session is stored in:

`%AppData%/YourVpnClient/account-session.json`

Repository:

- [JsonProfileRepository.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Persistence/JsonProfileRepository.cs)
- [JsonProductPlatformAuthService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Auth/JsonProductPlatformAuthService.cs)

Important behavior:

- first imported profile becomes active
- active profile can be switched
- delete removes local profile only
- profile persistence keeps full normalized import result, not a reduced summary

## 4. Import

Import implementation:

- [AmneziaImportService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Import/AmneziaImportService.cs)

Guaranteed preserved fields:

- endpoint
- address
- DNS
- MTU
- AllowedIPs
- PersistentKeepalive
- PublicKey
- PresharedKey
- AWG metadata `J*`, `S*`, `H*`, `I*`

Output models:

- [ImportedTunnelConfig.cs](/c:/Users/rrese/source/repos/vpn/Core/Models/ImportedTunnelConfig.cs)
- [ImportedServerProfile.cs](/c:/Users/rrese/source/repos/vpn/Core/Models/ImportedServerProfile.cs)
- [TunnelConfig.cs](/c:/Users/rrese/source/repos/vpn/Core/Models/TunnelConfig.cs)

## 5. Runtime

Runtime stack:

- primary autonomous backend:
  - [BundledAmneziaRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/BundledAmneziaRuntimeAdapter.cs)
- secondary external-backend path:
  - [AmneziaDaemonRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/AmneziaDaemonRuntimeAdapter.cs)
- legacy fallback:
  - [WindowsFirstVpnRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/WindowsFirstVpnRuntimeAdapter.cs)
- Windows runtime asset locator:
  - [IWindowsRuntimeAssetLocator.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/IWindowsRuntimeAssetLocator.cs)
- runtime config staging:
  - [IAmneziaRuntimeConfigStore.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/IAmneziaRuntimeConfigStore.cs)
- selector:
  - [HybridVpnRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/HybridVpnRuntimeAdapter.cs)

Selection rule:

- if a local Amnezia daemon is already available, use it first
- otherwise, if bundled AmneziaWG runtime exists, use it
- otherwise fall back to the legacy explicit Windows runtime path
- on startup, the client now tries to restore an already running local tunnel and map it back to a stored profile

Why this matters:

- daemon path is now preferred on machines where official Amnezia is already installed
- bundled AmneziaWG service path remains the main clean-machine runtime
- legacy fallback path remains useful, but lower fidelity
- autonomous packaging now stages bundled runtime files under `runtime/wireguard` instead of assuming global PATH/system installation

## 6. Diagnostics

Diagnostics service:

- [VpnDiagnosticsService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Diagnostics/VpnDiagnosticsService.cs)

The UI is intentionally minimal now.

The visible shell surfaces only:

- login/register entry for managed users
- import when no profile exists
- one primary connect or disconnect action
- current connection label
- selected server

The current shell is now split into two UX paths:

- `legacy`
  - manual `.vpn` / `.conf` import
  - one-tap connect
  - no forced account requirement
- `managed`
  - product account icon in the header
  - login form and external registration path to the personal cabinet
  - local account session restore across app restarts
  - stable local device fingerprint
  - direct device registration in `Product Platform`
  - direct managed access issuance from `Product Platform`
  - returned `.vpn` payload imported and saved locally without manual file handling

Important product rule:

- existing manually imported profiles must continue to work after client updates
- account login is additive, not a breaking prerequisite for legacy users

Diagnostics and connection logging no longer live in the visible window.
They are written to the local log file near the app runtime instead.

## 7. Verification Status

Verified locally:

- `dotnet build VpnClient.sln -c Release`
- `dotnet test VpnClient.sln -c Release`
- managed-profile repository upsert for the same account/device
- `powershell -ExecutionPolicy Bypass -File .\deploy\client\publish-win-x64.ps1 -Configuration Release -RuntimeIdentifier win-x64 -Version 0.1.7 -ZipPackage`
- `powershell -ExecutionPolicy Bypass -File .\deploy\client\build-msi.ps1 -Configuration Release -RuntimeIdentifier win-x64 -Version 0.1.7`

Relevant tests:

- [ImportServiceTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/ImportServiceTests.cs)
- [ProfileRepositoryTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/ProfileRepositoryTests.cs)
- [WindowsFirstVpnRuntimeAdapterTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/Runtime/WindowsFirstVpnRuntimeAdapterTests.cs)
- [AmneziaDaemonRuntimeAdapterTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/Runtime/AmneziaDaemonRuntimeAdapterTests.cs)
- [VpnDiagnosticsServiceTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/Diagnostics/VpnDiagnosticsServiceTests.cs)
- [AppVersionParserTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/Updates/AppVersionParserTests.cs)

What these tests prove:

- imports do not drop critical config fields
- persisted profiles keep full normalized config
- bundled runtime installs and removes tunnel services through official `amneziawg.exe`
- bundled runtime reads status/traffic through official `awg.exe show ... dump`
- bundled runtime can reattach to an already running local tunnel service after app restart
- legacy fallback runtime still applies DNS/MTU/routes explicitly
- daemon payload carries AWG/DNS/AllowedIPs fields
- diagnostics snapshots are wired correctly
- update version comparison handles prerelease suffixes sanely

What these tests do not prove:

- that a completely clean Windows machine carries traffic correctly end-to-end using the bundled runtime
- that the legacy fallback runtime is behaviorally identical to upstream Amnezia
- that the desktop UI managed-enrollment flow has been exercised end-to-end by a human on a packaged build

## 8. Current Open Problem

The open problem is still the same one that matters most:

- handshake may exist
- traffic may still diverge from official Amnezia behavior

Current hypothesis hierarchy:

1. best path:
   - use bundled AmneziaWG runtime locally
2. secondary path:
   - use external Amnezia daemon runtime if it exists
3. risk path:
   - legacy Windows fallback runtime may still be the place where "connected but unusable" survives

That means the next real tests should explicitly record which backend was used:

- daemon runtime
- fallback runtime

Without that, results are ambiguous.

## 9. Test Checklist For The Traffic Bug

For every future repro, record:

- source file type:
  - `.vpn`
  - `.conf`
- profile name
- runtime backend:
  - daemon
  - fallback
- handshake timestamp
- rx bytes
- tx bytes
- DNS behavior
- browser traffic behavior
- whether Telegram/partial traffic works while browser traffic fails

Minimum matrix:

1. official Amnezia `.vpn` via daemon runtime
2. official Amnezia `.conf` via daemon runtime
3. same `.vpn` via fallback runtime
4. same `.conf` via fallback runtime

That matrix should tell us whether the remaining mismatch is:

- import-side
- runtime-side
- or specifically fallback-runtime-side

## 10. Autonomous Product Status

The client now has a real clean-machine packaging path:

- self-contained Windows publish profile exists
- publish script stages signed AmneziaWG runtime binaries into `runtime/wireguard`
- the UI manifest requests administrator elevation
- the primary clean-machine backend no longer depends on a preinstalled Amnezia or WireGuard desktop app
- a WiX-based per-machine MSI installer now builds successfully
- the MSI now exposes install-directory selection and a checked desktop-shortcut option
- the desktop-shortcut preference is stored and reused by later MSI upgrades
- the desktop client now has a JSON-manifest self-update path layered on top of the MSI

What is still not fully complete:

- the MSI still needs live validation on a truly clean Windows machine
- there is still no full upstream manager-service integration beyond the tunnel-service path
- if `runtime/wireguard` is empty, clean-machine connect still depends on external installs
- release signing and hosted manifest infrastructure still need production values

Post-`0.1.7` release note:

- the desktop window was cut down to a single-screen mobile-like flow
- the central power button is now the only primary action the user sees
- if no profile exists, that same action resolves to config import
- traffic counters, update cards, warnings, and internal runtime details are no longer visible in the main screen
- a single lightweight `Обновить` action is now shown inside the server card only when a newer release is actually available
- the MSI desktop shortcut target now uses the user desktop folder instead of the previous desktop-directory resolution path
- `.vpn` imports now materialize the real DNS and MTU values from the package instead of passing `$PRIMARY_DNS` and `$SECONDARY_DNS` placeholders into the bundled runtime
- already imported `.vpn` profiles are rematerialized again at connect time, so users do not need to reimport old packages to pick up the fix
- staged tunnel names are now human-readable instead of `yvc_<guid>` gibberish
- closing the window now hides the client to tray, and a real tray exit disconnects the active tunnel before shutdown
- when an official local Amnezia daemon is already available, the hybrid runtime now prefers it over the bundled backend instead of mixing two runtime paths on the same machine
- the tray icon now uses a real embedded shield asset instead of an invisible process icon fallback
- the tray menu now exposes `Подключить/Отключить VPN`, `Развернуть`, `Обновить`, and `Закрыть`
- update action is now surfaced from the header alert button and the tray menu instead of the server card
- the desktop window and packaged executable now embed the same shield icon
- the client now enforces a single running instance and raises the existing window from tray when launched again

Managed-enrollment development note:

- `Product Platform` now restores an existing active grant for the same account/device instead of creating duplicates
- the desktop client now imports the returned managed config directly from memory, without asking the user to save a file first
- local profile persistence now upserts managed profiles by `accountId + deviceId`, so repeated enrollment refreshes do not create duplicate local profiles

Post-`0.1.1` installer note:

- the repo now includes a source-level fix for MSI upgrade handling of the desktop shortcut preference
- the fix separates the default `INSTALLDESKTOPSHORTCUT=1` value from the persisted registry lookup, so upgrades from older installs no longer lose the default desktop shortcut selection
- this source fix is committed in the repository, but it still needs a new patch release to reach already installed `0.1.1` clients

See:

- [windows-packaging.md](/c:/Users/rrese/source/repos/vpn/docs/windows-packaging.md)
- [update-strategy.md](/c:/Users/rrese/source/repos/vpn/docs/update-strategy.md)
