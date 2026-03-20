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

## Entry points

- `Sources/etoVPNMacBridge/main.swift`
  Native executable entry point.
- `Sources/etoVPNMacBridge/BridgeApplication.swift`
  Composition root for the helper.
- `Sources/etoVPNMacBridge/BridgeServer.swift`
  Socket listener placeholder.
- `Sources/etoVPNMacBridge/BridgeLineProtocol.swift`
  Newline-delimited JSON envelope decoding and dispatch skeleton.
- `Sources/etoVPNMacBridge/BridgeCommandDispatcher.swift`
  Command routing and response shaping.
- `Sources/etoVPNMacBridge/PacketTunnelCoordinator.swift`
  Native orchestration boundary that will eventually talk to
  `NETunnelProviderManager` and the packet tunnel extension.

## Runtime contract

The bridge should implement the command surface documented in:

- `docs/macos-runtime-bridge-contract.md`

Today this scaffold is intentionally non-functional. It exists to freeze the
shape of the native code and the responsibilities of each entry point before
the actual `NetworkExtension` integration is written.
