# Client Operational State

Snapshot date: `2026-03-18`

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

It intentionally does not support:

- peer issuance
- node list
- control plane API work
- user management on servers

## 2. Main Entry Points

Solution:

- [VpnClient.sln](/c:/Users/rrese/source/repos/vpn/VpnClient.sln)

Executable:

- [VpnClient.UI.exe](/c:/Users/rrese/source/repos/vpn/UI/bin/Release/net8.0/VpnClient.UI.exe)

Packaged portable build:

- [VpnClient-win-x64.zip](/c:/Users/rrese/source/repos/vpn/artifacts/client-publish/VpnClient-win-x64.zip)

Packaged installer:

- [YourVpnClient-0.1.0-local.msi](/c:/Users/rrese/source/repos/vpn/artifacts/client-installer/win-x64/YourVpnClient-0.1.0-local.msi)

Self-contained publish profile:

- [win-x64-selfcontained.pubxml](/c:/Users/rrese/source/repos/vpn/UI/Properties/PublishProfiles/win-x64-selfcontained.pubxml)

Windows packaging script:

- [publish-win-x64.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/publish-win-x64.ps1)
- [build-msi.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/build-msi.ps1)

UI composition root:

- [Program.cs](/c:/Users/rrese/source/repos/vpn/UI/Program.cs)

Main shell:

- [MainWindowViewModel.cs](/c:/Users/rrese/source/repos/vpn/UI/ViewModels/MainWindowViewModel.cs)
- [MainWindow.axaml](/c:/Users/rrese/source/repos/vpn/UI/Views/MainWindow.axaml)

## 3. Persistence

Profiles are stored in:

`%AppData%/YourVpnClient/profiles.json`

Repository:

- [JsonProfileRepository.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Persistence/JsonProfileRepository.cs)

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

- if bundled AmneziaWG runtime exists, use it first
- otherwise, if local Amnezia daemon is available, use it
- otherwise fall back to the legacy explicit Windows runtime path

Why this matters:

- bundled AmneziaWG service path is now the main clean-machine runtime
- daemon path is still useful for parity with an already installed Amnezia desktop stack
- legacy fallback path remains useful, but lower fidelity
- autonomous packaging now stages bundled runtime files under `runtime/wireguard` instead of assuming global PATH/system installation

## 6. Diagnostics

Diagnostics service:

- [VpnDiagnosticsService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Diagnostics/VpnDiagnosticsService.cs)

The UI surfaces:

- current runtime backend
- connect status
- latest handshake
- rx bytes
- tx bytes
- import errors
- connection logs
- runtime warnings

## 7. Verification Status

Verified locally:

- `dotnet build VpnClient.sln -c Release`
- `dotnet test VpnClient.sln -c Release`
- `powershell -ExecutionPolicy Bypass -File .\deploy\client\publish-win-x64.ps1 -Configuration Release -RuntimeIdentifier win-x64 -Version 0.1.0-local -ZipPackage`
- `powershell -ExecutionPolicy Bypass -File .\deploy\client\build-msi.ps1 -Configuration Release -RuntimeIdentifier win-x64 -Version 0.1.0-local`

Relevant tests:

- [ImportServiceTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/ImportServiceTests.cs)
- [ProfileRepositoryTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/ProfileRepositoryTests.cs)
- [WindowsFirstVpnRuntimeAdapterTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/Runtime/WindowsFirstVpnRuntimeAdapterTests.cs)
- [AmneziaDaemonRuntimeAdapterTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/Runtime/AmneziaDaemonRuntimeAdapterTests.cs)
- [VpnDiagnosticsServiceTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/Diagnostics/VpnDiagnosticsServiceTests.cs)

What these tests prove:

- imports do not drop critical config fields
- persisted profiles keep full normalized config
- bundled runtime installs and removes tunnel services through official `amneziawg.exe`
- bundled runtime reads status/traffic through official `awg.exe show ... dump`
- legacy fallback runtime still applies DNS/MTU/routes explicitly
- daemon payload carries AWG/DNS/AllowedIPs fields
- diagnostics snapshots are wired correctly

What these tests do not prove:

- that a completely clean Windows machine carries traffic correctly end-to-end using the bundled runtime
- that the legacy fallback runtime is behaviorally identical to upstream Amnezia

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

What is still not fully complete:

- the MSI still needs live validation on a truly clean Windows machine
- there is still no full upstream manager-service integration beyond the tunnel-service path
- if `runtime/wireguard` is empty, clean-machine connect still depends on external installs

See:

- [windows-packaging.md](/c:/Users/rrese/source/repos/vpn/docs/windows-packaging.md)
