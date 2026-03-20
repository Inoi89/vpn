import Foundation
import NetworkExtension

enum PacketTunnelNetworkSettingsBuilder {
    static func build(from configuration: PacketTunnelConfiguration) throws -> NEPacketTunnelNetworkSettings {
        let settings = NEPacketTunnelNetworkSettings(tunnelRemoteAddress: configuration.tunnelRemoteAddress)
        let includedRoutes = configuration.peer.effectiveAllowedIPs
        let excludedRoutes = configuration.peer.excludedIPs

        if let ipv4Settings = buildIPv4Settings(
            from: configuration.interface.addresses,
            allowedIPs: includedRoutes,
            excludedIPs: excludedRoutes)
        {
            settings.ipv4Settings = ipv4Settings
        }

        if let ipv6Settings = buildIPv6Settings(
            from: configuration.interface.addresses,
            allowedIPs: includedRoutes,
            excludedIPs: excludedRoutes)
        {
            settings.ipv6Settings = ipv6Settings
        }

        if !configuration.interface.dnsServers.isEmpty {
            settings.dnsSettings = NEDNSSettings(servers: configuration.interface.dnsServers)
        }

        if let mtu = configuration.interface.mtu {
            settings.mtu = NSNumber(value: mtu)
        }

        return settings
    }

    private static func buildIPv4Settings(
        from addresses: [String],
        allowedIPs: [String],
        excludedIPs: [String]) -> NEIPv4Settings?
    {
        let addresses = parseCidrs(addresses).filter { !$0.isIPv6 }
        guard !addresses.isEmpty else {
            return nil
        }

        let settings = NEIPv4Settings(
            addresses: addresses.map(\.address),
            subnetMasks: addresses.map { subnetMask(forPrefixLength: $0.prefixLength) })
        settings.includedRoutes = parseCidrs(allowedIPs)
            .filter { !$0.isIPv6 }
            .map { route(for: $0) }
        if !excludedIPs.isEmpty {
            settings.excludedRoutes = parseCidrs(excludedIPs)
                .filter { !$0.isIPv6 }
                .map { route(for: $0) }
        }
        return settings
    }

    private static func buildIPv6Settings(
        from addresses: [String],
        allowedIPs: [String],
        excludedIPs: [String]) -> NEIPv6Settings?
    {
        let addresses = parseCidrs(addresses).filter(\.isIPv6)
        guard !addresses.isEmpty else {
            return nil
        }

        let settings = NEIPv6Settings(
            addresses: addresses.map(\.address),
            networkPrefixLengths: addresses.map { NSNumber(value: $0.prefixLength) })
        settings.includedRoutes = parseCidrs(allowedIPs)
            .filter(\.isIPv6)
            .map { route(for: $0) }
        if !excludedIPs.isEmpty {
            settings.excludedRoutes = parseCidrs(excludedIPs)
                .filter(\.isIPv6)
                .map { route(for: $0) }
        }
        return settings
    }

    private static func parseCidrs(_ rawValues: [String]) -> [ParsedCidr] {
        rawValues.compactMap(ParsedCidr.init)
    }

    private static func subnetMask(forPrefixLength prefix: Int) -> String {
        guard prefix > 0 else {
            return "0.0.0.0"
        }

        let mask = UInt32.max << (32 - UInt32(prefix))
        let octet1 = (mask >> 24) & 0xff
        let octet2 = (mask >> 16) & 0xff
        let octet3 = (mask >> 8) & 0xff
        let octet4 = mask & 0xff
        return "\(octet1).\(octet2).\(octet3).\(octet4)"
    }

    private static func route(for cidr: ParsedCidr) -> NEIPv4Route {
        if cidr.prefixLength == 0 && cidr.address == "0.0.0.0" {
            return .default()
        }

        return NEIPv4Route(
            destinationAddress: cidr.address,
            subnetMask: subnetMask(forPrefixLength: cidr.prefixLength))
    }

    private static func route(for cidr: ParsedCidr) -> NEIPv6Route {
        if cidr.prefixLength == 0 && cidr.address == "::" {
            return .default()
        }

        return NEIPv6Route(
            destinationAddress: cidr.address,
            networkPrefixLength: NSNumber(value: cidr.prefixLength))
    }
}

private struct ParsedCidr {
    let address: String
    let prefixLength: Int
    let isIPv6: Bool

    init?(_ raw: String) {
        let parts = raw.split(separator: "/", maxSplits: 1, omittingEmptySubsequences: true)
        guard parts.count == 2,
              let prefixLength = Int(parts[1])
        else {
            return nil
        }

        self.address = String(parts[0])
        self.prefixLength = prefixLength
        self.isIPv6 = address.contains(":")
    }
}
