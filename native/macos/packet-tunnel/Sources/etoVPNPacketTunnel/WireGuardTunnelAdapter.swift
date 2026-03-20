import Foundation

final class WireGuardTunnelAdapter {
    private var snapshot = TunnelRuntimeSnapshot.disconnected()
    private var activeConfiguration: PacketTunnelConfiguration?

    func start(with configuration: PacketTunnelConfiguration) {
        // Placeholder only.
        //
        // This boundary should eventually translate `PacketTunnelConfiguration`
        // into the native tunnel engine configuration and start the engine.
        activeConfiguration = configuration
        snapshot = TunnelRuntimeSnapshot(
            connected: false,
            state: .failed,
            rxBytes: 0,
            txBytes: 0,
            latestHandshakeAtUtc: nil,
            warnings: [
                "Prepared canonical \(configuration.format.uppercased()) runtime configuration for \(configuration.tunnelRemoteAddress).",
                "The packet tunnel scaffold can apply network settings, but the native WireGuard/AWG engine is not attached yet."
            ],
            lastError: "WireGuard/AWG engine integration is not implemented in this scaffold.")
        _ = configuration.redactedSummary
    }

    func stop() {
        activeConfiguration = nil
        snapshot = .disconnected()
    }

    func markFailed(_ error: String) {
        snapshot = TunnelRuntimeSnapshot(
            connected: false,
            state: .failed,
            rxBytes: 0,
            txBytes: 0,
            latestHandshakeAtUtc: nil,
            warnings: [],
            lastError: error)
    }

    func currentSnapshot() -> TunnelRuntimeSnapshot {
        snapshot
    }
}
