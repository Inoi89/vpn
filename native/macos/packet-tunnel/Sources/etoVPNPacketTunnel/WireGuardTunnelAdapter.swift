import Foundation

final class WireGuardTunnelAdapter {
    private var snapshot = TunnelRuntimeSnapshot.disconnected()
    private var activeConfiguration: PacketTunnelConfiguration?
    private let engine: PacketTunnelEngine

    init(engine: PacketTunnelEngine) {
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
                    engineName: self.engine.engineName,
                    interfaceName: self.engine.interfaceName,
                    runtimeConfigurationSummary: self.engine.runtimeConfiguration() ?? configuration.redactedSummary,
                    warnings: [],
                    lastError: error.localizedDescription)
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
                    engineName: self.engine.engineName,
                    interfaceName: self.engine.interfaceName,
                    runtimeConfigurationSummary: self.engine.runtimeConfiguration() ?? self.activeConfiguration?.redactedSummary,
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
                completion(.updateFailed("The packet tunnel adapter was released before the engine updated."))
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
                    engineName: self.engine.engineName,
                    interfaceName: self.engine.interfaceName,
                    runtimeConfigurationSummary: self.engine.runtimeConfiguration() ?? configuration.redactedSummary,
                    warnings: [],
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
            engineName: engine.engineName,
            interfaceName: engine.interfaceName,
            runtimeConfigurationSummary: engine.runtimeConfiguration() ?? activeConfiguration?.redactedSummary,
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

    func logEntries() -> [String] {
        engine.logEntries()
    }
}
