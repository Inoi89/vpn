import Foundation
import NetworkExtension
import etoVPNMacShared

final class PacketTunnelManagerStore {
    func loadOrCreateManager(for profile: TunnelProfilePayload) async throws -> NETunnelProviderManager {
        let managers = try await loadManagers()
        if let existing = managers.first(where: { matches($0, profileId: profile.profileId) }) {
            return existing
        }

        let manager = NETunnelProviderManager()
        manager.localizedDescription = RuntimeBridgeConstants.managerDescription(for: profile.profileId)
        manager.isEnabled = true
        return manager
    }

    func configure(_ manager: NETunnelProviderManager, with profile: TunnelProfilePayload) async throws {
        let providerProtocol = (manager.protocolConfiguration as? NETunnelProviderProtocol) ?? NETunnelProviderProtocol()
        providerProtocol.providerBundleIdentifier = RuntimeBridgeConstants.packetTunnelBundleIdentifier
        providerProtocol.serverAddress = profile.endpoint ?? profile.profileName
        providerProtocol.providerConfiguration = try PacketTunnelProviderConfigurationFactory.makeProviderConfiguration(for: profile)

        manager.protocolConfiguration = providerProtocol
        manager.localizedDescription = RuntimeBridgeConstants.managerDescription(for: profile.profileId)
        manager.isEnabled = true

        try await save(manager)
        try await load(manager)
    }

    func start(_ manager: NETunnelProviderManager) throws {
        guard let session = manager.connection as? NETunnelProviderSession else {
            throw PacketTunnelManagerStoreError.unexpectedSessionType
        }

        try session.startVPNTunnel(options: nil)
    }

    func requestStatus(from manager: NETunnelProviderManager) async throws -> TunnelProviderMessageStatusResponse? {
        try await requestProviderMessage(
            from: manager,
            action: "status",
            as: TunnelProviderMessageStatusResponse.self)
    }

    func requestLogs(from manager: NETunnelProviderManager) async throws -> TunnelProviderMessageLogsResponse? {
        try await requestProviderMessage(
            from: manager,
            action: "logs",
            as: TunnelProviderMessageLogsResponse.self)
    }

    func requestRuntimeConfiguration(
        from manager: NETunnelProviderManager) async throws -> TunnelProviderMessageRuntimeConfigurationResponse?
    {
        try await requestProviderMessage(
            from: manager,
            action: "runtimeConfiguration",
            as: TunnelProviderMessageRuntimeConfigurationResponse.self)
    }

    func requestUpdate(
        from manager: NETunnelProviderManager,
        configuration: String) async throws -> TunnelProviderMessageRuntimeConfigurationResponse?
    {
        try await requestProviderMessage(
            from: manager,
            action: "update",
            configuration: configuration,
            as: TunnelProviderMessageRuntimeConfigurationResponse.self)
    }

    func stop(_ manager: NETunnelProviderManager) {
        guard let session = manager.connection as? NETunnelProviderSession else {
            return
        }

        session.stopTunnel()
    }

    private func matches(_ manager: NETunnelProviderManager, profileId: String) -> Bool {
        guard let providerProtocol = manager.protocolConfiguration as? NETunnelProviderProtocol,
              let providerConfiguration = providerProtocol.providerConfiguration,
              let configuredProfileId = providerConfiguration[RuntimeBridgeConstants.providerProfileIdKey] as? String
        else {
            return false
        }

        return configuredProfileId == profileId
    }

    private func loadManagers() async throws -> [NETunnelProviderManager] {
        try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<[NETunnelProviderManager], Error>) in
            NETunnelProviderManager.loadAllFromPreferences { managers, error in
                if let error {
                    continuation.resume(throwing: error)
                    return
                }

                continuation.resume(returning: managers ?? [])
            }
        }
    }

    private func save(_ manager: NETunnelProviderManager) async throws {
        try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
            manager.saveToPreferences { error in
                if let error {
                    continuation.resume(throwing: error)
                    return
                }

                continuation.resume(returning: ())
            }
        }
    }

    private func load(_ manager: NETunnelProviderManager) async throws {
        try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in
            manager.loadFromPreferences { error in
                if let error {
                    continuation.resume(throwing: error)
                    return
                }

                continuation.resume(returning: ())
            }
        }
    }

    private func requestProviderMessage<Response: Decodable>(
        from manager: NETunnelProviderManager,
        action: String,
        configuration: String? = nil,
        as responseType: Response.Type) async throws -> Response?
    {
        guard let session = manager.connection as? NETunnelProviderSession else {
            throw PacketTunnelManagerStoreError.unexpectedSessionType
        }

        let request = TunnelProviderMessageRequest(action: action, configuration: configuration)
        let encoder = JSONEncoder()
        let payload = try encoder.encode(request)

        let responseData = try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Data?, Error>) in
            do {
                try session.sendProviderMessage(payload) { response in
                    continuation.resume(returning: response)
                }
            } catch {
                continuation.resume(throwing: error)
            }
        }

        guard let responseData else {
            return nil
        }

        let decoder = JSONDecoder()
        return try decoder.decode(responseType, from: responseData)
    }
}

enum PacketTunnelManagerStoreError: Error {
    case unexpectedSessionType
}
