import Foundation

public enum RuntimeBridgeConstants {
    public static let protocolVersion = "1"
    public static let defaultSocketFilename = "etoVPN.runtime.sock"
    public static let stagedProfileFilename = "etoVPN.staged-profile.json"
    public static let packetTunnelBundleIdentifier = "com.etovpn.packet-tunnel"
    public static let managerDescriptionPrefix = "etoVPN"
    public static let providerProfilePayloadKey = "wireguard"
    public static let providerProfileIdKey = "profileId"
    public static let providerProfileNameKey = "profileName"
    public static let providerConfigFormatKey = "configFormat"
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

    public static func managerDescription(for profileId: String) -> String {
        "\(managerDescriptionPrefix) \(profileId)"
    }
}
