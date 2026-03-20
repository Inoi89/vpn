import Foundation
import etoVPNMacShared

final class StatusSnapshotStore {
    private var current = StatusResponsePayload(
        connected: false,
        state: .disconnected,
        profileId: nil,
        profileName: nil,
        serverEndpoint: nil,
        deviceIpv4Address: nil,
        deviceIpv6Address: nil,
        dns: [],
        mtu: nil,
        allowedIps: [],
        routes: [],
        rxBytes: 0,
        txBytes: 0,
        latestHandshakeAtUtc: nil,
        warnings: [],
        lastError: nil)

    func snapshot() -> StatusResponsePayload {
        current
    }

    func update(_ snapshot: StatusResponsePayload) {
        current = snapshot
    }
}
