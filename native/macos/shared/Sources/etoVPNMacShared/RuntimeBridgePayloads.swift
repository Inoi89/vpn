import Foundation

public struct HelloRequestPayload: Codable {
    public let client: String
    public let clientVersion: String
    public let platform: String

    public init(client: String, clientVersion: String, platform: String) {
        self.client = client
        self.clientVersion = clientVersion
        self.platform = platform
    }
}

public typealias ConfigureRequestPayload = TunnelProfilePayload
public typealias ActivateRequestPayload = TunnelProfilePayload

public struct DeactivateRequestPayload: Codable {
    public let profileId: String?

    public init(profileId: String?) {
        self.profileId = profileId
    }
}

public struct HealthResponsePayload: Codable {
    public let helperVersion: String
    public let protocolVersion: String
    public let socketPath: String
    public let capabilities: [String]

    public init(
        helperVersion: String,
        protocolVersion: String,
        socketPath: String,
        capabilities: [String])
    {
        self.helperVersion = helperVersion
        self.protocolVersion = protocolVersion
        self.socketPath = socketPath
        self.capabilities = capabilities
    }
}

public struct StatusResponsePayload: Codable {
    public let connected: Bool
    public let state: RuntimeTunnelState
    public let profileId: String?
    public let profileName: String?
    public let serverEndpoint: String?
    public let deviceIpv4Address: String?
    public let deviceIpv6Address: String?
    public let dns: [String]
    public let mtu: Int?
    public let allowedIps: [String]
    public let routes: [String]
    public let rxBytes: Int64
    public let txBytes: Int64
    public let latestHandshakeAtUtc: String?
    public let warnings: [String]
    public let lastError: String?

    public init(
        connected: Bool,
        state: RuntimeTunnelState,
        profileId: String?,
        profileName: String?,
        serverEndpoint: String?,
        deviceIpv4Address: String?,
        deviceIpv6Address: String?,
        dns: [String],
        mtu: Int?,
        allowedIps: [String],
        routes: [String],
        rxBytes: Int64,
        txBytes: Int64,
        latestHandshakeAtUtc: String?,
        warnings: [String],
        lastError: String?)
    {
        self.connected = connected
        self.state = state
        self.profileId = profileId
        self.profileName = profileName
        self.serverEndpoint = serverEndpoint
        self.deviceIpv4Address = deviceIpv4Address
        self.deviceIpv6Address = deviceIpv6Address
        self.dns = dns
        self.mtu = mtu
        self.allowedIps = allowedIps
        self.routes = routes
        self.rxBytes = rxBytes
        self.txBytes = txBytes
        self.latestHandshakeAtUtc = latestHandshakeAtUtc
        self.warnings = warnings
        self.lastError = lastError
    }
}

public struct LogsResponsePayload: Codable {
    public let entries: [String]

    public init(entries: [String]) {
        self.entries = entries
    }
}

public struct QuitResponsePayload: Codable {
    public let accepted: Bool

    public init(accepted: Bool) {
        self.accepted = accepted
    }
}

public struct BridgeErrorPayload: Codable {
    public let code: String
    public let message: String
    public let details: String?

    public init(code: String, message: String, details: String?) {
        self.code = code
        self.message = message
        self.details = details
    }
}

public struct RuntimeBridgeRequestEnvelope<Payload: Codable>: Codable {
    public let id: String
    public let type: String
    public let command: RuntimeBridgeCommand
    public let payload: Payload

    public init(id: String, command: RuntimeBridgeCommand, payload: Payload) {
        self.id = id
        self.type = "request"
        self.command = command
        self.payload = payload
    }
}

public struct RuntimeBridgeSuccessEnvelope<Payload: Codable>: Codable {
    public let id: String?
    public let type: String
    public let ok: Bool
    public let payload: Payload

    public init(id: String? = nil, payload: Payload) {
        self.id = id
        self.type = "response"
        self.ok = true
        self.payload = payload
    }
}

public struct RuntimeBridgeFailureEnvelope: Codable {
    public let id: String?
    public let type: String
    public let ok: Bool
    public let error: BridgeErrorPayload

    public init(id: String? = nil, error: BridgeErrorPayload) {
        self.id = id
        self.type = "response"
        self.ok = false
        self.error = error
    }
}
