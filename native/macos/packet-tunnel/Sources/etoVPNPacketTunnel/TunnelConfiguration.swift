import Foundation
import etoVPNMacShared

struct TunnelConfiguration {
    let profile: TunnelProfilePayload

    init(profile: TunnelProfilePayload) {
        self.profile = profile
    }
}

final class TunnelControlStore {
    func loadConfiguration() throws -> TunnelConfiguration {
        // Placeholder only.
        //
        // The bridge should eventually serialize the currently staged profile
        // into an App Group container that the packet tunnel can read.
        throw TunnelControlStoreError.notImplemented
    }
}

private enum TunnelControlStoreError: Error {
    case notImplemented
}
