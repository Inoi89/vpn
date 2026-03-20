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

    func loadProfile(from providerProtocol: NETunnelProviderProtocol?) throws -> TunnelProfilePayload {
        guard let providerProtocol,
              let providerConfiguration = providerProtocol.providerConfiguration
        else {
            return try loadFallbackProfile()
        }

        if let data = providerConfiguration[RuntimeBridgeConstants.providerProfilePayloadKey] as? Data {
            return try decodeProfile(from: data)
        }

        if let string = providerConfiguration[RuntimeBridgeConstants.providerProfilePayloadKey] as? String,
           let data = string.data(using: .utf8)
        {
            return try decodeProfile(from: data)
        }

        return try loadFallbackProfile()
    }

    private func loadFallbackProfile() throws -> TunnelProfilePayload {
        // Temporary scaffold fallback only.
        // The target path is to decode the profile payload from
        // `protocolConfiguration.providerConfiguration`.
        guard let data = try? Data(contentsOf: fileURL) else {
            throw TunnelProfileStoreError.missingProfile
        }

        return try decodeProfile(from: data)
    }

    private func decodeProfile(from data: Data) throws -> TunnelProfilePayload {
        guard let profile = try? decoder.decode(TunnelProfilePayload.self, from: data) else {
            throw TunnelProfileStoreError.unreadableProfile
        }
        return profile
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
