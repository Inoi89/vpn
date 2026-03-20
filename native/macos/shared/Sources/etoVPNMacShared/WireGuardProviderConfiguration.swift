import Foundation

public struct WireGuardProviderConfiguration: Codable {
    public let profileId: String
    public let profileName: String
    public let format: String
    public let dns1: String?
    public let dns2: String?
    public let mtu: Int?
    public let hostName: String
    public let port: Int?
    public let clientIP: String
    public let clientPrivateKey: String
    public let serverPublicKey: String
    public let presharedKey: String?
    public let allowedIPs: [String]
    public let persistentKeepAlive: Int
    public let splitTunnelType: Int
    public let splitTunnelSites: [String]
    public let interfaceValues: [String: String]
    public let peerValues: [String: String]
    public let h1: String?
    public let h2: String?
    public let h3: String?
    public let h4: String?
    public let jc: String?
    public let jmin: String?
    public let jmax: String?
    public let s1: String?
    public let s2: String?
    public let s3: String?
    public let s4: String?
    public let i1: String?
    public let i2: String?
    public let i3: String?
    public let i4: String?
    public let i5: String?

    enum CodingKeys: String, CodingKey {
        case profileId
        case profileName
        case format
        case dns1
        case dns2
        case mtu
        case hostName
        case port
        case clientIP = "client_ip"
        case clientPrivateKey = "client_priv_key"
        case serverPublicKey = "server_pub_key"
        case presharedKey = "psk_key"
        case allowedIPs = "allowed_ips"
        case persistentKeepAlive = "persistent_keep_alive"
        case splitTunnelType
        case splitTunnelSites
        case interfaceValues
        case peerValues
        case h1 = "H1"
        case h2 = "H2"
        case h3 = "H3"
        case h4 = "H4"
        case jc = "Jc"
        case jmin = "Jmin"
        case jmax = "Jmax"
        case s1 = "S1"
        case s2 = "S2"
        case s3 = "S3"
        case s4 = "S4"
        case i1 = "I1"
        case i2 = "I2"
        case i3 = "I3"
        case i4 = "I4"
        case i5 = "I5"
    }

    public init(
        profileId: String,
        profileName: String,
        format: String,
        dns1: String?,
        dns2: String?,
        mtu: Int?,
        hostName: String,
        port: Int?,
        clientIP: String,
        clientPrivateKey: String,
        serverPublicKey: String,
        presharedKey: String?,
        allowedIPs: [String],
        persistentKeepAlive: Int,
        splitTunnelType: Int,
        splitTunnelSites: [String],
        interfaceValues: [String: String],
        peerValues: [String: String],
        h1: String?,
        h2: String?,
        h3: String?,
        h4: String?,
        jc: String?,
        jmin: String?,
        jmax: String?,
        s1: String?,
        s2: String?,
        s3: String?,
        s4: String?,
        i1: String?,
        i2: String?,
        i3: String?,
        i4: String?,
        i5: String?)
    {
        self.profileId = profileId
        self.profileName = profileName
        self.format = format
        self.dns1 = dns1
        self.dns2 = dns2
        self.mtu = mtu
        self.hostName = hostName
        self.port = port
        self.clientIP = clientIP
        self.clientPrivateKey = clientPrivateKey
        self.serverPublicKey = serverPublicKey
        self.presharedKey = presharedKey
        self.allowedIPs = allowedIPs
        self.persistentKeepAlive = persistentKeepAlive
        self.splitTunnelType = splitTunnelType
        self.splitTunnelSites = splitTunnelSites
        self.interfaceValues = interfaceValues
        self.peerValues = peerValues
        self.h1 = h1
        self.h2 = h2
        self.h3 = h3
        self.h4 = h4
        self.jc = jc
        self.jmin = jmin
        self.jmax = jmax
        self.s1 = s1
        self.s2 = s2
        self.s3 = s3
        self.s4 = s4
        self.i1 = i1
        self.i2 = i2
        self.i3 = i3
        self.i4 = i4
        self.i5 = i5
    }

