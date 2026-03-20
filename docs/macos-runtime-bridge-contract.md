# macOS Runtime Bridge Contract

This document is the source of truth for the Phase 2 macOS runtime scaffold.

The contract must stay aligned across:

- the desktop macOS runtime adapter in `.NET`
- the native bridge helper under `native/macos/bridge`
- the packet tunnel extension under `native/macos/packet-tunnel`

## Transport

- Transport: Unix domain socket
- Encoding: UTF-8 JSON
- Framing: one JSON object per line
- Default socket filename: `etoVPN.runtime.sock`
- Default socket path: the active user's temp directory plus the socket filename

Examples:

- `.NET`: `Path.Combine(Path.GetTempPath(), "etoVPN.runtime.sock")`
- Swift: `FileManager.default.temporaryDirectory.appendingPathComponent("etoVPN.runtime.sock")`

## Envelope

Every desktop request uses a stable request envelope:

```json
{
  "id": "9d7e7f2d-4a3c-4df8-9e3d-1ef93f5af73f",
  "type": "request",
  "command": "status",
  "payload": {}
}
```

Bridge responses should mirror that shape:

```json
{
  "id": "9d7e7f2d-4a3c-4df8-9e3d-1ef93f5af73f",
  "type": "response",
  "ok": true,
  "payload": {
    "connected": true,
    "state": "connected"
  }
}
```

If `ok` is `false`, the response should include an `error` object:

```json
{
  "id": "9d7e7f2d-4a3c-4df8-9e3d-1ef93f5af73f",
  "type": "response",
  "ok": false,
  "error": {
    "code": "not_implemented",
    "message": "Packet tunnel activation is not implemented.",
    "details": null
  }
}
```

## Commands

The desktop client now reserves this command surface:

- `hello`
- `health`
- `configure`
- `activate`
- `deactivate`
- `status`
- `logs`
- `quit`

`configure`, `activate`, `deactivate`, and `status` are required for the macOS
MVP. The rest are scaffolded now so the protocol does not drift later.

## Payloads

### `hello`

```json
{
  "client": "etoVPN.Desktop",
  "clientVersion": "0.1.9",
  "platform": "macos"
}
```

### `configure` / `activate`

The desktop client sends the same profile payload for both commands.

```json
{
  "profileId": "guid",
  "profileName": "Frankfurt",
  "sourceFormat": "AmneziaVpn",
  "sourceFileName": "frankfurt.vpn",
  "endpoint": "45.136.49.191:443",
  "address": "10.8.1.2/32",
  "dns": ["1.1.1.1", "1.0.0.1"],
  "mtu": 1280,
  "allowedIps": ["0.0.0.0/0", "::/0"],
  "publicKey": "server-public-key",
  "presharedKey": null,
  "privateKey": "client-private-key",
  "rawConfig": "raw wireguard config",
  "rawPackageJson": "{...}",
  "tunnelConfig": {
    "format": "AmneziaVpn",
    "address": "10.8.1.2/32",
    "dns": ["1.1.1.1", "1.0.0.1"],
    "mtu": 1280,
    "allowedIps": ["0.0.0.0/0", "::/0"],
    "endpoint": "45.136.49.191:443",
    "publicKey": "server-public-key",
    "presharedKey": null,
    "persistentKeepalive": null,
    "interfaceValues": {},
    "peerValues": {},
    "awgValues": {}
  },
  "managedProfile": {
    "accountId": "guid",
    "accountEmail": "user@example.com",
    "deviceId": "guid",
    "accessGrantId": "guid",
    "nodeId": "guid",
    "controlPlaneAccessId": "guid",
    "configFormat": "amnezia-vpn"
  }
}
```

The bridge should treat `configure` as a staging step and `activate` as the
step that hands control to the packet tunnel lifecycle.

### `deactivate`

```json
{
  "profileId": "guid"
}
```

The payload is optional. If present, it identifies the profile currently being
shut down.

### `status`

The minimum status payload should expose:

```json
{
  "connected": true,
  "state": "connected",
  "profileId": "guid",
  "profileName": "Frankfurt",
  "serverEndpoint": "45.136.49.191:443",
  "deviceIpv4Address": "10.8.1.2/32",
  "deviceIpv6Address": null,
  "dns": ["1.1.1.1", "1.0.0.1"],
  "mtu": 1280,
  "allowedIps": ["0.0.0.0/0", "::/0"],
  "routes": ["0.0.0.0/0", "::/0"],
  "rxBytes": 512,
  "txBytes": 128,
  "latestHandshakeAtUtc": "2026-03-20T10:00:00Z",
  "warnings": [],
  "lastError": null
}
```

Desktop lookup order for compatibility:

- status: `status` -> `state` -> `connected`
- endpoint: `serverEndpoint` -> `serverIpv4Gateway` -> `endpoint`
- address: `deviceIpv4Address` -> `deviceIpv6Address` -> `address`
- handshake: `latestHandshakeAtUtc` -> `date`

## Native ownership split

- Bridge helper:
  owns the Unix socket, envelope decoding, request dispatch, profile staging,
  helper lifecycle, and status snapshots
- Packet tunnel extension:
  owns `NetworkExtension` lifecycle and the final network settings application
- Shared native models:
  own the typed bridge payloads and shared constants

## Target Apple runtime handoff

The current scaffold uses a staged profile file only as a temporary placeholder.
It is not the intended final Apple runtime path.

The target handoff should mirror the upstream Amnezia Apple flow:

- the bridge owns `NETunnelProviderManager`
- the bridge persists tunnel configuration into
  `NETunnelProviderProtocol.providerConfiguration`
- the packet tunnel reads `protocolConfiguration.providerConfiguration`
  on startup
- `startVPNTunnel(options:)` stays a lifecycle trigger, not the primary config
  transport

For the first real implementation, the bridge should store a UTF-8 JSON payload
under a stable provider-configuration key such as `wireguard`, derived from the
desktop `TunnelProfilePayload`.

Recommended provider-configuration keys:

- `wireguard`: `Data` containing the UTF-8 JSON-encoded `TunnelProfilePayload`
- `profileId`: `String`
- `profileName`: `String`
- `configFormat`: `String`

Recommended bridge-side lifecycle:

1. `NETunnelProviderManager.loadAllFromPreferences`
2. find/create the manager for the current `profileId`
3. populate `NETunnelProviderProtocol.providerBundleIdentifier`
4. populate `NETunnelProviderProtocol.providerConfiguration`
5. set `serverAddress`, `localizedDescription`, and `isEnabled`
6. `saveToPreferences`
7. `loadFromPreferences`
8. `startVPNTunnel(options: nil)`

Recommended status path after startup:

1. observe `NEVPNStatusDidChangeNotification`
2. map raw `NEVPNStatus` into `RuntimeTunnelState`
3. while the tunnel is active, poll `sendProviderMessage` with a JSON request
   such as `{"action":"status"}`
4. let the packet tunnel return counters and handshake timestamps

## Current Phase 2 expectation

This scaffold is not yet a real VPN runtime. Phase 2 is successful when:

1. The `.NET` client and native scaffold agree on socket path and command names.
2. The native helper shape is explicit and ready for `NetworkExtension`.
3. The desktop adapter can evolve against a stable bridge contract instead of
   inventing one later.
