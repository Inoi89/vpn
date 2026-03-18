# Client Architecture

Snapshot date: `2026-03-18`

This document describes the implemented desktop VPN client architecture in this repository.
The client is intentionally narrow:

1. Import `.vpn` or `.conf`
2. Persist profiles locally
3. Connect / disconnect
4. Show state, handshake, traffic, warnings, and import diagnostics

It is not a dashboard, not a peer issuer, and not a control-plane shell.

## 1. Solution Shape

The desktop client is implemented directly in the current repository as:

```text
/Core
  /Interfaces
  /Models
/Application
  /Imports
  /Profiles
/Infrastructure
  /Import
  /Persistence
  /Runtime
  /Diagnostics
  /Logging
  /Services
/UI
  /ViewModels
  /Views
/Tests
/docs
```

The project list in [VpnClient.sln](/c:/Users/rrese/source/repos/vpn/VpnClient.sln):

- [VpnClient.Core.csproj](/c:/Users/rrese/source/repos/vpn/Core/VpnClient.Core.csproj)
- [VpnClient.Application.csproj](/c:/Users/rrese/source/repos/vpn/Application/VpnClient.Application.csproj)
- [VpnClient.Infrastructure.csproj](/c:/Users/rrese/source/repos/vpn/Infrastructure/VpnClient.Infrastructure.csproj)
- [VpnClient.UI.csproj](/c:/Users/rrese/source/repos/vpn/UI/VpnClient.UI.csproj)
- [VpnClient.Tests.csproj](/c:/Users/rrese/source/repos/vpn/Tests/VpnClient.Tests.csproj)
- [VpnClient.Diagnostics.Tests.csproj](/c:/Users/rrese/source/repos/vpn/Tests/VpnClient.Diagnostics.Tests.csproj)

## 2. Core Models

The client now centers on three runtime-safe models:

- [ImportedServerProfile.cs](/c:/Users/rrese/source/repos/vpn/Core/Models/ImportedServerProfile.cs)
  - Stable local profile identity
  - Display name
  - Embedded full import result
  - Import/update timestamps
- [ImportedTunnelConfig.cs](/c:/Users/rrese/source/repos/vpn/Core/Models/ImportedTunnelConfig.cs)
  - Raw imported payload
  - Source file metadata
  - Source format marker
  - Optional raw `.vpn` package JSON
  - Normalized tunnel config
- [TunnelConfig.cs](/c:/Users/rrese/source/repos/vpn/Core/Models/TunnelConfig.cs)
  - Interface key-values
  - Peer key-values
  - Dedicated AWG metadata bag
  - Typed endpoint/address/DNS/MTU/AllowedIPs/keepalive fields

The connection runtime state lives in [ConnectionState.cs](/c:/Users/rrese/source/repos/vpn/Core/Models/ConnectionState.cs).
It carries:

- runtime status
- current profile id/name
- endpoint and address
- DNS and MTU
- route set / AllowedIPs
- handshake timestamp
- rx/tx counters
- warnings
- last runtime error
- backend mode markers

## 3. Interfaces

The important contracts are:

- [IImportService.cs](/c:/Users/rrese/source/repos/vpn/Core/Interfaces/IImportService.cs)
- [IProfileRepository.cs](/c:/Users/rrese/source/repos/vpn/Core/Interfaces/IProfileRepository.cs)
- [IVpnRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Core/Interfaces/IVpnRuntimeAdapter.cs)
- [IVpnDiagnosticsService.cs](/c:/Users/rrese/source/repos/vpn/Core/Interfaces/IVpnDiagnosticsService.cs)

These are the only interfaces the UI needs for the core product flow.

## 4. Application Layer

The application layer is deliberately thin and orchestration-oriented.

Implemented use cases:

- [ImportTunnelConfigUseCase.cs](/c:/Users/rrese/source/repos/vpn/Application/Imports/ImportTunnelConfigUseCase.cs)
- [ImportProfileUseCase.cs](/c:/Users/rrese/source/repos/vpn/Application/Profiles/ImportProfileUseCase.cs)
- [AddProfileUseCase.cs](/c:/Users/rrese/source/repos/vpn/Application/Profiles/AddProfileUseCase.cs)
- [ListProfilesUseCase.cs](/c:/Users/rrese/source/repos/vpn/Application/Profiles/ListProfilesUseCase.cs)
- [RenameProfileUseCase.cs](/c:/Users/rrese/source/repos/vpn/Application/Profiles/RenameProfileUseCase.cs)
- [DeleteProfileUseCase.cs](/c:/Users/rrese/source/repos/vpn/Application/Profiles/DeleteProfileUseCase.cs)
- [SetActiveProfileUseCase.cs](/c:/Users/rrese/source/repos/vpn/Application/Profiles/SetActiveProfileUseCase.cs)

