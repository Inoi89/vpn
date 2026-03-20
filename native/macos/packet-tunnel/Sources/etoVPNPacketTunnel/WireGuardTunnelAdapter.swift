import Foundation

final class WireGuardTunnelAdapter {
    private var snapshot = TunnelRuntimeSnapshot.disconnected()
    private var activeConfiguration: PacketTunnelConfiguration?
    private let engine: PacketTunnelEngine

    init(engine: PacketTunnelEngine = ScaffoldWireGuardEngine()) {
        self.engine = engine
    }

    func start(
        with configuration: PacketTunnelConfiguration,
        completion: @escaping (PacketTunnelEngineError?) -> Void)
    {
        activeConfiguration = configuration
        engine.start(configuration: configuration) { [weak self] result in
            guard let self else {
                completion(.startFailed("The packet tunnel adapter was released before the engine returned."))
                return
            }

            switch result {
            case .success(let startedSnapshot):
                self.snapshot = startedSnapshot
                completion(nil)
            case .failure(let error):
                self.snapshot = TunnelRuntimeSnapshot(
                    connected: false,
                    state: .failed,
                    rxBytes: 0,
                    txBytes: 0,
                    latestHandshakeAtUtc: nil,
                    warnings: [
                        "Prepared canonical \(configuration.format.uppercased()) runtime configuration for \(configuration.tunnelRemoteAddress).",
                        "The packet tunnel scaffold can apply network settings, but the native WireGuard/AWG engine is not attached yet."
                    ],
                    lastError: error.localizedDescription)
                _ = configuration.redactedSummary
                completion(error)
            }
        }
    }

    func stop(completion: @escaping (PacketTunnelEngineError?) -> Void) {
        engine.stop { [weak self] result in
            guard let self else {
                completion(.stopFailed("The packet tunnel adapter was released before the engine stopped."))
                return
            }

            switch result {
            case .success:
                self.activeConfiguration = nil
                self.snapshot = .disconnected()
                completion(nil)
            case .failure(let error):
                self.snapshot = TunnelRuntimeSnapshot(
                    connected: false,
                    state: .failed,
                    rxBytes: 0,
                    txBytes: 0,
                    latestHandshakeAtUtc: nil,
                    warnings: [],
                    lastError: error.localizedDescription)
                completion(error)
            }
        }
    }

    func update(
        with configuration: PacketTunnelConfiguration,
        completion: @escaping (PacketTunnelEngineError?) -> Void)
    {
        activeConfiguration = configuration
        engine.update(configuration: configuration) { [weak self] result in
            guard let self else {
                completion(.startFailed("The packet tunnel adapter was released before the engine updated."))
                return
            }

            switch result {
            case .success(let snapshot):
                self.snapshot = snapshot
                completion(nil)
            case .failure(let error):
                self.snapshot = TunnelRuntimeSnapshot(
                    connected: false,
                    state: .failed,
                    rxBytes: 0,
                    txBytes: 0,
                    latestHandshakeAtUtc: nil,
                    warnings: ["The macOS packet tunnel engine rejected an in-place configuration update."],
                    lastError: error.localizedDescription)
                completion(error)
            }
        }
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

    func preparedConfigurationSummary() -> String? {
        engine.runtimeConfiguration() ?? activeConfiguration?.redactedSummary
    }

    func interfaceName() -> String? {
        engine.interfaceName
    }
}
