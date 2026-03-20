import Foundation

public struct TunnelProfilePayload: Codable {
    public let profileId: String
    public let profileName: String
    public let sourceFormat: String?
    public let sourceFileName: String?
    public let endpoint: String?
    public let address: String?
    public let dns: [String]
    public let mtu: Int?
    public let allowedIps: [String]
    public let publicKey: String?
    public let presharedKey: String?
    public let privateKey: String?
    public let rawConfig: String
    public let rawPackageJson: String?
    public let tunnelConfig: TunnelConfigurationPayload
    public let managedProfile: ManagedProfilePayload?

    public init(
        profileId: String,
        profileName: String,
        sourceFormat: String?,
        sourceFileName: String?,
        endpoint: String?,
        address: String?,
        dns: [String],
        mtu: Int?,
        allowedIps: [String],
        publicKey: String?,
        presharedKey: String?,
        privateKey: String?,
        rawConfig: String,
        rawPackageJson: String?,
        tunnelConfig: TunnelConfigurationPayload,
        managedProfile: ManagedProfilePayload?)
    {
        self.profileId = profileId
        self.profileName = profileName
        self.sourceFormat = sourceFormat
        self.sourceFileName = sourceFileName
        self.endpoint = endpoint
        self.address = address
        self.dns = dns
        self.mtu = mtu
        self.allowedIps = allowedIps
        self.publicKey = publicKey
        self.presharedKey = presharedKey
        self.privateKey = privateKey
        self.rawConfig = rawConfig
        self.rawPackageJson = rawPackageJson
        self.tunnelConfig = tunnelConfig
        self.managedProfile = managedProfile
    }
}

public struct TunnelConfigurationPayload: Codable {
    public let format: String
    public let address: String?
    public let dns: [String]
    public let mtu: Int?
    public let allowedIps: [String]
    public let splitTunnelType: Int?
    public let splitTunnelSites: [String]?
    public let endpoint: String?
    public let publicKey: String?
    public let presharedKey: String?
    public let persistentKeepalive: Int?
    public let interfaceValues: [String: String]
    public let peerValues: [String: String]
    public let awgValues: [String: String]

    public init(
        format: String,
        address: String?,
        dns: [String],
        mtu: Int?,
        allowedIps: [String],
        splitTunnelType: Int? = nil,
        splitTunnelSites: [String]? = nil,
        endpoint: String?,
        publicKey: String?,
        presharedKey: String?,
        persistentKeepalive: Int?,
        interfaceValues: [String: String],
        peerValues: [String: String],
        awgValues: [String: String])
    {
        self.format = format
        self.address = address
        self.dns = dns
        self.mtu = mtu
        self.allowedIps = allowedIps
        self.splitTunnelType = splitTunnelType
        self.splitTunnelSites = splitTunnelSites
        self.endpoint = endpoint
        self.publicKey = publicKey
        self.presharedKey = presharedKey
        self.persistentKeepalive = persistentKeepalive
        self.interfaceValues = interfaceValues
        self.peerValues = peerValues
        self.awgValues = awgValues
    }
}

public struct ManagedProfilePayload: Codable {
    public let accountId: String
    public let accountEmail: String
    public let deviceId: String
    public let accessGrantId: String
    public let nodeId: String
    public let controlPlaneAccessId: String?
    public let configFormat: String

    public init(
        accountId: String,
        accountEmail: String,
        deviceId: String,
        accessGrantId: String,
        nodeId: String,
        controlPlaneAccessId: String?,
        configFormat: String)
    {
        self.accountId = accountId
        self.accountEmail = accountEmail
        self.deviceId = deviceId
        self.accessGrantId = accessGrantId
        self.nodeId = nodeId
        self.controlPlaneAccessId = controlPlaneAccessId
        self.configFormat = configFormat
    }
}
