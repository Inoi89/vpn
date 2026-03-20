import Foundation
import NetworkExtension
import etoVPNMacShared

final class PacketTunnelProvider: NEPacketTunnelProvider {
    private let controlStore = TunnelControlStore()
    private let tunnelAdapter = WireGuardTunnelAdapter()

    override func startTunnel(
        options: [String : NSObject]?,
        completionHandler: @escaping (Error?) -> Void)
    {
        do {
            let configuration = try controlStore.loadConfiguration()
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
        completionHandler()
    }

    private func applyScaffoldNetworkSettings(using configuration: TunnelConfiguration) {
        // Placeholder only.
        //
        // The real implementation should build `NEPacketTunnelNetworkSettings`
        // from `configuration`, apply DNS/routes/MTU, and then start the tunnel
        // backend before calling the completion handler.
        _ = configuration
    }
}

private enum PacketTunnelScaffoldError: Error {
    case notImplemented
}
