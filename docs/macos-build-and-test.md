# macOS Build And Test

This is the shortest path from the current repo state to a real macOS smoke
test.

Current checkpoint:

- native helper build works on a real Mac
- desktop `.app` publish works
- the app launches
- auth works
- managed access issuance works
- the helper and runtime bridge work
- `Connect` reaches `NETunnelProviderManager`
- the current blocker for a real VPN session is Apple signing for
  `NetworkExtension`, not desktop logic

See also:

- [macos-current-state.md](/c:/Users/rrese/source/repos/vpn/docs/macos-current-state.md)

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

For a real VPN smoke test, ad-hoc signing is not enough. If you do not have a
real Apple developer signing context, the expected failure mode is:

`Failed to configure packet tunnel manager: permission denied`

## Native Runtime Build

From the repo root:

```bash
./native/macos/build-native.sh --configuration Release --runtime osx-arm64
```

If you have a real Apple Developer team and want the packet tunnel to pass the
`NETunnelProviderManager` permission gate, use a signed build instead:

```bash
DEVELOPMENT_TEAM=TEAMID CODE_SIGN_STYLE=Automatic ALLOW_PROVISIONING_UPDATES=1 \
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

If the native build was already done with real Apple signing, preserve that
signature during app publish:

```bash
DEVELOPMENT_TEAM=TEAMID PRESERVE_NATIVE_SIGNATURES=1 SKIP_NATIVE_BUILD=1 RUNTIME_IDENTIFIER=osx-arm64 \
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
  Xcode build with a real Apple signing context and then smoke-testing it on a
  real Mac.
- For a non-developer tester handoff, use:
  [macos-tester-handoff.md](/c:/Users/rrese/source/repos/vpn/docs/macos-tester-handoff.md)
