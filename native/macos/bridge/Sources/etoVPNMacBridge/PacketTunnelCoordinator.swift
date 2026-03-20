import Foundation
import NetworkExtension
import etoVPNMacShared

final class PacketTunnelCoordinator {
    private let stagingStore: TunnelProfileStagingStore
    private let statusStore: StatusSnapshotStore
    private let managerStore: PacketTunnelManagerStore
    private var stagedProfile: TunnelProfilePayload?
    private var activeManager: NETunnelProviderManager?

    init(
        stagingStore: TunnelProfileStagingStore,
        statusStore: StatusSnapshotStore,
        managerStore: PacketTunnelManagerStore)
    {
        self.stagingStore = stagingStore
        self.statusStore = statusStore
        self.managerStore = managerStore
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
        statusStore.update(
            makeStatusSnapshot(
                profile: profile,
                connected: false,
                state: .connecting,
                warnings: ["Activation request staged; configuring NETunnelProviderManager."],
                lastError: nil))

        Task {
            do {
                let manager = try await managerStore.loadOrCreateManager(for: profile)
                try await managerStore.configure(manager, with: profile)
                try managerStore.start(manager)
                activeManager = manager

                if let providerStatus = try await managerStore.requestStatus(from: manager) {
                    statusStore.update(
                        makeStatusSnapshot(
                            profile: profile,
                            connected: providerStatus.connected,
                            state: providerStatus.state,
                            warnings: providerStatus.warnings,
                            lastError: providerStatus.lastError,
                            rxBytes: providerStatus.rxBytes,
                            txBytes: providerStatus.txBytes,
                            latestHandshakeAtUtc: providerStatus.latestHandshakeAtUtc))
                } else {
                    statusStore.update(
                        makeStatusSnapshot(
                            profile: profile,
                            connected: false,
                            state: map(manager.connection.status),
                            warnings: ["Tunnel manager configured; waiting for packet tunnel startup."],
                            lastError: nil))
                }
            } catch {
                statusStore.update(
                    makeStatusSnapshot(
                        profile: profile,
                        connected: false,
                        state: .failed,
                        warnings: [],
                        lastError: "Failed to configure packet tunnel manager: \(error.localizedDescription)"))
            }
        }
    }

    func requestDeactivation(profileId: String?) {
        stagedProfile = nil
        stagingStore.clear()
        if let activeManager {
            managerStore.stop(activeManager)
            self.activeManager = nil
        }
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
        lastError: String?,
        rxBytes: Int64 = 0,
        txBytes: Int64 = 0,
        latestHandshakeAtUtc: String? = nil) -> StatusResponsePayload
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
            rxBytes: rxBytes,
            txBytes: txBytes,
            latestHandshakeAtUtc: latestHandshakeAtUtc,
            warnings: warnings,
            lastError: lastError)
    }

    private func map(_ status: NEVPNStatus) -> RuntimeTunnelState {
        switch status
        {
            case .invalid, .disconnected:
                return .disconnected
            case .connecting:
                return .connecting
            case .connected:
                return .connected
            case .reasserting:
                return .degraded
            case .disconnecting:
                return .degraded
            @unknown default:
                return .failed
        }
    }
}
