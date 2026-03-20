import Foundation

public struct TunnelProviderMessageRequest: Codable {
    public let action: String
    public let tunnelId: String?

    public init(action: String, tunnelId: String? = nil) {
        self.action = action
        self.tunnelId = tunnelId
    }
}

public struct TunnelProviderMessageStatusResponse: Codable {
    public let connected: Bool
    public let state: RuntimeTunnelState
    public let rxBytes: Int64
    public let txBytes: Int64
    public let latestHandshakeAtUtc: String?
    public let warnings: [String]
    public let lastError: String?

    public init(
        connected: Bool,
        state: RuntimeTunnelState,
        rxBytes: Int64,
        txBytes: Int64,
        latestHandshakeAtUtc: String?,
        warnings: [String],
        lastError: String?)
    {
        self.connected = connected
        self.state = state
        self.rxBytes = rxBytes
        self.txBytes = txBytes
        self.latestHandshakeAtUtc = latestHandshakeAtUtc
        self.warnings = warnings
        self.lastError = lastError
    }
}
