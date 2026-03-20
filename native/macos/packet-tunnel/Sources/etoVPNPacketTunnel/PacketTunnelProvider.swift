import Foundation
import NetworkExtension
import etoVPNMacShared

final class PacketTunnelProvider: NEPacketTunnelProvider {
    private let profileStore = TunnelProfileStore()
    private let tunnelAdapter = WireGuardTunnelAdapter()
    private var activeConfiguration: PacketTunnelConfiguration?

    override func startTunnel(
        options: [String : NSObject]?,
        completionHandler: @escaping (Error?) -> Void)
    {
        do {
            let providerProtocol = protocolConfiguration as? NETunnelProviderProtocol
            let configuration = try profileStore.loadConfiguration(from: providerProtocol)
            activeConfiguration = configuration
            try applyScaffoldNetworkSettings(using: configuration, completionHandler: completionHandler)
        } catch {
            tunnelAdapter.markFailed(error.localizedDescription)
            completionHandler(error)
        }
    }

    override func stopTunnel(
        with reason: NEProviderStopReason,
        completionHandler: @escaping () -> Void)
    {
        tunnelAdapter.stop { [weak self] _ in
            self?.activeConfiguration = nil
            try? self?.profileStore.clearProfile()
            completionHandler()
        }
    }

    override func handleAppMessage(_ messageData: Data, completionHandler: ((Data?) -> Void)? = nil) {
        let decoder = JSONDecoder()
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.sortedKeys]

        guard let request = try? decoder.decode(TunnelProviderMessageRequest.self, from: messageData) else {
            completionHandler?(nil)
            return
        }

        switch request.action
        {
            case "status":
                let response = tunnelAdapter.currentSnapshot().asProviderMessage()
                completionHandler?(try? encoder.encode(response))

            case "update":
                guard let activeConfiguration,
                      let configString = request.configuration?.trimmingCharacters(in: .whitespacesAndNewlines),
                      !configString.isEmpty
                else {
                    completionHandler?(nil)
                    return
                }

                do {
                    let updatedConfiguration = try PacketTunnelConfigurationBuilder.build(
                        fromWgQuickConfig: configString,
                        profileId: activeConfiguration.profileId,
                        profileName: activeConfiguration.profileName,
                        format: activeConfiguration.format)
                    tunnelAdapter.update(with: updatedConfiguration) { [weak self] engineError in
                        guard let self else {
                            completionHandler?(nil)
                            return
                        }

                        guard engineError == nil else {
                            completionHandler?(nil)
                            return
                        }

                        self.activeConfiguration = updatedConfiguration
                        let response = TunnelProviderMessageRuntimeConfigurationResponse(
                            interfaceName: self.tunnelAdapter.interfaceName(),
                            engineName: self.tunnelAdapter.currentSnapshot().engineName,
                            configuration: self.tunnelAdapter.preparedConfigurationSummary())
                        completionHandler?(try? encoder.encode(response))
                    }
                } catch {
                    completionHandler?(nil)
                }

            case "logs":
                let response = TunnelProviderMessageLogsResponse(entries: tunnelAdapter.logEntries())
                completionHandler?(try? encoder.encode(response))

            case "runtimeConfiguration":
                let response = TunnelProviderMessageRuntimeConfigurationResponse(
                    interfaceName: tunnelAdapter.interfaceName(),
                    engineName: tunnelAdapter.currentSnapshot().engineName,
                    configuration: tunnelAdapter.preparedConfigurationSummary())
                completionHandler?(try? encoder.encode(response))

            default:
                completionHandler?(nil)
        }
    }

    private func applyScaffoldNetworkSettings(
        using configuration: PacketTunnelConfiguration,
        completionHandler: @escaping (Error?) -> Void)
        throws
    {
        let settings = try PacketTunnelNetworkSettingsBuilder.build(from: configuration)
        setTunnelNetworkSettings(settings) { [weak self] error in
            guard let self else {
                completionHandler(error)
                return
            }

            if let error {
                self.tunnelAdapter.markFailed(error.localizedDescription)
                completionHandler(error)
                return
            }

            self.tunnelAdapter.start(with: configuration) { engineError in
                completionHandler(engineError)
            }
        }
    }
}
