import Foundation
import etoVPNMacShared

final class TunnelProfileStagingStore {
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

    func save(_ profile: TunnelProfilePayload) {
        guard let data = try? encoder.encode(profile) else {
            return
        }

        try? data.write(to: fileURL, options: .atomic)
    }

    func load() -> TunnelProfilePayload? {
        guard let data = try? Data(contentsOf: fileURL) else {
            return nil
        }

        return try? decoder.decode(TunnelProfilePayload.self, from: data)
    }

    func clear() {
        try? FileManager.default.removeItem(at: fileURL)
    }
}
