import Foundation
import etoVPNMacShared

final class PacketTunnelCoordinator {
    private let statusStore: StatusSnapshotStore
    private var stagedProfile: TunnelProfilePayload?

    init(statusStore: StatusSnapshotStore) {
        self.statusStore = statusStore
    }

    func stageProfile(_ profile: TunnelProfilePayload) {
        stagedProfile = profile
        statusStore.update(
            StatusResponsePayload(
                connected: false,
                state: .connecting,
                profileId: profile.profileId,
                profileName: profile.profileName,
                serverEndpoint: profile.endpoint,
                deviceIpv4Address: profile.address,
                deviceIpv6Address: nil,
                dns: profile.dns,
                mtu: profile.mtu,
                allowedIps: profile.allowedIps,
                routes: profile.allowedIps,
                rxBytes: 0,
                txBytes: 0,
                latestHandshakeAtUtc: nil,
                warnings: ["Packet tunnel activation is not implemented in the scaffold."],
                lastError: nil))
    }

    func requestActivation() {
        // Placeholder only.
        //
        // The real implementation should:
        // - persist the staged profile into an App Group container
        // - load or create `NETunnelProviderManager`
        // - hand control to the packet tunnel extension
        // - update the status snapshot as the tunnel moves through states
    }

    func requestDeactivation(profileId: String?) {
        stagedProfile = nil
        statusStore.update(
            StatusResponsePayload(
                connected: false,
                state: .disconnected,
                profileId: profileId,
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
                lastError: nil))
    }
}
