# Desktop Client Direction

Snapshot date: `2026-03-18`

This document captures the intended direction for the custom desktop client that will sit on top of the existing Amnezia fleet.

The immediate product goal is intentionally narrow:

- add a server from a config file;
- connect;
- disconnect;
- show state;
- avoid rebuilding server provisioning and key generation inside the desktop client.

## 1. Product Scope

The desktop app should not become a second control plane.

For the next phase, the client only needs to solve:

- local import of `.vpn` and `.conf`;
- local profile persistence;
- connection lifecycle;
- minimal connection diagnostics;
- a custom UI/brand layer.

The desktop app does **not** need to solve:

- node registration;
- peer issuance from the control plane;
- peer enable/disable/delete;
- fleet dashboard concerns.

Those remain in the control plane.

## 2. What Already Exists in This Repository

There is already a minimal desktop shell in the current repo:

- [`VpnClient.sln`](../VpnClient.sln)
- [`Core/Interfaces/IVpnService.cs`](../Core/Interfaces/IVpnService.cs)
- [`Infrastructure/Services/VpnService.cs`](../Infrastructure/Services/VpnService.cs)
- [`Infrastructure/Services/ConfigService.cs`](../Infrastructure/Services/ConfigService.cs)
- [`UI/ViewModels/MainWindowViewModel.cs`](../UI/ViewModels/MainWindowViewModel.cs)
- [`UI/Views/MainWindow.axaml`](../UI/Views/MainWindow.axaml)

Current state of that client:

- it is a thin Avalonia shell;
- it can load a local config file;
- it can call `ConnectAsync` / `DisconnectAsync`;
- it is currently much closer to a WireGuard test harness than to a production-ready Amnezia client.

Current implementation snapshot in this repo:

- [`Core/Models/ImportedProfile.cs`](../Core/Models/ImportedProfile.cs) stores the normalized local profile shape;
- [`Infrastructure/Services/ConfigService.cs`](../Infrastructure/Services/ConfigService.cs) now imports both `.conf` and `.vpn` into that local model;
- [`UI/ViewModels/MainWindowViewModel.cs`](../UI/ViewModels/MainWindowViewModel.cs) is now centered on one active imported profile, not on hardcoded file paths;
- [`UI/Views/MainWindow.axaml`](../UI/Views/MainWindow.axaml) has been reshaped into an Amnezia-like desktop shell focused on `import -> connect -> diagnostics`.

This is an intentional halfway point:

- the UI direction is now aligned with the product goal;
- the runtime is still the old thin `VpnService`, so full Amnezia parity is **not** claimed yet.

## 3. Upstream Amnezia Flow We Need To Mirror

The two most important upstream areas are:

- import path:
  - [`importController.cpp`](../.research/amnezia-client/client/ui/controllers/importController.cpp)
- connection path:
  - [`connectionController.cpp`](../.research/amnezia-client/client/ui/controllers/connectionController.cpp)
  - [`vpnconnection.cpp`](../.research/amnezia-client/client/vpnconnection.cpp)
  - [`localsocketcontroller.cpp`](../.research/amnezia-client/client/mozilla/localsocketcontroller.cpp)

The practical meaning:

- importing a config file is not just "read text and pass it through";
- Amnezia normalizes imported configs into an internal JSON model;
- the connect path then feeds that normalized model into a client runtime/daemon path that sets routes, DNS, MTU, split-tunnel settings, and AWG metadata.

This is the key reason why "config created inside Amnezia" and "third-party imported raw `.conf`" can diverge in behavior even when server-side peer creation is already correct.

## 4. Recommended Technical Direction

### 4.1 Keep The Existing Control Plane Separate

The control plane continues to:

- issue configs;
- manage nodes;
- track sessions;
- manage users and access state.

The desktop app should consume the exported config as an input artifact, not reimplement the control plane.

### 4.2 Build A Local Client Domain Model

Instead of passing raw text through the UI, define a local model such as:

