import Foundation
import NetworkExtension
import etoVPNMacShared

struct TunnelConfiguration {
    let profile: TunnelProfilePayload

    init(profile: TunnelProfilePayload) {
        self.profile = profile
    }
}

final class TunnelControlStore {
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

    func saveConfiguration(_ configuration: TunnelConfiguration) {
        guard let data = try? encoder.encode(configuration.profile) else {
            return
        }

        try? data.write(to: fileURL, options: .atomic)
    }

    func clearConfiguration() throws {
        try FileManager.default.removeItem(at: fileURL)
    }

    func loadConfiguration() throws -> TunnelConfiguration {
        throw TunnelControlStoreError.missingProfile
    }

    func loadConfiguration(from providerProtocol: NETunnelProviderProtocol?) throws -> TunnelConfiguration {
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

    private func loadFallbackConfiguration() throws -> TunnelConfiguration {
        // Temporary scaffold fallback only.
        // The target path is to decode the profile payload from
        // `protocolConfiguration.providerConfiguration`.
        guard let data = try? Data(contentsOf: fileURL) else {
            throw TunnelControlStoreError.missingProfile
        }

        return try decodeConfiguration(from: data)
    }

    private func decodeConfiguration(from data: Data) throws -> TunnelConfiguration {
        guard let profile = try? decoder.decode(TunnelProfilePayload.self, from: data) else {
            throw TunnelControlStoreError.unreadableProfile
        }
        return TunnelConfiguration(profile: profile)
    }
}

private enum TunnelControlStoreError: Error, LocalizedError {
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
