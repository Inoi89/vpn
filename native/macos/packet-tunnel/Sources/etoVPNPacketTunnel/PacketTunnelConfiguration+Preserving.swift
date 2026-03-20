import Foundation

extension PacketTunnelConfiguration {
    func preservingRoutingMetadata(from previous: PacketTunnelConfiguration) -> PacketTunnelConfiguration {
        PacketTunnelConfiguration(
            profileId: profileId,
            profileName: profileName,
            format: format,
            tunnelRemoteAddress: tunnelRemoteAddress,
            splitTunnelType: previous.splitTunnelType,
            splitTunnelSites: previous.splitTunnelSites,
            interface: interface,
            peer: PacketTunnelPeerConfiguration(
                allowedIPs: peer.allowedIPs,
                splitTunnelType: previous.peer.splitTunnelType,
                splitTunnelSites: previous.peer.splitTunnelSites,
                endpoint: peer.endpoint,
                publicKey: peer.publicKey,
                presharedKey: peer.presharedKey,
                persistentKeepalive: peer.persistentKeepalive,
                peerValues: peer.peerValues,
                awgValues: peer.awgValues),
            privateKey: privateKey,
            wgQuickConfig: wgQuickConfig,
            redactedSummary: redactedSummary)
    }
}
