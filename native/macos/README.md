# macOS Native Runtime Scaffold

This tree owns the future macOS-native runtime for `etoVPN`.

It is intentionally isolated from the desktop C# code. The goal is to make the
bridge and packet tunnel layout explicit before any real `NetworkExtension`
implementation lands.

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

This scaffold does not modify any existing desktop/runtime .NET source files.