    public static func from(profile: TunnelProfilePayload) throws -> WireGuardProviderConfiguration {
        let tunnelConfig = profile.tunnelConfig
        let clientIP = firstNonEmpty(tunnelConfig.address, profile.address)
        guard let clientIP else {
            throw WireGuardProviderConfigurationError.missingAddress
        }

        let endpoint = firstNonEmpty(tunnelConfig.endpoint, profile.endpoint)
        guard let endpoint else {
            throw WireGuardProviderConfigurationError.missingEndpoint
        }

        let (hostName, port) = splitEndpoint(endpoint)
        guard let hostName, !hostName.isEmpty else {
            throw WireGuardProviderConfigurationError.missingEndpoint
        }

        let privateKey = firstNonEmpty(profile.privateKey, tunnelConfig.interfaceValues["PrivateKey"])
        guard let privateKey else {
            throw WireGuardProviderConfigurationError.missingPrivateKey
        }

        let publicKey = firstNonEmpty(tunnelConfig.publicKey, profile.publicKey, tunnelConfig.peerValues["PublicKey"])
        guard let publicKey else {
            throw WireGuardProviderConfigurationError.missingPublicKey
        }

        let allowedIPs = normalizedList(!tunnelConfig.allowedIps.isEmpty ? tunnelConfig.allowedIps : profile.allowedIps)
        guard !allowedIPs.isEmpty else {
            throw WireGuardProviderConfigurationError.missingAllowedIps
        }

        let dnsServers = normalizedList(tunnelConfig.dns.isEmpty ? profile.dns : tunnelConfig.dns)
        let persistentKeepAlive = tunnelConfig.persistentKeepalive
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

        return WireGuardProviderConfiguration(
            profileId: profile.profileId,
            profileName: profile.profileName,
            format: normalizedFormat(profile.managedProfile?.configFormat ?? profile.sourceFormat ?? tunnelConfig.format),
            dns1: dnsServers.indices.contains(0) ? dnsServers[0] : nil,
            dns2: dnsServers.indices.contains(1) ? dnsServers[1] : nil,
            mtu: tunnelConfig.mtu ?? profile.mtu ?? integerValue(tunnelConfig.interfaceValues["MTU"]),
            hostName: hostName,
            port: port,
            clientIP: clientIP,
            clientPrivateKey: privateKey,
            serverPublicKey: publicKey,
            presharedKey: firstNonEmpty(tunnelConfig.presharedKey, profile.presharedKey, tunnelConfig.peerValues["PresharedKey"]),
            allowedIPs: allowedIPs,
            persistentKeepAlive: persistentKeepAlive,
            splitTunnelType: 0,
            splitTunnelSites: [],
            interfaceValues: interfaceValues,
            peerValues: peerValues,
            h1: awgValues["H1"],
            h2: awgValues["H2"],
            h3: awgValues["H3"],
            h4: awgValues["H4"],
            jc: awgValues["Jc"],
            jmin: awgValues["Jmin"],
            jmax: awgValues["Jmax"],
            s1: awgValues["S1"],
            s2: awgValues["S2"],
            s3: awgValues["S3"],
            s4: awgValues["S4"],
            i1: awgValues["I1"],
            i2: awgValues["I2"],
            i3: awgValues["I3"],
            i4: awgValues["I4"],
            i5: awgValues["I5"])
    }

    public func wgQuickConfig() -> String {
        var lines = ["[Interface]"]
        lines.append("Address = \(clientIP)")

        let dnsValues = [dns1, dns2].compactMap {
            $0?.trimmingCharacters(in: .whitespacesAndNewlines)
        }.filter { !$0.isEmpty }
        if !dnsValues.isEmpty {
            lines.append("DNS = \(dnsValues.joined(separator: ", "))")
        }

        if let mtu {
            lines.append("MTU = \(mtu)")
        }

        lines.append("PrivateKey = \(clientPrivateKey)")

        for (key, value) in interfaceValues.sorted(by: { $0.key.localizedCaseInsensitiveCompare($1.key) == .orderedAscending }) {
            lines.append("\(key) = \(value)")
        }

        for (key, value) in awgValues().sorted(by: { $0.key.localizedCaseInsensitiveCompare($1.key) == .orderedAscending }) {
            lines.append("\(key) = \(value)")
        }

        lines.append("[Peer]")
        lines.append("PublicKey = \(serverPublicKey)")
        if let presharedKey {
            lines.append("PresharedKey = \(presharedKey)")
        }

        lines.append("AllowedIPs = \(allowedIPs.joined(separator: ", "))")
        lines.append("Endpoint = \(endpointString())")
        lines.append("PersistentKeepalive = \(persistentKeepAlive)")

        for (key, value) in peerValues.sorted(by: { $0.key.localizedCaseInsensitiveCompare($1.key) == .orderedAscending }) {
            lines.append("\(key) = \(value)")
        }

        return lines.joined(separator: "\n")
    }

