# Packet Tunnel Extension

This directory contains the scaffold for the future macOS packet tunnel target.

The packet tunnel is the `NetworkExtension` component that will eventually own
the real macOS tunnel lifecycle. The bridge/helper will stage configuration and
request the system to start or stop this extension.

## Responsibilities

- Receive a staged tunnel profile from the bridge.
- Read that staged profile from the shared control-store location.
- Build a canonical packet-tunnel configuration object from the shared profile payload.
- Preserve optional split-tunnel mode and site lists through the canonical
  config so the network settings builder can apply include/exclude routes.
- Prefer `NETunnelProviderProtocol.providerConfiguration` as the primary
  startup handoff.
- Materialize DNS, routes, MTU, and addresses into
  `NEPacketTunnelNetworkSettings` from the canonical config object.
- Keep a redacted canonical `wg-quick` summary ready for the future native
  WireGuard/AWG engine boundary.
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
  Scaffold builder for `NEPacketTunnelNetworkSettings`.
- `Sources/etoVPNPacketTunnel/TunnelRuntimeSnapshot.swift`
  Provider-side runtime snapshot surfaced through `handleAppMessage`.
- `Sources/etoVPNPacketTunnel/WireGuardTunnelAdapter.swift`
  Placeholder boundary where the tunnel engine integration should live.
- `Sources/etoVPNPacketTunnel/PacketTunnelEngine.swift`
  Engine protocol modeled after the future WireGuardKit/AWG boundary.
- `Sources/etoVPNPacketTunnel/ScaffoldWireGuardEngine.swift`
  Temporary no-op engine that preserves the intended lifecycle surface.
- `Info.plist`
  Packet tunnel bundle metadata.
