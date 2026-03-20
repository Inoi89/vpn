import Foundation

enum WgQuickConfigurationParser {
    static func parse(
        _ wgQuickConfig: String,
        profileId: String,
        profileName: String,
        format: String) throws -> PacketTunnelConfiguration
    {
        var currentSection: Section = .none
        var sawPeerSection = false

        var interfaceAddresses: [String] = []
        var dnsServers: [String] = []
        var mtu: Int?
        var privateKey: String?
        var interfaceValues: [String: String] = [:]
        var awgValues: [String: String] = [:]

        var allowedIPs: [String] = []
        var endpoint: String?
        var publicKey: String?
        var presharedKey: String?
        var persistentKeepalive: Int?
        var peerValues: [String: String] = [:]

        for rawLine in wgQuickConfig.components(separatedBy: .newlines) {
            let line = stripComment(from: rawLine).trimmingCharacters(in: .whitespacesAndNewlines)
            guard !line.isEmpty else {
                continue
            }

            switch line {
            case "[Interface]":
                currentSection = .interface
                continue
            case "[Peer]":
                if sawPeerSection {
                    throw WgQuickConfigurationParserError.multiplePeersAreNotSupported
                }

                currentSection = .peer
                sawPeerSection = true
                continue
            default:
                break
            }

            guard let keyValue = splitKeyValue(line) else {
                throw WgQuickConfigurationParserError.invalidLine(line)
            }

            switch currentSection {
            case .interface:
                consumeInterface(
                    key: keyValue.key,
                    value: keyValue.value,
                    addresses: &interfaceAddresses,
                    dnsServers: &dnsServers,
                    mtu: &mtu,
                    privateKey: &privateKey,
                    interfaceValues: &interfaceValues,
                    awgValues: &awgValues)
            case .peer:
                consumePeer(
                    key: keyValue.key,
                    value: keyValue.value,
                    allowedIPs: &allowedIPs,
                    endpoint: &endpoint,
                    publicKey: &publicKey,
                    presharedKey: &presharedKey,
                    persistentKeepalive: &persistentKeepalive,
                    peerValues: &peerValues)
            case .none:
                throw WgQuickConfigurationParserError.invalidLine(line)
            }
        }

        guard !interfaceAddresses.isEmpty else {
            throw PacketTunnelConfigurationError.missingAddress
        }

        guard let endpoint, !endpoint.isEmpty else {
            throw PacketTunnelConfigurationError.missingEndpoint
        }

        guard let privateKey, !privateKey.isEmpty else {
            throw PacketTunnelConfigurationError.missingPrivateKey
        }

        guard let publicKey, !publicKey.isEmpty else {
            throw PacketTunnelConfigurationError.missingPublicKey
        }

        guard !allowedIPs.isEmpty else {
            throw PacketTunnelConfigurationError.missingAllowedIps
        }

        return PacketTunnelConfiguration(
            profileId: profileId,
            profileName: profileName,
            format: format,
            tunnelRemoteAddress: endpoint,
            splitTunnelType: 0,
            splitTunnelSites: [],
            interface: PacketTunnelInterfaceConfiguration(
                addresses: interfaceAddresses,
                dnsServers: dnsServers,
                mtu: mtu,
                interfaceValues: interfaceValues),
            peer: PacketTunnelPeerConfiguration(
                allowedIPs: allowedIPs,
                splitTunnelType: 0,
                splitTunnelSites: [],
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

    private static func consumeInterface(
        key: String,
        value: String,
        addresses: inout [String],
        dnsServers: inout [String],
        mtu: inout Int?,
        privateKey: inout String?,
        interfaceValues: inout [String: String],
        awgValues: inout [String: String])
    {
        switch key.lowercased() {
        case "address":
            addresses.append(contentsOf: parseDelimitedValues(value))
        case "dns":
            dnsServers.append(contentsOf: parseDelimitedValues(value))
        case "mtu":
            mtu = Int(value.trimmingCharacters(in: .whitespacesAndNewlines))
        case "privatekey":
            privateKey = value
        default:
            if isAwgKey(key) {
                awgValues[key] = value
            } else {
                interfaceValues[key] = value
            }
        }
    }

    private static func consumePeer(
        key: String,
        value: String,
        allowedIPs: inout [String],
        endpoint: inout String?,
        publicKey: inout String?,
        presharedKey: inout String?,
        persistentKeepalive: inout Int?,
        peerValues: inout [String: String])
    {
        switch key.lowercased() {
        case "allowedips":
            allowedIPs.append(contentsOf: parseDelimitedValues(value))
        case "endpoint":
            endpoint = value
        case "publickey":
            publicKey = value
        case "presharedkey":
            presharedKey = value
        case "persistentkeepalive", "persistentkeepaliveinterval":
            persistentKeepalive = Int(value.trimmingCharacters(in: .whitespacesAndNewlines))
        default:
            peerValues[key] = value
        }
    }

    private static func splitKeyValue(_ line: String) -> (key: String, value: String)? {
        guard let separatorIndex = line.firstIndex(of: "=") else {
            return nil
        }

        let key = line[..<separatorIndex].trimmingCharacters(in: .whitespacesAndNewlines)
        let value = line[line.index(after: separatorIndex)...].trimmingCharacters(in: .whitespacesAndNewlines)
        guard !key.isEmpty, !value.isEmpty else {
            return nil
        }

        return (key, value)
    }

    private static func parseDelimitedValues(_ raw: String) -> [String] {
        raw.split(separator: ",")
            .map { $0.trimmingCharacters(in: .whitespacesAndNewlines) }
            .filter { !$0.isEmpty }
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

    private static func stripComment(from line: String) -> String {
        guard let commentIndex = line.firstIndex(of: "#") else {
            return line
        }

        return String(line[..<commentIndex])
    }

    private static func isAwgKey(_ key: String) -> Bool {
        switch key {
        case "H1", "H2", "H3", "H4", "Jc", "Jmin", "Jmax", "S1", "S2", "S3", "S4", "I1", "I2", "I3", "I4", "I5":
            return true
        default:
            return false
        }
    }

    private enum Section {
        case none
        case interface
        case peer
    }
}

enum WgQuickConfigurationParserError: Error, LocalizedError {
    case invalidLine(String)
    case multiplePeersAreNotSupported

    var errorDescription: String? {
        switch self {
        case .invalidLine(let line):
            return "The packet tunnel update contains an invalid wg-quick line: \(line)"
        case .multiplePeersAreNotSupported:
            return "The macOS scaffold currently supports only single-peer WireGuard/AWG configurations."
        }
    }
}
