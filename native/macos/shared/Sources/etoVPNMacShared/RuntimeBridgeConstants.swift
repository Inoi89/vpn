import Foundation

public enum RuntimeBridgeConstants {
    public static let protocolVersion = "1"
    public static let defaultSocketFilename = "etoVPN.runtime.sock"
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
}