The UI composes these use cases rather than talking directly to file parsing or filesystem code.

## 5. Infrastructure Layer

### Import

[AmneziaImportService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Import/AmneziaImportService.cs) handles:

- `.vpn` base64url decode
- Qt/qCompress-compatible zlib decode
- recursive JSON traversal for embedded tunnel config
- `.conf` section parsing
- exact AWG metadata preservation

### Persistence

[JsonProfileRepository.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Persistence/JsonProfileRepository.cs) stores profiles in:

`%AppData%/YourVpnClient/profiles.json`

Supported operations:

- add
- delete
- rename
- load
- set active

Writes are atomic through temp-file replacement.

### Runtime

The runtime is intentionally split:

- [BundledAmneziaRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/BundledAmneziaRuntimeAdapter.cs)
  - Primary clean-machine path
  - Uses bundled `amneziawg.exe`, `awg.exe`, and `wintun.dll`
  - Installs tunnel services through the official AmneziaWG CLI
  - Reads handshake and traffic through `awg.exe show ... dump`
- [AmneziaDaemonRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/AmneziaDaemonRuntimeAdapter.cs)
  - Secondary path
  - Talks to Amnezia daemon over `\\.\pipe\amneziavpn`
  - Builds activation JSON that includes DNS, MTU, routes, AllowedIPs, and AWG metadata
- [WindowsFirstVpnRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/WindowsFirstVpnRuntimeAdapter.cs)
  - Legacy fallback path
  - Uses `awg.exe`, `netsh`, and Wintun
  - Resolves bundled native/runtime files from `runtime/wireguard` first
  - Explicitly marked as lower-fidelity
- [HybridVpnRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/HybridVpnRuntimeAdapter.cs)
  - Chooses bundled runtime first
  - Then daemon path
  - Falls back only when neither higher-fidelity path is available

App-local Windows runtime asset resolution is isolated in:

- [IWindowsRuntimeAssetLocator.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/IWindowsRuntimeAssetLocator.cs)
- [WintunService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Services/WintunService.cs)
- [IAmneziaRuntimeConfigStore.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/IAmneziaRuntimeConfigStore.cs)

The named-pipe transport is isolated in:

- [IAmneziaDaemonTransport.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/IAmneziaDaemonTransport.cs)
- [NamedPipeAmneziaDaemonTransport.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/NamedPipeAmneziaDaemonTransport.cs)

### Diagnostics

[VpnDiagnosticsService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Diagnostics/VpnDiagnosticsService.cs) aggregates:

- import validation errors
- connection log entries
- live runtime status
- last handshake
- tx/rx counters
- current active profile

## 6. UI Layer

The Avalonia shell is implemented in:

- [MainWindowViewModel.cs](/c:/Users/rrese/source/repos/vpn/UI/ViewModels/MainWindowViewModel.cs)
- [MainWindow.axaml](/c:/Users/rrese/source/repos/vpn/UI/Views/MainWindow.axaml)
- [Program.cs](/c:/Users/rrese/source/repos/vpn/UI/Program.cs)

The UI behavior is:

- left sidebar with imported profiles
- empty state when no config exists
- one-click connect/disconnect for the selected profile
- inline rename and delete
- runtime summary card
- diagnostics log feed
- import error feed

The UI does not parse configs and does not manage nodes or peers.

## 7. Build And Run

Build:

```powershell
dotnet build VpnClient.sln -c Release
```

Run:

```powershell
dotnet run --project UI\VpnClient.UI.csproj
```

Self-contained Windows publish:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\client\publish-win-x64.ps1 -Configuration Release -RuntimeIdentifier win-x64 -Version 0.1.0-local -ZipPackage
```

Run tests:

```powershell
dotnet test VpnClient.sln -c Release
```

## 8. Current Known Risk

The main production risk remains runtime fidelity, not import syntax and not server provisioning.

What is already true:

- profile import preserves Amnezia metadata
- bundled runtime now ships on official AmneziaWG Windows binaries
- daemon runtime sends DNS/MTU/AllowedIPs/AWG fields to the Amnezia daemon
- legacy fallback runtime is explicit and tested

What is still not fully closed:

- if the local machine does not have bundled runtime assets and no compatible Amnezia daemon/runtime service, the client falls back to a lower-fidelity Windows path
- that legacy fallback can still hit the old class of bugs where handshake exists but traffic behavior diverges from upstream Amnezia

Operationally, the bundled service path is now the preferred runtime path on clean Windows machines.

Product-wise, the Windows package now moves toward an autonomous distribution with bundled runtime assets instead of depending on a separately installed VPN product.
