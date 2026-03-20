import Foundation
import NetworkExtension
import etoVPNMacShared

final class TunnelProfileStore {
    private let fileURL: URL
    private let encoder: JSONEncoder
    private let decoder: JSONDecoder

    init(
        fileURL: URL = RuntimeBridgeConstants.stagedProfileURL(),
        encoder: JSONEncoder = JSONEncoder(),
        decoder: JSONDecoder = JSONDecoder())
    {
        self.fileURL = fileURL
        self.encoder = encoder
        self.decoder = decoder
    }

    func saveProfile(_ profile: TunnelProfilePayload) {
        guard let data = try? encoder.encode(profile) else {
            return
        }

        try? data.write(to: fileURL, options: .atomic)
    }

    func clearProfile() throws {
        try FileManager.default.removeItem(at: fileURL)
    }

    func loadConfiguration(from providerProtocol: NETunnelProviderProtocol?) throws -> PacketTunnelConfiguration {
        guard let providerProtocol,
              let providerConfiguration = providerProtocol.providerConfiguration
        else {
            return try loadFallbackConfiguration()
        }

        if let data = providerConfiguration[RuntimeBridgeConstants.providerProfilePayloadKey] as? Data {
            return try decodeConfiguration(from: data)
        }

        if let string = providerConfiguration[RuntimeBridgeConstants.providerProfilePayloadKey] as? String,
           let data = string.data(using: .utf8)
        {
            return try decodeConfiguration(from: data)
        }

        return try loadFallbackConfiguration()
    }

    private func loadFallbackConfiguration() throws -> PacketTunnelConfiguration {
        // Temporary scaffold fallback only.
        // The primary Apple path is to decode a normalized WireGuard payload from
        // `protocolConfiguration.providerConfiguration`.
        guard let data = try? Data(contentsOf: fileURL) else {
            throw TunnelProfileStoreError.missingProfile
        }

        return try decodeConfiguration(from: data)
    }

    private func decodeConfiguration(from data: Data) throws -> PacketTunnelConfiguration {
        if let providerConfiguration = try? decoder.decode(WireGuardProviderConfiguration.self, from: data) {
            return PacketTunnelConfigurationBuilder.build(from: providerConfiguration)
        }

        if let profile = try? decoder.decode(TunnelProfilePayload.self, from: data) {
            return try PacketTunnelConfigurationBuilder.build(from: profile)
        }

        throw TunnelProfileStoreError.unreadableProfile
    }
}

private enum TunnelProfileStoreError: Error, LocalizedError {
    case missingProfile
    case unreadableProfile

    var errorDescription: String? {
        switch self {
        case .missingProfile:
            return "No staged tunnel profile was available."
        case .unreadableProfile:
            return "The staged tunnel profile could not be decoded."
        }
    }
}
