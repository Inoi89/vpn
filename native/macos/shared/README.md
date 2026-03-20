# macOS Shared Models

This directory contains native-only shared models used by both the bridge and
the packet tunnel.

## Responsibilities

- Hold the bridge command and payload types.
- Freeze the helper-to-extension profile shape.
- Expose constants that must stay aligned with the desktop macOS runtime
  adapter.

## Files

- `Sources/etoVPNMacShared/RuntimeBridgeCommand.swift`
- `Sources/etoVPNMacShared/RuntimeBridgePayloads.swift`
- `Sources/etoVPNMacShared/TunnelProfilePayload.swift`
- `Sources/etoVPNMacShared/RuntimeBridgeConstants.swift`
- `Sources/etoVPNMacShared/TunnelProviderMessage.swift`

The desktop-facing bridge contract is documented in:

- `docs/macos-runtime-bridge-contract.md`
