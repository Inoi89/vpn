import Foundation

public struct TunnelProviderMessageRequest: Codable {
    public let action: String
    public let tunnelId: String?
    public let configuration: String?

    public init(action: String, tunnelId: String? = nil, configuration: String? = nil) {
        self.action = action
        self.tunnelId = tunnelId
        self.configuration = configuration
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

public struct TunnelProviderMessageRuntimeConfigurationResponse: Codable {
    public let interfaceName: String?
    public let configuration: String?

    public init(interfaceName: String?, configuration: String?) {
        self.interfaceName = interfaceName
        self.configuration = configuration
    }
}
