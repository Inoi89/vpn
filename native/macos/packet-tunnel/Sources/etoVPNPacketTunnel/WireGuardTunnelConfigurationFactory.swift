import Foundation

enum WireGuardTunnelConfigurationFactory {
    static func build(from configuration: PacketTunnelConfiguration) throws -> TunnelConfiguration {
        let tunnelConfiguration = try TunnelConfiguration(
            fromWgQuickConfig: configuration.wgQuickConfig,
            called: configuration.profileName)

        guard !tunnelConfiguration.peers.isEmpty else {
            throw PacketTunnelEngineError.invalidConfiguration("The packet tunnel profile does not contain a peer.")
        }

        if configuration.peer.splitTunnelType == 1 && !configuration.peer.splitTunnelSites.isEmpty {
            tunnelConfiguration.peers[0].allowedIPs = try parseAddressRanges(
                configuration.peer.splitTunnelSites,
                label: "split tunnel include route")
        } else if configuration.peer.splitTunnelType == 2 && !configuration.peer.splitTunnelSites.isEmpty {
            tunnelConfiguration.peers[0].excludeIPs = try parseAddressRanges(
                configuration.peer.splitTunnelSites,
                label: "split tunnel exclude route")
        }

        return tunnelConfiguration
    }

    private static func parseAddressRanges(_ values: [String], label: String) throws -> [IPAddressRange] {
        try values.map { value in
            let normalizedValue = value.trimmingCharacters(in: .whitespacesAndNewlines)
            guard let addressRange = IPAddressRange(from: normalizedValue) else {
                throw PacketTunnelEngineError.invalidConfiguration("The packet tunnel profile contains an invalid \(label) '\(value)'.")
            }

            return addressRange
        }
    }
}
