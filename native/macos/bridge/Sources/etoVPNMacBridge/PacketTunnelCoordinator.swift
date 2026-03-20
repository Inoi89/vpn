import Foundation
import Dispatch
import NetworkExtension
import etoVPNMacShared

final class PacketTunnelCoordinator {
    private let stagingStore: TunnelProfileStagingStore
    private let statusStore: StatusSnapshotStore
    private let managerStore: PacketTunnelManagerStore
    private let statusObserver: TunnelManagerStatusObserver
    private let statusPoller: TunnelStatusPoller
    private var stagedProfile: TunnelProfilePayload?
    private var activeManager: NETunnelProviderManager?

    init(
        stagingStore: TunnelProfileStagingStore,
        statusStore: StatusSnapshotStore,
        managerStore: PacketTunnelManagerStore,
        statusObserver: TunnelManagerStatusObserver,
        statusPoller: TunnelStatusPoller)
    {
        self.stagingStore = stagingStore
        self.statusStore = statusStore
        self.managerStore = managerStore
        self.statusObserver = statusObserver
        self.statusPoller = statusPoller
    }

    func stageProfile(_ profile: TunnelProfilePayload) {
        stagedProfile = profile
        stagingStore.save(profile)
        statusStore.update(
            makeStatusSnapshot(
                profile: profile,
                connected: false,
                state: .disconnected,
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
                beginObserving(manager: manager, profile: profile)

                if let providerStatus = try await managerStore.requestStatus(from: manager) {
                    applyProviderStatus(providerStatus, profile: profile)
                } else {
                    statusStore.update(
                        makeStatusSnapshot(
                            profile: profile,
                            connected: false,
                            state: map(manager.connection.status, connected: false),
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
        statusObserver.stop()
        statusPoller.stop()
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

    func requestLogs() -> [String] {
        guard let activeManager else {
            return []
        }

        let semaphore = DispatchSemaphore(value: 0)
        var entries: [String] = []

        Task {
            defer { semaphore.signal() }

            do {
                if let response = try await managerStore.requestLogs(from: activeManager) {
                    entries = response.entries
                }
            } catch {
                entries = ["Provider log request failed: \(error.localizedDescription)"]
            }
        }

        guard semaphore.wait(timeout: .now() + 3) == .success else {
            return ["Provider log request timed out."]
        }
        return entries
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
        let resolvedEndpoint = profile.tunnelConfig.endpoint ?? profile.endpoint
        let resolvedAddress = profile.tunnelConfig.address ?? profile.address
        let resolvedDns = profile.tunnelConfig.dns.isEmpty ? profile.dns : profile.tunnelConfig.dns
        let resolvedMtu = profile.tunnelConfig.mtu ?? profile.mtu
        let resolvedAllowedIps = profile.tunnelConfig.allowedIps.isEmpty ? profile.allowedIps : profile.tunnelConfig.allowedIps

        StatusResponsePayload(
            connected: connected,
            state: state,
            profileId: profile.profileId,
            profileName: profile.profileName,
            serverEndpoint: resolvedEndpoint,
            deviceIpv4Address: resolvedAddress,
            deviceIpv6Address: nil,
            dns: resolvedDns,
            mtu: resolvedMtu,
            allowedIps: resolvedAllowedIps,
            routes: resolvedAllowedIps,
            rxBytes: rxBytes,
            txBytes: txBytes,
            latestHandshakeAtUtc: latestHandshakeAtUtc,
            warnings: warnings,
            lastError: lastError)
    }

    private func beginObserving(manager: NETunnelProviderManager, profile: TunnelProfilePayload) {
        statusObserver.start(observing: manager) { [weak self] status in
            guard let self else { return }

            let current = self.statusStore.snapshot()
            self.statusStore.update(
                self.makeStatusSnapshot(
                    profile: profile,
                    connected: current.connected,
                    state: self.map(status, connected: current.connected),
                    warnings: current.warnings,
                    lastError: current.lastError,
                    rxBytes: current.rxBytes,
                    txBytes: current.txBytes,
                    latestHandshakeAtUtc: current.latestHandshakeAtUtc))
        }

        statusPoller.start(
            manager: manager,
            onStatus: { [weak self] providerStatus in
                guard let self else { return }
                self.applyProviderStatus(providerStatus, profile: profile)
            },
            onFailure: { [weak self] error in
                guard let self else { return }
                let current = self.statusStore.snapshot()
                self.statusStore.update(
                    self.makeStatusSnapshot(
                        profile: profile,
                        connected: current.connected,
                        state: current.state,
                        warnings: current.warnings,
                        lastError: "Provider status request failed: \(error.localizedDescription)",
                        rxBytes: current.rxBytes,
                        txBytes: current.txBytes,
                        latestHandshakeAtUtc: current.latestHandshakeAtUtc))
            })
    }

    private func applyProviderStatus(_ providerStatus: TunnelProviderMessageStatusResponse, profile: TunnelProfilePayload) {
        let normalizedState: RuntimeTunnelState
        if providerStatus.state == .connected && !providerStatus.connected {
            normalizedState = .degraded
        } else {
            normalizedState = providerStatus.state
        }

        statusStore.update(
            makeStatusSnapshot(
                profile: profile,
                connected: providerStatus.connected,
                state: normalizedState,
                warnings: providerStatus.warnings,
                lastError: providerStatus.lastError,
                rxBytes: providerStatus.rxBytes,
                txBytes: providerStatus.txBytes,
                latestHandshakeAtUtc: providerStatus.latestHandshakeAtUtc))
    }

    private func map(_ status: NEVPNStatus, connected: Bool) -> RuntimeTunnelState {
        switch status
        {
            case .invalid, .disconnected:
                return .disconnected
            case .connecting:
                return .connecting
            case .connected:
                return connected ? .connected : .degraded
            case .reasserting:
                return .degraded
            case .disconnecting:
                return .degraded
            @unknown default:
                return .failed
        }
    }
}
