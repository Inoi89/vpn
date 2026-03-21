# macOS Build And Test

This is the shortest path from the current repo state to a real macOS smoke
test.

## Prerequisites

On the Mac machine, install:

- .NET 8 SDK
- Xcode
- Xcode command line tools
- XcodeGen
- Go 1.25+ only if you are building without the repo's hydrated `.research`
  folder or without the prebuilt `libwg-go.a`

You will also need an Apple signing context that can build a packet tunnel
extension.

## Native Runtime Build

From the repo root:

```bash
./native/macos/build-native.sh --configuration Release --runtime osx-arm64
```

This generates the Xcode project from [project.yml](/c:/Users/rrese/source/repos/vpn/native/macos/project.yml),
hydrates the upstream `amneziawg-apple` sources if needed, prefers the repo's
prebuilt macOS `libwg-go.a` when present, otherwise falls back to building it
from `WireGuardKitGo`, then builds the helper app plus packet tunnel and stages
the outputs under:

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
- The macOS packet tunnel is now wired to the real Apple `WireGuardAdapter`
  path from `amneziawg-apple`.
- If you hand the Mac tester a full repo snapshot that already includes
  `.research/amnezia-client`, they usually do not need `git` and do not need
  `go` just for the first smoke build.
- If Gatekeeper blocks the unsigned app bundle, the shortest workaround for a
  smoke test is:

  ```bash
  xattr -dr com.apple.quarantine artifacts/client-publish/<runtime>/etoVPN.app
  open artifacts/client-publish/<runtime>/etoVPN.app
  ```

- The remaining blocker is no longer engine integration, but running the native
  Xcode build and smoke-testing it on a real Mac with entitlements/signing.
- For a non-developer tester handoff, use:
  [macos-tester-handoff.md](/c:/Users/rrese/source/repos/vpn/docs/macos-tester-handoff.md)
