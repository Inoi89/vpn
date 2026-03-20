# macOS Bridge Helper

This directory contains the scaffold for the future `etoVPN` macOS helper.

The bridge is the native process that the Avalonia desktop client will talk to
over a Unix domain socket. It is responsible for all platform-specific runtime
operations that should not live in the C# UI layer.

## Responsibilities

- Own the bridge socket endpoint expected by the desktop client.
- Accept newline-delimited UTF-8 JSON requests.
- Dispatch `hello`, `health`, `configure`, `activate`, `deactivate`, `status`, `logs`, and `quit`.
- Stage tunnel configuration for the packet tunnel extension.
- Start or stop the tunnel through native macOS APIs.
- Publish a stable status snapshot back to the desktop client.
- Proxy provider-side `logs` and runtime-configuration requests through the
  tunnel manager when the packet tunnel exposes them.

## Entry points

- `Sources/etoVPNMacBridge/main.swift`
  Native executable entry point.
- `Sources/etoVPNMacBridge/BridgeApplication.swift`
  Composition root for the helper.
- `Sources/etoVPNMacBridge/BridgeServer.swift`
  Socket listener and newline-delimited request ingestion scaffold.
- `Sources/etoVPNMacBridge/BridgeCommandDispatcher.swift`
  Command routing and response shaping.
- `Sources/etoVPNMacBridge/PacketTunnelManagerStore.swift`
  `NETunnelProviderManager` load/save/start/stop scaffold.
- `Sources/etoVPNMacBridge/PacketTunnelCoordinator.swift`
  Native orchestration boundary that stages manager configuration and packet
  tunnel startup.
- `Sources/etoVPNMacBridge/TunnelManagerStatusObserver.swift`
  `NEVPNStatusDidChangeNotification` observer scaffold.
- `Sources/etoVPNMacBridge/TunnelStatusPoller.swift`
  `sendProviderMessage` status polling scaffold.

## Runtime contract

The bridge should implement the command surface documented in:

- `docs/macos-runtime-bridge-contract.md`

Today this scaffold is still not a real VPN runtime, but it now mirrors the
target Apple control path more closely: socket request handling, staged profile
handoff, canonical `wireguard` provider payloads, and
`NETunnelProviderManager` orchestration all have explicit native boundaries
ready for the real implementation.
