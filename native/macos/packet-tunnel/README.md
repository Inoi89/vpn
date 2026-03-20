# Packet Tunnel Extension

This directory now contains the first real macOS packet tunnel path for
`etoVPN`.

The packet tunnel is the `NetworkExtension` component that owns the actual
WireGuard/AWG lifecycle on macOS. The bridge/helper stages configuration,
starts or stops the extension through `NETunnelProviderManager`, and polls
provider-side status.

## Responsibilities

- Receive a staged tunnel profile from the bridge.
- Read that staged profile from the shared control-store location.
- Build a canonical packet-tunnel configuration object from the shared profile payload.
- Preserve optional split-tunnel mode and site lists through the canonical
  config so the WireGuard runtime receives correct include/exclude routes.
- Prefer `NETunnelProviderProtocol.providerConfiguration` as the primary
  startup handoff.
- Decode a real Apple `TunnelConfiguration` from the canonical `wg-quick`
  payload.
- Start and stop the actual WireGuard/AWG tunnel engine.
- Expose a provider-side runtime-configuration/debug surface that can later map
  to the real engine runtime configuration.
- Accept in-place `wg-quick` updates through `handleAppMessage` so the bridge
  can prefer hot reconfiguration over a full tunnel restart.
- Emit status, counters, and handshake information that the bridge can publish
  back to the desktop client.
- Answer bridge-side `sendProviderMessage` status requests with a compact JSON
  snapshot.

## Entry points

- `Sources/etoVPNPacketTunnel/PacketTunnelProvider.swift`
  Extension lifecycle entry point.
- `Sources/etoVPNPacketTunnel/PacketTunnelConfiguration.swift`
  Canonical packet-tunnel configuration models and builder from the shared
  profile payload.
- `Sources/etoVPNPacketTunnel/TunnelConfiguration.swift`
  Temporary staged profile store and provider-configuration decoder.
- `Sources/etoVPNPacketTunnel/PacketTunnelNetworkSettingsBuilder.swift`
  Legacy manual network-settings builder kept for reference while the real
  Apple adapter path settles.
- `Sources/etoVPNPacketTunnel/TunnelRuntimeSnapshot.swift`
  Provider-side runtime snapshot surfaced through `handleAppMessage`.
- `Sources/etoVPNPacketTunnel/WireGuardTunnelAdapter.swift`
  Thin boundary over the Apple WireGuard runtime engine.
- `Sources/etoVPNPacketTunnel/PacketTunnelEngine.swift`
  Engine protocol modeled after the Apple WireGuardKit/AWG boundary.
- `Sources/etoVPNPacketTunnel/WireGuardAdapterEngine.swift`
  Real Apple `WireGuardAdapter`-backed engine entry point.
- `Sources/etoVPNPacketTunnel/WireGuardTunnelConfigurationFactory.swift`
  Narrow bridge between our canonical packet-tunnel config and the upstream
  Apple `TunnelConfiguration`.
- `Sources/etoVPNPacketTunnel/WireGuardRuntimeConfigurationParser.swift`
  Parses adapter runtime counters/handshake data and redacts sensitive fields
  before surfacing them through the bridge.
- `Info.plist`
  Packet tunnel bundle metadata.
- `etoVPNPacketTunnel.entitlements`
  NetworkExtension and app-group entitlements scaffold for the generated Xcode
  project.
