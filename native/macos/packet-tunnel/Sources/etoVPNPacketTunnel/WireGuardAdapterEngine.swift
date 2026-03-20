import Foundation
import NetworkExtension

final class WireGuardAdapterEngine: PacketTunnelEngine {
    private weak var provider: NEPacketTunnelProvider?
    private var adapter: WireGuardAdapter?
    private var lastRuntimeConfiguration: String?
    private var lastInterfaceName: String?
    private var logs: [String] = []
    private let dateFormatter = ISO8601DateFormatter()

    init(provider: NEPacketTunnelProvider) {
        self.provider = provider
        dateFormatter.formatOptions = [.withInternetDateTime]
    }

    var engineName: String {
        "amneziawg-apple"
    }

    var interfaceName: String? {
        adapter?.interfaceName ?? lastInterfaceName
    }

    func start(
        configuration: PacketTunnelConfiguration,
        completion: @escaping (Result<TunnelRuntimeSnapshot, PacketTunnelEngineError>) -> Void)
    {
        guard let provider else {
            completion(.failure(.startFailed("The packet tunnel provider was released before engine startup.")))
            return
        }

        guard adapter == nil else {
            completion(.failure(.invalidState))
            return
        }

        do {
            let tunnelConfiguration = try WireGuardTunnelConfigurationFactory.build(from: configuration)
            appendLog(level: "verbose", message: "Starting WireGuard adapter for \(configuration.tunnelRemoteAddress).")

            let adapter = WireGuardAdapter(with: provider) { [weak self] logLevel, message in
                self?.appendLog(logLevel: logLevel, message: message)
            }
            self.adapter = adapter

            adapter.start(tunnelConfiguration: tunnelConfiguration) { [weak self] error in
                guard let self else {
                    completion(.failure(.startFailed("The packet tunnel engine was released before startup completed.")))
                    return
                }

                if let error {
                    self.appendLog(level: "error", message: "WireGuard adapter start failed: \(error.localizedDescription)")
                    self.adapter = nil
                    completion(.failure(Self.map(error, operation: "start")))
                    return
                }

                self.captureRuntimeSnapshot(
                    defaultConfigurationSummary: configuration.redactedSummary,
                    completion: completion)
            }
        } catch let error as PacketTunnelEngineError {
            completion(.failure(error))
        } catch {
            completion(.failure(.invalidConfiguration(error.localizedDescription)))
        }
    }

    func update(
        configuration: PacketTunnelConfiguration,
        completion: @escaping (Result<TunnelRuntimeSnapshot, PacketTunnelEngineError>) -> Void)
    {
        guard let adapter else {
            completion(.failure(.invalidState))
            return
        }

        do {
            let tunnelConfiguration = try WireGuardTunnelConfigurationFactory.build(from: configuration)
            appendLog(level: "verbose", message: "Updating WireGuard adapter for \(configuration.tunnelRemoteAddress).")

            adapter.update(tunnelConfiguration: tunnelConfiguration) { [weak self] error in
                guard let self else {
                    completion(.failure(.updateFailed("The packet tunnel engine was released before the configuration update completed.")))
                    return
                }

                if let error {
                    self.appendLog(level: "error", message: "WireGuard adapter update failed: \(error.localizedDescription)")
                    completion(.failure(Self.map(error, operation: "update")))
                    return
                }

                self.captureRuntimeSnapshot(
                    defaultConfigurationSummary: configuration.redactedSummary,
                    completion: completion)
            }
        } catch let error as PacketTunnelEngineError {
            completion(.failure(error))
        } catch {
            completion(.failure(.invalidConfiguration(error.localizedDescription)))
        }
    }

    func stop(
        completion: @escaping (Result<Void, PacketTunnelEngineError>) -> Void)
    {
        guard let adapter else {
            completion(.success(()))
            return
        }

        appendLog(level: "verbose", message: "Stopping WireGuard adapter.")
        adapter.stop { [weak self] error in
            guard let self else {
                completion(.failure(.stopFailed("The packet tunnel engine was released before the stop completed.")))
                return
            }

            self.adapter = nil
            self.lastRuntimeConfiguration = nil
            self.lastInterfaceName = nil

            if let error {
                self.appendLog(level: "error", message: "WireGuard adapter stop failed: \(error.localizedDescription)")
                completion(.failure(.stopFailed(error.localizedDescription)))
                return
            }

            completion(.success(()))
        }
    }

    func runtimeConfiguration() -> String? {
        WireGuardRuntimeConfigurationParser.parse(lastRuntimeConfiguration).redactedConfiguration
    }

    func logEntries() -> [String] {
        logs
    }

    private func captureRuntimeSnapshot(
        defaultConfigurationSummary: String,
        completion: @escaping (Result<TunnelRuntimeSnapshot, PacketTunnelEngineError>) -> Void)
    {
        guard let adapter else {
            completion(.failure(.invalidState))
            return
        }

        adapter.getRuntimeConfiguration { [weak self] runtimeConfiguration in
            guard let self else {
                completion(.failure(.startFailed("The packet tunnel engine was released before the runtime snapshot was captured.")))
                return
            }

            self.lastRuntimeConfiguration = runtimeConfiguration
            self.lastInterfaceName = adapter.interfaceName
            let parsedStatus = WireGuardRuntimeConfigurationParser.parse(runtimeConfiguration)

            completion(
                .success(
                    TunnelRuntimeSnapshot(
                        connected: true,
                        state: .connected,
                        rxBytes: parsedStatus.rxBytes,
                        txBytes: parsedStatus.txBytes,
                        latestHandshakeAtUtc: parsedStatus.latestHandshakeAtUtc,
                        engineName: self.engineName,
                        interfaceName: adapter.interfaceName,
                        runtimeConfigurationSummary: parsedStatus.redactedConfiguration ?? defaultConfigurationSummary,
                        warnings: runtimeConfiguration == nil ? ["WireGuard started, but the runtime configuration is not available yet."] : [],
                        lastError: nil)))
        }
    }

    private func appendLog(logLevel: WireGuardLogLevel, message: String) {
        switch logLevel {
        case .error:
            appendLog(level: "error", message: message)
        case .verbose:
            appendLog(level: "verbose", message: message)
        }
    }

    private func appendLog(level: String, message: String) {
        let timestamp = dateFormatter.string(from: Date())
        logs.append("[\(timestamp)] [\(level)] \(message)")

        if logs.count > 200 {
            logs.removeFirst(logs.count - 200)
        }
    }

    private static func map(_ error: WireGuardAdapterError, operation: String) -> PacketTunnelEngineError {
        switch error {
        case .cannotLocateTunnelFileDescriptor:
            return .cannotLocateTunnelFileDescriptor
        case .invalidState:
            return operation == "update"
                ? .updateFailed("The WireGuard adapter rejected the request because it is in an invalid state.")
                : .invalidState
        case .dnsResolution(let dnsErrors):
            return .dnsResolution(dnsErrors.map(\.address))
        case .setNetworkSettings(let underlyingError):
            return .setNetworkSettings(underlyingError.localizedDescription)
        case .startWireGuardBackend(let errorCode):
            return .startWireGuardBackend(errorCode)
        }
    }
}
