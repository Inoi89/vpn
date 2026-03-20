import Foundation

final class ScaffoldWireGuardEngine: PacketTunnelEngine {
    private let scaffoldInterfaceName = "etoVPN-scaffold"
    private var lastConfiguration: PacketTunnelConfiguration?

    var interfaceName: String? {
        scaffoldInterfaceName
    }

    func start(
        configuration: PacketTunnelConfiguration,
        completion: @escaping (Result<TunnelRuntimeSnapshot, PacketTunnelEngineError>) -> Void)
    {
        lastConfiguration = configuration
        completion(
            .failure(.notImplemented))
    }

    func update(
        configuration: PacketTunnelConfiguration,
        completion: @escaping (Result<TunnelRuntimeSnapshot, PacketTunnelEngineError>) -> Void)
    {
        lastConfiguration = configuration
        completion(
            .failure(.notImplemented))
    }

    func stop(
        completion: @escaping (Result<Void, PacketTunnelEngineError>) -> Void)
    {
        lastConfiguration = nil
        completion(.success(()))
    }

    func runtimeConfiguration() -> String? {
        lastConfiguration?.redactedSummary
    }
}
