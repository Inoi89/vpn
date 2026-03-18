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

- primary:
  - [AmneziaDaemonRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/AmneziaDaemonRuntimeAdapter.cs)
- fallback:
  - [WindowsFirstVpnRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/WindowsFirstVpnRuntimeAdapter.cs)
- selector:
  - [HybridVpnRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/HybridVpnRuntimeAdapter.cs)

Selection rule:

- if local Amnezia daemon is available, use it
- otherwise fall back to explicit Windows runtime path

Why this matters:

- daemon path is the closest match to upstream Amnezia runtime semantics
- fallback path is useful, but lower fidelity

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

Relevant tests:

- [ImportServiceTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/ImportServiceTests.cs)
- [ProfileRepositoryTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/ProfileRepositoryTests.cs)
- [WindowsFirstVpnRuntimeAdapterTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/Runtime/WindowsFirstVpnRuntimeAdapterTests.cs)
- [AmneziaDaemonRuntimeAdapterTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/Runtime/AmneziaDaemonRuntimeAdapterTests.cs)
- [VpnDiagnosticsServiceTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/Diagnostics/VpnDiagnosticsServiceTests.cs)

What these tests prove:

- imports do not drop critical config fields
- persisted profiles keep full normalized config
- fallback runtime applies DNS/MTU/routes explicitly
- daemon payload carries AWG/DNS/AllowedIPs fields
- diagnostics snapshots are wired correctly

What these tests do not prove:

- that a real Windows machine with installed Amnezia runtime carries traffic correctly end-to-end
- that fallback runtime is behaviorally identical to upstream Amnezia

## 8. Current Open Problem

The open problem is still the same one that matters most:

- handshake may exist
- traffic may still diverge from official Amnezia behavior

Current hypothesis hierarchy:

1. best path:
   - use Amnezia daemon runtime locally
2. risk path:
   - fallback Windows runtime may still be the place where "connected but unusable" survives

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
