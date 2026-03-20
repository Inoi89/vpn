import Foundation

enum PacketTunnelEngineError: Error, LocalizedError {
    case notImplemented
    case invalidConfiguration(String)
    case startFailed(String)
    case stopFailed(String)

    var errorDescription: String? {
        switch self {
        case .notImplemented:
            return "The native WireGuard/AWG engine integration is not implemented yet."
        case .invalidConfiguration(let message):
            return "The packet tunnel configuration is invalid: \(message)"
        case .startFailed(let message):
            return "The native WireGuard/AWG engine failed to start: \(message)"
        case .stopFailed(let message):
            return "The native WireGuard/AWG engine failed to stop: \(message)"
        }
    }
}

protocol PacketTunnelEngine {
    func start(
        configuration: PacketTunnelConfiguration,
        completion: @escaping (Result<TunnelRuntimeSnapshot, PacketTunnelEngineError>) -> Void)

    func update(
        configuration: PacketTunnelConfiguration,
        completion: @escaping (Result<TunnelRuntimeSnapshot, PacketTunnelEngineError>) -> Void)

    func stop(
        completion: @escaping (Result<Void, PacketTunnelEngineError>) -> Void)

    func runtimeConfiguration() -> String?

    var interfaceName: String? { get }
}
