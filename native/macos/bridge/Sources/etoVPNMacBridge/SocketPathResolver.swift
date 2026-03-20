import Foundation
import etoVPNMacShared

enum SocketPathResolver {
    static func defaultSocketURL() -> URL {
        FileManager.default.temporaryDirectory
            .appendingPathComponent(RuntimeBridgeConstants.defaultSocketFilename, isDirectory: false)
    }
}
