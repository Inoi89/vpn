import Foundation
import NetworkExtension
import etoVPNMacShared

final class PacketTunnelProvider: NEPacketTunnelProvider {
    private let controlStore = TunnelControlStore()
    private let tunnelAdapter = WireGuardTunnelAdapter()
    private var activeConfiguration: TunnelConfiguration?

    override func startTunnel(
        options: [String : NSObject]?,
        completionHandler: @escaping (Error?) -> Void)
    {
        do {
            let providerProtocol = protocolConfiguration as? NETunnelProviderProtocol
            let configuration = try controlStore.loadConfiguration(from: providerProtocol)
            activeConfiguration = configuration
            applyScaffoldNetworkSettings(using: configuration)
            tunnelAdapter.start(with: configuration)
            completionHandler(PacketTunnelScaffoldError.notImplemented)
        } catch {
            completionHandler(error)
        }
    }

    override func stopTunnel(
        with reason: NEProviderStopReason,
        completionHandler: @escaping () -> Void)
    {
        tunnelAdapter.stop()
        activeConfiguration = nil
        try? controlStore.clearConfiguration()
        completionHandler()
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
                let isConnected = activeConfiguration != nil
                let response = TunnelProviderMessageStatusResponse(
                    connected: isConnected,
                    state: isConnected ? .connecting : .disconnected,
                    rxBytes: 0,
                    txBytes: 0,
                    latestHandshakeAtUtc: nil,
                    warnings: ["Packet tunnel provider status is still scaffolded."],
                    lastError: nil)
                completionHandler?(try? encoder.encode(response))

            default:
                completionHandler?(nil)
        }
    }

    private func applyScaffoldNetworkSettings(using configuration: TunnelConfiguration) {
        // Placeholder only.
        //
        // The real implementation should:
        // - decode the real payload from `protocolConfiguration.providerConfiguration`
        // - keep the staged control-store only as a scaffold fallback
        // - build `NEPacketTunnelNetworkSettings` from `configuration`
        // - apply DNS/routes/MTU
        // - start the tunnel backend before calling the completion handler
        _ = configuration
    }
}

private enum PacketTunnelScaffoldError: Error {
    case notImplemented
}
