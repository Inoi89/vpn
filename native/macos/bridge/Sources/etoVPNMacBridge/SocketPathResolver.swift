import Foundation
import etoVPNMacShared

enum SocketPathResolver {
    private static let overrideEnvironmentVariable = "ETOVPN_RUNTIME_SOCKET_PATH"

    static func defaultSocketURL() -> URL {
        if let overridePath = ProcessInfo.processInfo.environment[overrideEnvironmentVariable],
           !overridePath.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        {
            return URL(fileURLWithPath: overridePath, isDirectory: false)
        }

        return FileManager.default.temporaryDirectory
            .appendingPathComponent(RuntimeBridgeConstants.defaultSocketFilename, isDirectory: false)
    }
}
