import Foundation
import etoVPNMacShared

struct PacketTunnelConfiguration: Codable {
    let profileId: String
    let profileName: String
    let format: String
    let tunnelRemoteAddress: String
    let splitTunnelType: Int
    let splitTunnelSites: [String]
    let interface: PacketTunnelInterfaceConfiguration
    let peer: PacketTunnelPeerConfiguration
    let privateKey: String
    let wgQuickConfig: String
    let redactedSummary: String
}

struct PacketTunnelInterfaceConfiguration: Codable {
    let addresses: [String]
    let dnsServers: [String]
    let mtu: Int?
    let interfaceValues: [String: String]
}

struct PacketTunnelPeerConfiguration: Codable {
    let allowedIPs: [String]
    let splitTunnelType: Int
    let splitTunnelSites: [String]
    let endpoint: String?
    let publicKey: String?
    let presharedKey: String?
    let persistentKeepalive: Int?
    let peerValues: [String: String]
    let awgValues: [String: String]

    var effectiveAllowedIPs: [String] {
        if splitTunnelType == 1 && !splitTunnelSites.isEmpty {
            return splitTunnelSites
        }

        return allowedIPs
    }

    var excludedIPs: [String] {
        guard splitTunnelType == 2 else {
            return []
        }

        return splitTunnelSites
    }
}

enum PacketTunnelConfigurationBuilder {
    static func build(from profile: TunnelProfilePayload) throws -> PacketTunnelConfiguration {
        let tunnelConfig = profile.tunnelConfig
        let interfaceAddresses = parseDelimitedValues(tunnelConfig.address ?? profile.address)
        guard !interfaceAddresses.isEmpty else {
            throw PacketTunnelConfigurationError.missingAddress
        }

        let dnsServers = normalizeValues(tunnelConfig.dns.isEmpty ? profile.dns : tunnelConfig.dns)
        let mtu = tunnelConfig.mtu ?? profile.mtu
        let baseAllowedIPs = normalizeValues(tunnelConfig.allowedIps.isEmpty ? profile.allowedIps : tunnelConfig.allowedIps)
        let splitTunnelType = tunnelConfig.splitTunnelType ?? 0
        let splitTunnelSites = normalizeValues(tunnelConfig.splitTunnelSites ?? [])
        let allowedIPs = splitTunnelType == 1 && !splitTunnelSites.isEmpty ? splitTunnelSites : baseAllowedIPs
        guard !allowedIPs.isEmpty else {
            throw PacketTunnelConfigurationError.missingAllowedIps
        }

        let endpoint = tunnelConfig.endpoint ?? profile.endpoint
        guard let endpoint, !endpoint.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            throw PacketTunnelConfigurationError.missingEndpoint
        }

        let format = profile.managedProfile?.configFormat ?? profile.sourceFormat ?? tunnelConfig.format
        let privateKey = firstNonEmpty(profile.privateKey, tunnelConfig.interfaceValues["PrivateKey"])
        guard let privateKey else {
            throw PacketTunnelConfigurationError.missingPrivateKey
        }

        let publicKey = firstNonEmpty(tunnelConfig.publicKey, profile.publicKey, tunnelConfig.peerValues["PublicKey"])
        guard let publicKey else {
            throw PacketTunnelConfigurationError.missingPublicKey
        }

