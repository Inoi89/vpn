import Foundation

final class ScaffoldWireGuardEngine: PacketTunnelEngine {
    private let dateFormatter = ISO8601DateFormatter()
    private let scaffoldInterfaceName = "etoVPN-scaffold"
    private let scaffoldEngineName = "scaffold-wireguard-engine"
    private var lastConfiguration: PacketTunnelConfiguration?
    private var logs: [String] = []

    var engineName: String {
        scaffoldEngineName
    }

    var interfaceName: String? {
        scaffoldInterfaceName
    }

    func start(
        configuration: PacketTunnelConfiguration,
        completion: @escaping (Result<TunnelRuntimeSnapshot, PacketTunnelEngineError>) -> Void)
    {
        lastConfiguration = configuration
        appendLog("Received start request for \(configuration.tunnelRemoteAddress).")
        completion(
            .failure(.notImplemented))
    }

    func update(
        configuration: PacketTunnelConfiguration,
        completion: @escaping (Result<TunnelRuntimeSnapshot, PacketTunnelEngineError>) -> Void)
    {
        lastConfiguration = configuration
        appendLog("Received runtime configuration update for \(configuration.tunnelRemoteAddress).")
        completion(
            .failure(.notImplemented))
    }

    func stop(
        completion: @escaping (Result<Void, PacketTunnelEngineError>) -> Void)
    {
        appendLog("Received stop request.")
        lastConfiguration = nil
        completion(.success(()))
    }

    func runtimeConfiguration() -> String? {
        lastConfiguration?.redactedSummary
    }

    func logEntries() -> [String] {
        logs
    }

    private func appendLog(_ message: String) {
        let timestamp = dateFormatter.string(from: Date())
        logs.append("[\(timestamp)] \(message)")

        if logs.count > 100 {
            logs.removeFirst(logs.count - 100)
        }
    }
}
