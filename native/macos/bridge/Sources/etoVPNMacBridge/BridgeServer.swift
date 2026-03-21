import Foundation
import Darwin
import etoVPNMacShared

final class BridgeServer {
    private let socketPath: URL
    private let dispatcher: BridgeCommandDispatcher
    private var bufferedInput = ""
    private var shouldStop = false

    init(socketPath: URL, dispatcher: BridgeCommandDispatcher) {
        self.socketPath = socketPath
        self.dispatcher = dispatcher
    }

    func run() {
        bufferedInput = ""
        shouldStop = false

        try? FileManager.default.createDirectory(
            at: socketPath.deletingLastPathComponent(),
            withIntermediateDirectories: true)
        try? FileManager.default.removeItem(at: socketPath)

        signal(SIGPIPE, SIG_IGN)

        let serverSocket = socket(AF_UNIX, SOCK_STREAM, 0)
        guard serverSocket >= 0 else {
            reportSocketError("Failed to create the macOS runtime bridge socket.")
            return
        }

        defer {
            close(serverSocket)
            unlink(socketPath.path)
        }

        do {
            var (address, addressLength) = try makeSocketAddress()
            let bindResult = withUnsafePointer(to: &address) { pointer in
                pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { sockaddrPointer in
                    bind(serverSocket, sockaddrPointer, addressLength)
                }
            }

            guard bindResult == 0 else {
                reportSocketError("Failed to bind the macOS runtime bridge socket.")
                return
            }

            chmod(socketPath.path, mode_t(0o600))

            guard listen(serverSocket, SOMAXCONN) == 0 else {
                reportSocketError("Failed to listen on the macOS runtime bridge socket.")
                return
            }
        } catch {
            reportSocketError("Failed to prepare the macOS runtime bridge socket: \(error.localizedDescription)")
            return
        }

        while !shouldStop {
            let clientSocket = accept(serverSocket, nil, nil)
            if clientSocket < 0 {
                if errno == EINTR {
                    continue
                }

                reportSocketError("Failed to accept a macOS runtime bridge client.")
                continue
            }

            handleClient(clientSocket)
            close(clientSocket)
        }
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
            let request = try RuntimeBridgeWireCodec.decodeRequestLine(trimmedLine)
            let response = dispatcher.dispatchLine(request)
            if request.command == .quit {
                shouldStop = true
            }

            return response
        } catch {
            return RuntimeBridgeWireCodec.encodeFailureLine(
                id: nil,
                code: "invalid_request",
                message: "The bridge request could not be decoded.",
                details: error.localizedDescription)
        }
    }

    private func handleClient(_ clientSocket: Int32) {
        bufferedInput = ""

        while !shouldStop {
            var buffer = [UInt8](repeating: 0, count: 4096)
            let bytesRead = read(clientSocket, &buffer, buffer.count)
            if bytesRead == 0 {
                return
            }

            if bytesRead < 0 {
                if errno != EINTR {
                    reportSocketError("Failed to read from the macOS runtime bridge client.")
                }

                return
            }

            let data = Data(buffer[0..<bytesRead])
            let responses = consume(data)
            if responses.isEmpty {
                continue
            }

            for response in responses {
                writeResponse(response, to: clientSocket)
            }

            return
        }
    }

    private func writeResponse(_ response: String, to clientSocket: Int32) {
        let responseData = Data(response.utf8)
        var totalWritten = 0

        responseData.withUnsafeBytes { rawBuffer in
            guard let baseAddress = rawBuffer.baseAddress else {
                return
            }

            while totalWritten < responseData.count {
                let remaining = responseData.count - totalWritten
                let bytesWritten = write(
                    clientSocket,
                    baseAddress.advanced(by: totalWritten),
                    remaining)

                if bytesWritten <= 0 {
                    return
                }

                totalWritten += bytesWritten
            }
        }
    }

    private func makeSocketAddress() throws -> (sockaddr_un, socklen_t) {
        var address = sockaddr_un()
        address.sun_len = UInt8(MemoryLayout<sockaddr_un>.size)
        address.sun_family = sa_family_t(AF_UNIX)

        let pathBytes = socketPath.path.utf8CString
        let maxPathLength = MemoryLayout.size(ofValue: address.sun_path)
        guard pathBytes.count <= maxPathLength else {
            throw BridgeSocketError.socketPathTooLong
        }

        withUnsafeMutableBytes(of: &address.sun_path) { buffer in
            pathBytes.withUnsafeBytes { pathBuffer in
                guard let destination = buffer.baseAddress,
                      let source = pathBuffer.baseAddress
                else {
                    return
                }

                memcpy(destination, source, pathBytes.count)
            }
        }

        let addressLength = socklen_t(MemoryLayout.offset(of: \sockaddr_un.sun_path)! + pathBytes.count)
        return (address, addressLength)
    }

    private func reportSocketError(_ message: String) {
        let details = String(cString: strerror(errno))
        if let data = "[etoVPNMacBridge] \(message) \(details)\n".data(using: .utf8) {
            FileHandle.standardError.write(data)
        }
    }
}

private enum BridgeSocketError: Error {
    case socketPathTooLong
}
