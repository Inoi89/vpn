# macOS Current State

Last updated: `2026-03-21`

## What Already Works

- Native macOS helper build completes on a real Mac.
- Desktop macOS app bundle publish completes and produces `etoVPN.app`.
- The app launches on macOS.
- Product Platform auth works against `https://etovpn.com/api/`.
- Managed access issuance works.
- The desktop app can start the native helper automatically.
- The runtime bridge socket is alive and the UI now receives real runtime state changes.
- `Connect` reaches the real `NETunnelProviderManager` path instead of failing in the desktop shell.

## What Was Fixed During This Pass

- Native bridge socket server is real, not a stub.
- The desktop app now auto-starts the macOS helper from the app bundle.
- The packet tunnel bundle metadata now points to the correct executable.
- The helper no longer blocks `NetworkExtension` status callbacks on the main thread.
- macOS desktop publish now works as a signed app bundle.
- macOS app config and Product Platform endpoints load correctly from the bundle.
- macOS logging now writes to a writable user directory instead of the signed bundle.

## Current Blocker

The remaining blocker is not UI, auth, or bridge IPC.

The current blocker is Apple signing for `NetworkExtension`.

With the current ad-hoc signed build, the app reaches the packet tunnel manager
and fails with:

`Failed to configure packet tunnel manager: permission denied`

That means:

- the desktop app is alive
- the helper is alive
- the bridge is alive
- the packet tunnel manager path is reached
- macOS rejects the tunnel configuration because the build is not signed with a
  real Apple developer signing context that can use `NetworkExtension`

## What Is Needed For A Real VPN Smoke Test

On the Mac machine, the tester needs:

- Xcode
- Xcode Command Line Tools
- `.NET 8 SDK`
- `XcodeGen`
- a signed-in Xcode account
- a real Apple Developer team that can sign `NetworkExtension`
- a local `Mac Development` certificate with private key
- the real Apple `Team ID`

Without that, the app can still be built and launched, but real VPN connect is
expected to fail with `permission denied`.

## Signed Build Commands

Replace `TEAMID` with the real Apple team id, for example `AB12CDE345`.

```bash
cd ~/vpn-work
pkill -f etoVPNMacBridge || true
rm -rf artifacts/macos-native/.derived/osx-arm64
rm -rf artifacts/client-publish/osx-arm64

DEVELOPMENT_TEAM=TEAMID CODE_SIGN_STYLE=Automatic ALLOW_PROVISIONING_UPDATES=1 \
./native/macos/build-native.sh --configuration Release --runtime osx-arm64 2>&1 | tee ~/Desktop/build-native-signed.log

DEVELOPMENT_TEAM=TEAMID PRESERVE_NATIVE_SIGNATURES=1 SKIP_NATIVE_BUILD=1 RUNTIME_IDENTIFIER=osx-arm64 \
./deploy/client/publish-macos.sh 2>&1 | tee ~/Desktop/publish-macos-signed.log

open ~/vpn-work/artifacts/client-publish/osx-arm64/etoVPN.app
```

## Expected Result Of A Signed Build

If the Apple signing context is valid, the next expected milestone is:

- the app launches
- login works
- `Connect` no longer fails with `permission denied`
- macOS shows the real VPN permission / tunnel flow
- the tunnel either connects, or fails with a provider/runtime error that is
  downstream from Apple signing

## Useful Logs

Desktop app log:

```bash
cat ~/Library/Application\ Support/YourVpnClient/logs/client.log
```

macOS runtime / NetworkExtension log:

```bash
log show --last 10m --info --debug --predicate 'subsystem == "com.apple.NetworkExtension" || process == "etoVPNMacBridge" || process == "etoVPNPacketTunnel"' --style compact
```
