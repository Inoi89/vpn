# macOS Native Runtime

This tree owns the future macOS-native runtime for `etoVPN`.

It is intentionally isolated from the desktop C# code. The goal is to make the
bridge and packet tunnel layout explicit without touching the working Windows
client.

## Intent

The macOS port is split into three native parts:

- `bridge/`
  A user-space helper that owns the Unix domain socket expected by the desktop
  client, accepts JSON commands, stages tunnel configuration, and talks to the
  packet tunnel extension.
- `packet-tunnel/`
  The `NetworkExtension` packet tunnel target that applies the final network
  settings and runs the tunnel lifecycle.
- `shared/`
  Native-only shared models and constants used by both the bridge and the
  packet tunnel.

## Build Scaffold

The practical generator scaffold lives in:

- `native/macos/project.yml`

It defines three XcodeGen targets:

- `etoVPNMacShared`
- `etoVPNMacBridge`
- `etoVPNPacketTunnel`

The helper target uses:

- `native/macos/bridge/Info.plist`
- `native/macos/bridge/etoVPNMacBridge.entitlements`

The packet tunnel target uses:

- `native/macos/packet-tunnel/Info.plist`
- `native/macos/packet-tunnel/etoVPNPacketTunnel.entitlements`

## Quick Start

On macOS with XcodeGen installed:

```bash
cd native/macos
xcodegen generate --spec project.yml
xcodebuild -project etoVPNMac.xcodeproj -scheme etoVPNMacBridge -configuration Debug build
xcodebuild -project etoVPNMac.xcodeproj -scheme etoVPNPacketTunnel -configuration Debug build
```

Or use the repo-level helper that stages the native outputs exactly where the
desktop publish script expects them:

```bash
./native/macos/build-native.sh --configuration Release --runtime osx-arm64
```

The bridge app target embeds the packet tunnel extension, so building or
archiving `etoVPNMacBridge` is the path to a full macOS helper bundle.

The native path is still pre-smoke, but it is no longer just a placeholder:
`build-native.sh` now hydrates the upstream `amneziawg-apple` sources if
needed, builds `libwg-go.a` from `WireGuardKitGo`, and wires the packet tunnel
target to the real Apple `WireGuardAdapter` code path.

## Layout

```text
native/macos/
  bridge/
    Sources/
      etoVPNMacBridge/
  packet-tunnel/
    Sources/
      etoVPNPacketTunnel/
  shared/
    Sources/
      etoVPNMacShared/
```

## Source of truth

The bridge contract that must stay aligned with the current desktop runtime
adapter lives in:

- `docs/macos-runtime-bridge-contract.md`

This native path does not modify any existing Windows desktop/runtime behavior.
