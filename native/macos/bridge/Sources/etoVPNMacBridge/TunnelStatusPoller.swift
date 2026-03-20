import Foundation
import NetworkExtension
import etoVPNMacShared

final class TunnelStatusPoller {
    private let managerStore: PacketTunnelManagerStore
    private var task: Task<Void, Never>?

    init(managerStore: PacketTunnelManagerStore) {
        self.managerStore = managerStore
    }

    func start(
        manager: NETunnelProviderManager,
        interval: Duration = .seconds(1),
        onStatus: @escaping (TunnelProviderMessageStatusResponse) -> Void,
        onFailure: @escaping (Error) -> Void)
    {
        stop()

        task = Task {
            while !Task.isCancelled {
                do {
                    if let response = try await managerStore.requestStatus(from: manager) {
                        onStatus(response)
                    }
                } catch {
                    onFailure(error)
                }

                do {
                    try await Task.sleep(for: interval)
                } catch {
                    return
                }
            }
        }
    }

    func stop() {
        task?.cancel()
        task = nil
    }
}
