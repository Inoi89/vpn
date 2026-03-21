import Foundation
import NetworkExtension
import etoVPNMacShared

final class TunnelStatusPoller {
    private let managerStore: PacketTunnelManagerStore
    private var task: Task<Void, Never>?
    private static let defaultPollIntervalNanoseconds: UInt64 = 1_000_000_000

    init(managerStore: PacketTunnelManagerStore) {
        self.managerStore = managerStore
    }

    func start(
        manager: NETunnelProviderManager,
        intervalNanoseconds: UInt64 = TunnelStatusPoller.defaultPollIntervalNanoseconds,
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
                    try await Task.sleep(nanoseconds: intervalNanoseconds)
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