    public func redactedSummary() -> String {
        wgQuickConfig()
            .split(separator: "\n", omittingEmptySubsequences: false)
            .map { line in
                let text = String(line)
                if text.hasPrefix("PrivateKey = ") { return "PrivateKey = ***" }
                if text.hasPrefix("PublicKey = ") { return "PublicKey = ***" }
                if text.hasPrefix("PresharedKey = ") { return "PresharedKey = ***" }
                return text
            }
            .joined(separator: "\n")
    }

    public func endpointString() -> String {
        if let port {
            return "\(hostName):\(port)"
        }

        return hostName
    }

    public func awgValues() -> [String: String] {
        var values: [String: String] = [:]

        appendOptional(h1, as: "H1", into: &values)
        appendOptional(h2, as: "H2", into: &values)
        appendOptional(h3, as: "H3", into: &values)
        appendOptional(h4, as: "H4", into: &values)
        appendOptional(jc, as: "Jc", into: &values)
        appendOptional(jmin, as: "Jmin", into: &values)
        appendOptional(jmax, as: "Jmax", into: &values)
        appendOptional(s1, as: "S1", into: &values)
        appendOptional(s2, as: "S2", into: &values)
        appendOptional(s3, as: "S3", into: &values)
        appendOptional(s4, as: "S4", into: &values)
        appendOptional(i1, as: "I1", into: &values)
        appendOptional(i2, as: "I2", into: &values)
        appendOptional(i3, as: "I3", into: &values)
        appendOptional(i4, as: "I4", into: &values)
        appendOptional(i5, as: "I5", into: &values)

        return values
    }

    private static func normalizedFormat(_ raw: String) -> String {
        let normalized = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        return normalized.isEmpty ? "wireguard" : normalized
    }

    private static func normalizedList(_ values: [String]) -> [String] {
        values
            .flatMap { raw in
                raw
                    .split(separator: ",")
                    .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            }
            .filter { !$0.isEmpty }
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

    private static func splitEndpoint(_ endpoint: String) -> (String?, Int?) {
        let value = endpoint.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !value.isEmpty else {
            return (nil, nil)
        }

        if value.hasPrefix("["),
           let bracketIndex = value.firstIndex(of: "]")
        {
            let host = String(value[value.index(after: value.startIndex)..<bracketIndex])
            let suffix = value[value.index(after: bracketIndex)...]
            if suffix.hasPrefix(":"),
               let port = Int(suffix.dropFirst())
            {
                return (host, port)
            }

            return (host, nil)
        }

        let parts = value.split(separator: ":", omittingEmptySubsequences: false)
        guard parts.count >= 2, let port = Int(parts.last ?? "") else {
            return (value, nil)
        }

        let host = parts.dropLast().joined(separator: ":")
        return (host.isEmpty ? value : host, port)
    }

    private func appendOptional(_ value: String?, as key: String, into dictionary: inout [String: String]) {
        guard let value = value?.trimmingCharacters(in: .whitespacesAndNewlines),
              !value.isEmpty
        else {
            return
        }

        dictionary[key] = value
    }
}

public enum WireGuardProviderConfigurationError: Error, LocalizedError {
    case missingAddress
    case missingEndpoint
    case missingPrivateKey
    case missingPublicKey
    case missingAllowedIps

    public var errorDescription: String? {
        switch self {
        case .missingAddress:
            return "The Apple packet tunnel payload is missing an interface address."
        case .missingEndpoint:
            return "The Apple packet tunnel payload is missing a peer endpoint."
        case .missingPrivateKey:
            return "The Apple packet tunnel payload is missing a client private key."
        case .missingPublicKey:
            return "The Apple packet tunnel payload is missing a server public key."
        case .missingAllowedIps:
            return "The Apple packet tunnel payload is missing allowed IP routes."
        }
    }
}
