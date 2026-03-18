# Desktop Client Direction

Snapshot date: `2026-03-18`

This document captures the current product direction for the standalone desktop VPN client in this repository.

The scope stays intentionally narrow:

- import config from `.vpn` or `.conf`
- store profiles locally
- connect / disconnect
- show connection state, handshake, traffic, warnings, and diagnostics

The desktop client must not become a second control plane.

## 1. Product Boundary

What the desktop client does:

- local config import
- local profile persistence
- local connection lifecycle
- local diagnostics
- custom UI/UX on top of existing Amnezia-compatible configs

What the desktop client does not do:

- peer issuance
- user creation on servers
- node management
- dashboard aggregation
- calling the control plane for ordinary connect flow

Those concerns remain in the server-side control plane and node agents.

## 2. Current Implemented Direction

The current client is already aligned to this product scope.

Implemented areas:

- import layer:
  - [AmneziaImportService.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Import/AmneziaImportService.cs)
- local storage:
  - [JsonProfileRepository.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Persistence/JsonProfileRepository.cs)
- bundled autonomous runtime:
  - [BundledAmneziaRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/BundledAmneziaRuntimeAdapter.cs)
- daemon runtime:
  - [AmneziaDaemonRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/AmneziaDaemonRuntimeAdapter.cs)
- legacy fallback runtime:
  - [WindowsFirstVpnRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/WindowsFirstVpnRuntimeAdapter.cs)
- runtime selector:
  - [HybridVpnRuntimeAdapter.cs](/c:/Users/rrese/source/repos/vpn/Infrastructure/Runtime/HybridVpnRuntimeAdapter.cs)
- Avalonia UI shell:
  - [MainWindowViewModel.cs](/c:/Users/rrese/source/repos/vpn/UI/ViewModels/MainWindowViewModel.cs)
  - [MainWindow.axaml](/c:/Users/rrese/source/repos/vpn/UI/Views/MainWindow.axaml)

This is no longer the old `ConfigService + VpnService` prototype path.
That earlier direction has been superseded.

## 3. The Core Design Decision

The central decision is:

Use the bundled AmneziaWG tunnel-service path first whenever it is packaged with the app.

Why:

- the known bug is not primarily in server-side provisioning anymore
- the known bug lives in client import/runtime semantics
- handshake can succeed while traffic still behaves differently from upstream Amnezia

So the client now prefers:

1. bundled AmneziaWG service path
2. Amnezia daemon path
3. legacy Windows WireGuard fallback path only when neither of the above exists

That remains the runtime selection rule inside the app.

But the product direction has now widened from "works on a machine that already has VPN runtime pieces" to:

- ship a self-contained Windows build
- ship app-local runtime assets
- stop depending on a preinstalled Amnezia/WireGuard desktop app for basic execution

That is the only defensible direction if the goal is to make traffic really work, not just show handshake.

## 4. What "Success" Means For This Client

The desktop client is only successful if:

- the user imports `.vpn` or `.conf`
- clicks connect once
- gets a real working tunnel
- sees handshake and traffic counters
- can do this on a clean Windows machine from a packaged build

If handshake exists but actual browsing or DNS still fails, the client is not considered correct yet.

## 5. Build / Run

Solution:

- [VpnClient.sln](/c:/Users/rrese/source/repos/vpn/VpnClient.sln)

Local binary:

- [VpnClient.UI.exe](/c:/Users/rrese/source/repos/vpn/UI/bin/Release/net8.0/VpnClient.UI.exe)

Commands:

```powershell
dotnet build VpnClient.sln -c Release
dotnet test VpnClient.sln -c Release
dotnet run --project UI\VpnClient.UI.csproj
```

Self-contained Windows packaging:

- [win-x64-selfcontained.pubxml](/c:/Users/rrese/source/repos/vpn/UI/Properties/PublishProfiles/win-x64-selfcontained.pubxml)
- [publish-win-x64.ps1](/c:/Users/rrese/source/repos/vpn/deploy/client/publish-win-x64.ps1)
- [windows-packaging.md](/c:/Users/rrese/source/repos/vpn/docs/windows-packaging.md)

## 6. What To Test Next

The next tests should focus on the exact historical failure mode.

Priority scenarios:

1. Import `.vpn` exported by official Amnezia and connect through daemon path.
2. Import raw `.conf` exported by official Amnezia and connect through daemon path.
3. Repeat both cases with daemon unavailable, so the client drops to fallback runtime.
4. For each case verify:
   - connect state
   - latest handshake
   - rx/tx counters
   - DNS resolution
   - actual web traffic
   - behavior on reconnect

The important observation to capture:

- does the failure reproduce only on legacy fallback runtime
- or does it still reproduce even on bundled/daemon runtime

That answer determines whether the remaining bug is in our client orchestration or deeper in config/runtime compatibility.

## 7. Current Known Risk

The remaining risk is explicit and should stay visible in every client discussion:

- daemon path is the preferred production path
- fallback path exists for compatibility and development
- fallback path is not guaranteed to be behaviorally identical to upstream Amnezia on every config/network combination

In short:

The architecture is now pointed at the right problem, but the product is not done until the packaged Windows build carries its own runtime dependencies and survives clean-machine traffic tests.
