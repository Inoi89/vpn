import Foundation

struct BridgeApplication {
    let socketPath: URL
    let server: BridgeServer

    static func `default`() -> BridgeApplication {
        let socketPath = SocketPathResolver.defaultSocketURL()
        let statusStore = StatusSnapshotStore()
        let diagnosticsStore = ProviderDiagnosticsStore()
        let stagingStore = TunnelProfileStagingStore()
        let managerStore = PacketTunnelManagerStore()
        let statusObserver = TunnelManagerStatusObserver()
        let statusPoller = TunnelStatusPoller(managerStore: managerStore)
        let coordinator = PacketTunnelCoordinator(
            stagingStore: stagingStore,
            statusStore: statusStore,
            diagnosticsStore: diagnosticsStore,
            managerStore: managerStore,
            statusObserver: statusObserver,
            statusPoller: statusPoller)
        let dispatcher = BridgeCommandDispatcher(
            socketPath: socketPath,
            coordinator: coordinator,
            statusStore: statusStore,
            diagnosticsStore: diagnosticsStore)
        let server = BridgeServer(
            socketPath: socketPath,
            dispatcher: dispatcher)

        return BridgeApplication(
            socketPath: socketPath,
            server: server)
    }

    func run() {
        server.run()
    }
}