- `ImportedServerProfile`
- `ImportedTunnelConfig`
- `ConnectionSessionState`

That model should represent:

- protocol type (`amnezia-vpn`, `amnezia-awg-native`);
- endpoint host/port;
- DNS;
- MTU;
- allowed IPs;
- AWG fields;
- display name;
- raw source payload for diagnostics.

### 4.3 Port The Import Logic First

The first serious step should be a C# import layer that mirrors Amnezia semantics:

- `.vpn` decoder:
  - `vpn://` -> base64url -> `qCompress` payload -> JSON
- `.conf` parser:
  - parse `[Interface]` and `[Peer]`
  - preserve `DNS`, `MTU`, `AllowedIPs`, `PersistentKeepalive`
  - preserve AWG fields `J*`, `S*`, `H*`, `I*`

This should become a dedicated service, not a UI concern.

### 4.4 Do Not Rebuild The Runtime Blindly

The current `Infrastructure/Services/VpnService.cs` is not enough for full parity with Amnezia.

Why:

- it treats config mostly as WireGuard text;
- it strips and applies config through `wg.exe setconf`;
- it does not model Amnezia import/runtime semantics deeply enough;
- it is Windows-centric and still too thin for a cross-node Amnezia-compatible client.

The safest next move is:

- reuse Amnezia runtime behavior where possible;
- put the custom product effort into UI and local profile UX;
- avoid inventing a second tunnel runtime unless we deliberately choose that cost.

## 5. Recommended Architecture For Our Custom Client

### Option A. Custom UI + Reused Amnezia Runtime

This is the recommended path.

Shape:

- Avalonia UI shell in `UI/`
- C# application/services layer in `Core/` and `Infrastructure/`
- import service that produces normalized connection objects
- adapter that feeds those objects into the existing runtime path compatible with Amnezia semantics

Pros:

- highest compatibility with existing `.vpn` / `.conf` behavior
- less tunnel/runtime risk
- fastest path to a usable internal desktop app

Cons:

- coupling to Amnezia runtime behavior
- packaging may be less clean than a ground-up runtime

### Option B. Custom UI + Custom Tunnel Runtime

This is possible, but high risk.

It means we would own:

- import parsing;
- route management;
- DNS setup;
- MTU behavior;
- AWG-specific runtime semantics;
- platform-specific tunnel handling.

Pros:

- full ownership
- cleaner product boundary in the long run

Cons:

- this is materially more expensive
- it increases the risk of exactly the compatibility issues we are debugging right now

For the current product goal, Option B is the wrong first move.

## 6. What To Build Next

### Milestone 1. Import-Only Desktop Shell

Deliver:

- open `.vpn` or `.conf` from disk
- validate and normalize config
- persist profile locally
- show one-card profile view with `Connect` / `Disconnect`

### Milestone 2. Runtime Adapter

Deliver:

- connect using the normalized imported profile
- surface connection state changes
- show endpoint, handshake, bytes, and last error

### Milestone 3. Multiple Saved Profiles

Deliver:

- profile list
- recent/primary profile
- delete/rename local profile

### Milestone 4. Diagnostics

Deliver:

- import validation errors
- connection logs
- resolved DNS/MTU/allowed IPs snapshot

## 7. Immediate Codebase Plan

The best next implementation step in this repo is:

1. add a desktop-client import service under `Core` / `Infrastructure`;
2. define a local normalized config model;
3. replace `ConfigService` file-path assumptions with a file-import workflow;
4. reshape `MainWindowViewModel` around saved profile + connect/disconnect;
5. leave control-plane issuance outside this desktop app.

## 8. Non-Negotiable Constraint

The current open issue with config generation/import must remain visible:

- server-side provisioning is no longer the primary suspect;
- the remaining risk is client import/runtime compatibility;
- the desktop app must therefore be designed around Amnezia-compatible import semantics first, not around a simplistic "just pass the file into WireGuard" assumption.
