import Foundation

final class BridgeServer {
    private let socketPath: URL
    private let dispatcher: BridgeCommandDispatcher

    init(socketPath: URL, dispatcher: BridgeCommandDispatcher) {
        self.socketPath = socketPath
        self.dispatcher = dispatcher
    }

    func run() {
        // Placeholder only.
        //
        // The real implementation should:
        // 1. Remove any stale socket file at `socketPath`.
        // 2. Bind and listen on a Unix domain socket.
        // 3. Accept newline-delimited JSON requests from the desktop client.
        // 4. Decode `RuntimeBridgeCommandEnvelope` requests.
        // 5. Dispatch commands through `BridgeCommandDispatcher`.
        // 6. Encode a single-line JSON response for each request.
        // 7. Keep the latest tunnel status available for `status` polling.
        _ = socketPath
        _ = dispatcher
    }
}
