import Foundation

final class BridgeServer {
    private let socketPath: URL
    private let dispatcher: BridgeCommandDispatcher
    private var bufferedInput = ""

    init(socketPath: URL, dispatcher: BridgeCommandDispatcher) {
        self.socketPath = socketPath
        self.dispatcher = dispatcher
    }

    func run() {
        // The transport loop is still a scaffold, but the request pipeline is
        // now explicit: cleanup, newline framing, envelope decoding, and
        // dispatch all have dedicated entry points.
        try? FileManager.default.removeItem(at: socketPath)
    }

    func consume(_ data: Data) -> [String] {
        guard let chunk = String(data: data, encoding: .utf8) else {
            return [RuntimeBridgeWireCodec.encodeFailureLine(
                id: nil,
                code: "invalid_transport",
                message: "The bridge transport received invalid UTF-8.",
                details: nil) + "\n"]
        }

        return ingest(chunk)
    }

    func ingest(_ chunk: String) -> [String] {
        bufferedInput += chunk

        var responses: [String] = []
        while let newlineRange = bufferedInput.range(of: "\n") {
            let line = String(bufferedInput[..<newlineRange.lowerBound])
            bufferedInput.removeSubrange(bufferedInput.startIndex..<newlineRange.upperBound)

            if let frame = handle(line: line) {
                responses.append(frame + "\n")
            }
        }

        return responses
    }

    func handle(line: String) -> String? {
        let trimmedLine = line.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmedLine.isEmpty else {
            return nil
        }

        do {
            return try BridgeLineProtocol.process(trimmedLine, dispatcher: dispatcher)
        } catch {
            return RuntimeBridgeWireCodec.encodeFailureLine(
                id: nil,
                code: "invalid_request",
                message: "The bridge request could not be decoded.",
                details: error.localizedDescription)
        }
    }
}
