import Foundation
import NetworkExtension

final class TunnelManagerStatusObserver {
    private var token: NSObjectProtocol?

    func start(
        observing manager: NETunnelProviderManager,
        onStatusChanged: @escaping (NEVPNStatus) -> Void)
    {
        stop()

        token = NotificationCenter.default.addObserver(
            forName: .NEVPNStatusDidChange,
            object: manager.connection,
            queue: .main)
        { _ in
            onStatusChanged(manager.connection.status)
        }
    }

    func stop() {
        if let token {
            NotificationCenter.default.removeObserver(token)
            self.token = nil
        }
    }
}
