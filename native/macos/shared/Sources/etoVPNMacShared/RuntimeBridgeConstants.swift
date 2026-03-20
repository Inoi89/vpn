import Foundation

public enum RuntimeBridgeConstants {
    public static let protocolVersion = "1"
    public static let defaultSocketFilename = "etoVPN.runtime.sock"
    public static let stagedProfileFilename = "etoVPN.staged-profile.json"
    public static let capabilities = [
        "activate",
        "configure",
        "deactivate",
        "status",
        "hello",
        "health",
        "logs",
        "quit"
    ]

    public static func stagedProfileURL() -> URL {
        FileManager.default.temporaryDirectory
            .appendingPathComponent(stagedProfileFilename, isDirectory: false)
    }
}
