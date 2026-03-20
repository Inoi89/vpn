import Foundation

struct BridgeApplication {
    let socketPath: URL
    let server: BridgeServer

    static func `default`() -> BridgeApplication {
        let socketPath = SocketPathResolver.defaultSocketURL()
        let statusStore = StatusSnapshotStore()
        let coordinator = PacketTunnelCoordinator(statusStore: statusStore)
        let dispatcher = BridgeCommandDispatcher(
            coordinator: coordinator,
            statusStore: statusStore)
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