        let presharedKey = firstNonEmpty(tunnelConfig.presharedKey, profile.presharedKey, tunnelConfig.peerValues["PresharedKey"])
        let persistentKeepalive = tunnelConfig.persistentKeepalive
            ?? integerValue(tunnelConfig.peerValues["PersistentKeepalive"])
            ?? integerValue(tunnelConfig.peerValues["PersistentKeepaliveInterval"])
            ?? 25
        let interfaceValues = sanitizedCustomValues(
            tunnelConfig.interfaceValues,
            reservedKeys: ["Address", "DNS", "MTU", "PrivateKey"])
        let peerValues = sanitizedCustomValues(
            tunnelConfig.peerValues,
            reservedKeys: ["PublicKey", "PresharedKey", "AllowedIPs", "Endpoint", "PersistentKeepalive", "PersistentKeepaliveInterval"])
        let awgValues = sanitizedCustomValues(tunnelConfig.awgValues, reservedKeys: [])
        let wgQuickConfig = buildWgQuickConfig(
            addresses: interfaceAddresses,
            dnsServers: dnsServers,
            mtu: mtu,
            privateKey: privateKey,
            interfaceValues: interfaceValues,
            awgValues: awgValues,
            publicKey: publicKey,
            presharedKey: presharedKey,
            allowedIPs: allowedIPs,
            endpoint: endpoint,
            persistentKeepalive: persistentKeepalive,
            peerValues: peerValues)

