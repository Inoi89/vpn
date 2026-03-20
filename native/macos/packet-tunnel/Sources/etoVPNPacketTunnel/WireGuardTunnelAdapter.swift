import Foundation

final class WireGuardTunnelAdapter {
    private var snapshot = TunnelRuntimeSnapshot.disconnected()

    func start(with configuration: TunnelConfiguration) {
        // Placeholder only.
        //
        // This boundary should eventually translate `TunnelConfiguration`
        // into the native tunnel engine configuration and start the engine.
        snapshot = TunnelRuntimeSnapshot(
            connected: false,
            state: .connecting,
            rxBytes: 0,
            txBytes: 0,
            latestHandshakeAtUtc: nil,
            warnings: ["WireGuard/AWG engine integration is not implemented in this scaffold."],
            lastError: nil)
        _ = configuration
    }

    func stop() {
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
