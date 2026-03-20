import Foundation

enum PacketTunnelEngineError: Error, LocalizedError {
    case invalidConfiguration(String)
    case invalidState
    case cannotLocateTunnelFileDescriptor
    case dnsResolution([String])
    case setNetworkSettings(String)
    case startWireGuardBackend(Int32)
    case startFailed(String)
    case updateFailed(String)
    case stopFailed(String)

    var errorDescription: String? {
        switch self {
        case .invalidConfiguration(let message):
            return "The packet tunnel configuration is invalid: \(message)"
        case .invalidState:
            return "The Apple packet tunnel engine received an operation in an invalid state."
        case .cannotLocateTunnelFileDescriptor:
            return "The Apple packet tunnel engine could not determine the utun file descriptor."
        case .dnsResolution(let endpoints):
            return "The Apple packet tunnel engine could not resolve: \(endpoints.joined(separator: ", "))."
        case .setNetworkSettings(let message):
            return "The Apple packet tunnel engine failed to apply network settings: \(message)"
        case .startWireGuardBackend(let errorCode):
            return "The Apple packet tunnel engine failed to start the WireGuard backend (\(errorCode))."
        case .startFailed(let message):
            return "The native WireGuard/AWG engine failed to start: \(message)"
        case .updateFailed(let message):
            return "The native WireGuard/AWG engine failed to update its runtime configuration: \(message)"
        case .stopFailed(let message):
            return "The native WireGuard/AWG engine failed to stop: \(message)"
        }
    }
}

protocol PacketTunnelEngine {
    var engineName: String { get }
    var interfaceName: String? { get }

    func start(
        configuration: PacketTunnelConfiguration,
        completion: @escaping (Result<TunnelRuntimeSnapshot, PacketTunnelEngineError>) -> Void)

    func update(
        configuration: PacketTunnelConfiguration,
        completion: @escaping (Result<TunnelRuntimeSnapshot, PacketTunnelEngineError>) -> Void)

    func stop(
        completion: @escaping (Result<Void, PacketTunnelEngineError>) -> Void)

    func runtimeConfiguration() -> String?
    func logEntries() -> [String]
}