        return PacketTunnelConfiguration(
            profileId: profile.profileId,
            profileName: profile.profileName,
            format: format,
            tunnelRemoteAddress: endpoint,
            splitTunnelType: splitTunnelType,
            splitTunnelSites: splitTunnelSites,
            interface: PacketTunnelInterfaceConfiguration(
                addresses: interfaceAddresses,
                dnsServers: dnsServers,
                mtu: mtu,
                interfaceValues: interfaceValues),
            peer: PacketTunnelPeerConfiguration(
                allowedIPs: allowedIPs,
                splitTunnelType: splitTunnelType,
                splitTunnelSites: splitTunnelSites,
                endpoint: endpoint,
                publicKey: publicKey,
                presharedKey: presharedKey,
                persistentKeepalive: persistentKeepalive,
                peerValues: peerValues,
                awgValues: awgValues),
            privateKey: privateKey,
            wgQuickConfig: wgQuickConfig,
            redactedSummary: redact(wgQuickConfig))
    }

    static func build(from providerConfiguration: WireGuardProviderConfiguration) -> PacketTunnelConfiguration {
        let dnsServers = [providerConfiguration.dns1, providerConfiguration.dns2]
            .compactMap { $0?.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
        let awgValues = providerConfiguration.awgValues()
        let wgQuickConfig = providerConfiguration.wgQuickConfig()
        let splitTunnelSites = normalizeValues(providerConfiguration.splitTunnelSites)

        return PacketTunnelConfiguration(
            profileId: providerConfiguration.profileId,
            profileName: providerConfiguration.profileName,
            format: providerConfiguration.format,
            tunnelRemoteAddress: providerConfiguration.endpointString(),
            splitTunnelType: providerConfiguration.splitTunnelType,
            splitTunnelSites: splitTunnelSites,
            interface: PacketTunnelInterfaceConfiguration(
                addresses: parseDelimitedValues(providerConfiguration.clientIP),
                dnsServers: dnsServers,
                mtu: providerConfiguration.mtu,
                interfaceValues: providerConfiguration.interfaceValues),
            peer: PacketTunnelPeerConfiguration(
                allowedIPs: providerConfiguration.allowedIPs,
                splitTunnelType: providerConfiguration.splitTunnelType,
                splitTunnelSites: splitTunnelSites,
                endpoint: providerConfiguration.endpointString(),
                publicKey: providerConfiguration.serverPublicKey,
                presharedKey: providerConfiguration.presharedKey,
                persistentKeepalive: providerConfiguration.persistentKeepAlive,
                peerValues: providerConfiguration.peerValues,
                awgValues: awgValues),
            privateKey: providerConfiguration.clientPrivateKey,
            wgQuickConfig: wgQuickConfig,
            redactedSummary: providerConfiguration.redactedSummary())
    }

    private static func parseDelimitedValues(_ raw: String?) -> [String] {
        guard let raw else {
            return []
        }

        return raw
            .split(separator: ",")
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
    }

    private static func normalizeValues(_ values: [String]) -> [String] {
        values.flatMap { parseDelimitedValues($0) }
    }

    private static func firstNonEmpty(_ values: String?...) -> String? {
        values.first {
            guard let candidate = $0?.trimmingCharacters(in: .whitespacesAndNewlines) else {
                return false
            }

            return !candidate.isEmpty
        } ?? nil
    }

    private static func integerValue(_ raw: String?) -> Int? {
        guard let raw = raw?.trimmingCharacters(in: .whitespacesAndNewlines),
              !raw.isEmpty
        else {
            return nil
        }

        return Int(raw)
    }

    private static func sanitizedCustomValues(
        _ values: [String: String],
        reservedKeys: Set<String>) -> [String: String]
    {
        var sanitized: [String: String] = [:]

        for (key, value) in values {
            let normalizedKey = key.trimmingCharacters(in: .whitespacesAndNewlines)
            let normalizedValue = value.trimmingCharacters(in: .whitespacesAndNewlines)
            guard !normalizedKey.isEmpty,
                  !normalizedValue.isEmpty,
                  !reservedKeys.contains(normalizedKey)
            else {
                continue
            }

            sanitized[normalizedKey] = normalizedValue
        }

        return sanitized
    }

    private static func buildWgQuickConfig(
        addresses: [String],
        dnsServers: [String],
        mtu: Int?,
        privateKey: String,
        interfaceValues: [String: String],
        awgValues: [String: String],
        publicKey: String,
        presharedKey: String?,
        allowedIPs: [String],
        endpoint: String,
        persistentKeepalive: Int,
        peerValues: [String: String]) -> String
    {
        var lines = ["[Interface]"]
        lines.append("Address = \(addresses.joined(separator: ", "))")

        if !dnsServers.isEmpty {
            lines.append("DNS = \(dnsServers.joined(separator: ", "))")
        }

        if let mtu {
            lines.append("MTU = \(mtu)")
        }

        lines.append("PrivateKey = \(privateKey)")

        for (key, value) in interfaceValues.sorted(by: { $0.key.localizedCaseInsensitiveCompare($1.key) == .orderedAscending }) {
            lines.append("\(key) = \(value)")
        }

        for (key, value) in awgValues.sorted(by: { $0.key.localizedCaseInsensitiveCompare($1.key) == .orderedAscending }) {
            lines.append("\(key) = \(value)")
        }

        lines.append("[Peer]")
        lines.append("PublicKey = \(publicKey)")

        if let presharedKey {
            lines.append("PresharedKey = \(presharedKey)")
        }

        lines.append("AllowedIPs = \(allowedIPs.joined(separator: ", "))")
        lines.append("Endpoint = \(endpoint)")
        lines.append("PersistentKeepalive = \(persistentKeepalive)")

        for (key, value) in peerValues.sorted(by: { $0.key.localizedCaseInsensitiveCompare($1.key) == .orderedAscending }) {
            lines.append("\(key) = \(value)")
        }

        return lines.joined(separator: "\n")
    }

    private static func redact(_ config: String) -> String {
        config
            .split(separator: "\n", omittingEmptySubsequences: false)
            .map { line in
                let text = String(line)

                if text.hasPrefix("PrivateKey = ") {
                    return "PrivateKey = ***"
                }

                if text.hasPrefix("PublicKey = ") {
                    return "PublicKey = ***"
                }

                if text.hasPrefix("PresharedKey = ") {
                    return "PresharedKey = ***"
                }

                return text
            }
            .joined(separator: "\n")
    }
}

enum PacketTunnelConfigurationError: Error, LocalizedError {
    case missingAddress
    case missingEndpoint
    case missingPrivateKey
    case missingPublicKey
    case missingAllowedIps

    var errorDescription: String? {
        switch self {
        case .missingAddress:
            return "The staged profile is missing an interface address."
        case .missingEndpoint:
            return "The staged profile is missing a peer endpoint."
        case .missingPrivateKey:
            return "The staged profile is missing a client private key."
        case .missingPublicKey:
            return "The staged profile is missing a server public key."
        case .missingAllowedIps:
            return "The staged profile is missing allowed IP routes."
        }
    }
}
