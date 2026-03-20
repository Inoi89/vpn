import Foundation
import etoVPNMacShared

final class PacketTunnelCoordinator {
    private let stagingStore: TunnelProfileStagingStore
    private let statusStore: StatusSnapshotStore
    private var stagedProfile: TunnelProfilePayload?

    init(
        stagingStore: TunnelProfileStagingStore,
        statusStore: StatusSnapshotStore)
    {
        self.stagingStore = stagingStore
        self.statusStore = statusStore
    }

    func stageProfile(_ profile: TunnelProfilePayload) {
        stagedProfile = profile
        stagingStore.save(profile)
        statusStore.update(
            makeStatusSnapshot(
                profile: profile,
                connected: false,
                state: .connecting,
                warnings: ["Staged profile persisted for packet tunnel handoff."],
                lastError: nil))
    }

    func requestActivation() {
        guard let profile = stagedProfile ?? stagingStore.load() else {
            statusStore.update(
                StatusResponsePayload(
                    connected: false,
                    state: .failed,
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
                    warnings: ["Activation was requested before a profile was staged."],
                    lastError: "Missing staged profile."))
            return
        }

        stagedProfile = profile
        stagingStore.save(profile)
        // This staged-file path is only a scaffold fallback.
        //
        // The target Apple runtime flow should:
        // - load or create `NETunnelProviderManager`
        // - write the real tunnel payload into `NETunnelProviderProtocol.providerConfiguration`
        // - save/load the manager from preferences
        // - call `startVPNTunnel(options: nil)`
        // - later poll the extension via `sendProviderMessage` for counters/handshake
        statusStore.update(
            makeStatusSnapshot(
                profile: profile,
                connected: false,
                state: .connecting,
                warnings: ["Packet tunnel activation is still scaffolded; the staged profile is ready for handoff."],
                lastError: nil))
    }

    func requestDeactivation(profileId: String?) {
        stagedProfile = nil
        stagingStore.clear()
        statusStore.update(
            StatusResponsePayload(
                connected: false,
                state: .disconnected,
                profileId: profileId ?? statusStore.snapshot().profileId,
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

    private func makeStatusSnapshot(
        profile: TunnelProfilePayload,
        connected: Bool,
        state: RuntimeTunnelState,
        warnings: [String],
        lastError: String?) -> StatusResponsePayload
    {
        StatusResponsePayload(
            connected: connected,
            state: state,
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
            warnings: warnings,
            lastError: lastError)
    }
}
