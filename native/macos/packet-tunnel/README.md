# Packet Tunnel Extension

This directory contains the scaffold for the future macOS packet tunnel target.

The packet tunnel is the `NetworkExtension` component that will eventually own
the real macOS tunnel lifecycle. The bridge/helper will stage configuration and
request the system to start or stop this extension.

## Responsibilities

- Receive a staged tunnel profile from the bridge.
- Read that staged profile from the shared control-store location.
- Materialize DNS, routes, MTU, and addresses into
  `NEPacketTunnelNetworkSettings`.
- Start and stop the actual WireGuard/AWG tunnel engine.
- Emit status, counters, and handshake information that the bridge can publish
  back to the desktop client.

## Entry points

- `Sources/etoVPNPacketTunnel/PacketTunnelProvider.swift`
  Extension lifecycle entry point.
- `Sources/etoVPNPacketTunnel/TunnelConfiguration.swift`
  Placeholder typed configuration model for the staged profile.
- `Sources/etoVPNPacketTunnel/WireGuardTunnelAdapter.swift`
  Placeholder boundary where the tunnel engine integration should live.
- `Info.plist`
  Packet tunnel bundle metadata.
