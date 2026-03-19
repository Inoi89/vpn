# Runtime Strategy

Snapshot date: `2026-03-18`

This document describes the runtime strategy actually implemented in the client.

The critical architectural decision is:

Use the external Amnezia daemon path first when it is already available on the machine, then the bundled AmneziaWG service path, and only then fall back to a raw Windows WireGuard path.

## 1. Why This Exists

The main bug class in this project is not:

- server peer issuance
- handshake visibility
- control-plane aggregation

It is:

- client runtime mismatch

The concrete failure mode we are guarding against:

- handshake happens
- UI says connected
- traffic still does not behave like upstream Amnezia

That is exactly why a plain `wg setconf` or `wg set` integration is not sufficient as the primary runtime path.

## 2. Implemented Runtime Layers

### Primary backend

[BundledAmneziaRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/BundledAmneziaRuntimeAdapter.cs)

Responsibilities:

- stage imported configs into a deterministic runtime config directory
- install tunnel services through `amneziawg.exe /installtunnelservice`
- remove tunnel services through `amneziawg.exe /uninstalltunnelservice`
- read handshake and traffic through `awg.exe show <name> dump`
- keep the clean-machine path on official AmneziaWG Windows binaries

Supporting runtime staging:

- [IAmneziaRuntimeConfigStore.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/IAmneziaRuntimeConfigStore.cs)
- [IWindowsRuntimeAssetLocator.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/IWindowsRuntimeAssetLocator.cs)

### Secondary backend

[AmneziaDaemonRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/AmneziaDaemonRuntimeAdapter.cs)

Responsibilities:

- connect to `\\.\pipe\amneziavpn`
- send `activate` JSON compatible with upstream Amnezia daemon expectations
- include:
  - `privateKey`
  - `deviceIpv4Address`
  - `deviceIpv6Address`
  - `serverPublicKey`
  - `serverPskKey`
  - `serverIpv4AddrIn`
  - `serverPort`
  - `serverIpv4Gateway`
  - `primaryDnsServer`
  - `secondaryDnsServer`
  - `allowedIPAddressRanges`
  - `excludedAddresses`
  - AWG metadata `J*`, `S*`, `H*`, `I*`
- request `status`
- parse tx/rx counters and handshake timestamp

Supporting transport:

- [IAmneziaDaemonTransport.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/IAmneziaDaemonTransport.cs)
- [NamedPipeAmneziaDaemonTransport.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/NamedPipeAmneziaDaemonTransport.cs)

### Fallback backend

[WindowsFirstVpnRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/WindowsFirstVpnRuntimeAdapter.cs)

Responsibilities:

- Wintun adapter lifecycle
- explicit `wg.exe set`
- explicit `netsh` DNS
- explicit MTU setup
- explicit route programming
- explicit `wg show dump` status parsing
- resolve bundled runtime assets from `runtime/wireguard` before falling back to machine-wide lookup

This backend is intentionally labeled as lower fidelity.

Runtime asset resolution for the fallback path lives in:

- [IWindowsRuntimeAssetLocator.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/IWindowsRuntimeAssetLocator.cs)
- [WintunService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Services/WintunService.cs)

### Selection wrapper

[HybridVpnRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/HybridVpnRuntimeAdapter.cs)

Behavior:

1. Probe external Amnezia daemon availability
2. Use daemon runtime when available
3. Otherwise probe bundled runtime availability
4. Use bundled AmneziaWG service runtime when available
5. Fall back to legacy Windows runtime otherwise
6. Preserve warnings when fallback mode is used

## 3. Runtime Semantics

### Connect

Input:

- full [ImportedServerProfile.cs](/c:/Users/rrese/source/repos/vpn/Core/Models/ImportedServerProfile.cs)

Why full profile, not plain string:

- runtime needs more than raw text
- `.vpn` import may carry richer metadata
- losing normalized fields between import and connect reintroduces the exact bug we are trying to kill

### Disconnect

The active backend is responsible for teardown:

- daemon path sends `deactivate`
- fallback path removes the adapter

### Status

The returned [ConnectionState.cs](/c:/Users/rrese/source/repos/vpn/Core/Models/ConnectionState.cs) includes:

- backend adapter name
- selected profile id/name
- endpoint
- address
- DNS list
- MTU
- AllowedIPs
- warnings
- last runtime error
- latest handshake
- rx/tx bytes
- backend mode markers

## 4. Diagnostics Coupling

[VpnDiagnosticsService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Diagnostics/VpnDiagnosticsService.cs) treats runtime as the source of truth.

The diagnostics snapshot contains:

- current connection state
- active persisted profile
- traffic stats
- import validation errors
- connection log entries

This is what the UI uses for:

- status copy
- handshake display
- traffic counters
- warning list
- error feed

## 5. Upstream Alignment

The daemon-first path is based on the actual upstream Amnezia local socket contract in the checked-in research tree:

- [localsocketcontroller.cpp](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/client/mozilla/localsocketcontroller.cpp)
- [daemon.cpp](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/client/daemon/daemon.cpp)
- [daemonlocalserverconnection.cpp](/c:/Users/rrese/source/repos/vpn/.research/amnezia-client/client/daemon/daemonlocalserverconnection.cpp)

That is the main reason this runtime is more defensible than a homegrown WireGuard-only launcher.

## 6. Current Known Risk

The remaining risk is explicit:

- if the target machine has no bundled AmneziaWG runtime and no compatible external Amnezia daemon/runtime service, the client falls back to the legacy Windows runtime path
- that legacy fallback is useful, but it is not guaranteed to match Amnezia behavior for every config/network combination

So the production recommendation is:

1. prefer the local Amnezia daemon when it already exists on the machine
2. otherwise use bundled AmneziaWG runtime
3. treat the legacy fallback runtime as compatibility mode
4. if a config shows `handshake without traffic`, reproduce on bundled or daemon path before investigating servers

## 7. Autonomous Windows Product Direction

The desktop client is no longer allowed to assume that another VPN product is already installed on the machine.

Current packaging direction:

- self-contained .NET publish
- bundled `amneziawg.exe`
- bundled `awg.exe`
- bundled `wintun.dll`
- administrator elevation
- stable app-local runtime layout under `runtime/wireguard`

Next runtime step:

- decide how much of the upstream manager-service stack to adopt in addition to the tunnel-service path

Packaging details are tracked in:

- [windows-packaging.md](/c:/Users/rrese/source/repos/vpn/docs/windows-packaging.md)

## 8. Verification

Runtime-related coverage lives in:

- [WindowsFirstVpnRuntimeAdapterTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/Runtime/WindowsFirstVpnRuntimeAdapterTests.cs)
- [AmneziaDaemonRuntimeAdapterTests.cs](/c:/Users/rrese/source/repos/vpn/Tests/Runtime/AmneziaDaemonRuntimeAdapterTests.cs)

These tests verify:

- bundled runtime installs tunnel services through official CLI
- bundled runtime reads status from `awg show dump`
- no blind `setconf`
- explicit DNS/route/MTU application in fallback mode
- status parsing from `wg show dump`
- daemon activation payload contains DNS, AllowedIPs, and AWG fields without loss
