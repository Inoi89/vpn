# macOS Build And Test

This is the shortest path from the current repo state to a real macOS smoke
test.

## Prerequisites

On the Mac machine, install:

- .NET 8 SDK
- Xcode
- Xcode command line tools
- XcodeGen

You will also need an Apple signing context that can build a packet tunnel
extension.

## Native Runtime Build

From the repo root:

```bash
./native/macos/build-native.sh --configuration Release --runtime osx-arm64
```

This generates the Xcode project from [project.yml](/c:/Users/rrese/source/repos/vpn/native/macos/project.yml),
builds the helper app plus packet tunnel, and stages the outputs under:

`artifacts/macos-native/osx-arm64`

Expected outputs:

- `artifacts/macos-native/osx-arm64/etoVPNMacBridge.app`
- `artifacts/macos-native/osx-arm64/etoVPNMacBridge`
- `artifacts/macos-native/osx-arm64/etoVPNPacketTunnel.appex`

## Desktop Publish

From the repo root:

```bash
./deploy/client/publish-macos.sh
```

Useful overrides:

```bash
CONFIGURATION=Release \
RUNTIME_IDENTIFIER=osx-arm64 \
VERSION=0.1.9 \
DEVELOPMENT_TEAM=YOURTEAMID \
./deploy/client/publish-macos.sh
```

This publishes the Avalonia desktop client, calls the native build handoff, and
assembles:

`artifacts/client-publish/osx-arm64/etoVPN.app`

If you only want the desktop publish output without native artifacts:

```bash
SKIP_NATIVE_BUILD=1 ./deploy/client/publish-macos.sh
```

## First Smoke Checklist

1. Launch `etoVPN.app`.
2. Verify the desktop UI starts and reaches the macOS runtime bridge.
3. Import a legacy profile or log in with a managed account.
4. Trigger `Connect`.
5. Verify helper + packet tunnel are present and the bridge status changes.
6. Confirm traffic, handshake, and disconnect behavior.

## Notes

- The Windows updater/MSI path is unrelated to this flow.
- The native macOS runtime is still scaffold-heavy; the next hard dependency is
  real `WireGuardAdapter` / `amneziawg-apple` integration inside the packet
  tunnel target.
