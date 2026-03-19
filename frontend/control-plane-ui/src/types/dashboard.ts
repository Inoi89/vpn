export type AccessConfigFormat = 'amnezia-awg-native' | 'amnezia-vpn'

export type NodeSummary = {
  id: string
  agentIdentifier: string
  name: string
  cluster: string
  agentBaseAddress: string
  status: string
  agentVersion?: string | null
  lastSeenAtUtc?: string | null
  activeSessions: number
  enabledPeerCount: number
  lastError?: string | null
}

export type SessionSummary = {
  id: string
  nodeId: string
  userId: string
  nodeName: string
  userDisplayName: string
  publicKey: string
  endpoint?: string | null
  connectedAtUtc?: string | null
  latestHandshakeAtUtc?: string | null
  rxBytes: number
  txBytes: number
  state: string
}

export type UserSummary = {
  id: string
  externalId: string
  displayName: string
  email?: string | null
  isEnabled: boolean
  peerCount: number
  nodeIds: string[]
  enabledNodeIds: string[]
  lastActivityAtUtc?: string | null
}

export type AccessSummary = {
  id: string
  nodeId: string
  userId: string
  nodeName: string
  externalId: string
  displayName: string
  email?: string | null
  publicKey: string
  allowedIps: string
  protocol: string
  isEnabled: boolean
  lastSyncedAtUtc: string
  endpoint?: string | null
  sessionState: string
  latestHandshakeAtUtc?: string | null
  lastActivityAtUtc?: string | null
  accountId?: string | null
  accountEmail?: string | null
  accountDisplayName?: string | null
  deviceId?: string | null
  deviceName?: string | null
  devicePlatform?: string | null
  deviceFingerprint?: string | null
  clientVersion?: string | null
}

export type TrafficPoint = {
  capturedAtUtc: string
  userDisplayName: string
  rxBytes: number
  txBytes: number
}

export type DashboardSnapshot = {
  nodes: NodeSummary[]
  sessions: SessionSummary[]
  users: UserSummary[]
  accesses: AccessSummary[]
  traffic: TrafficPoint[]
}

export type NodeRealtimeEnvelope = {
  nodeId: string
  nodeName: string
  observedAtUtc: string
  sessions: SessionSummary[]
}

export type IssueNodeAccessRequest = {
  displayName: string
  configFormat: AccessConfigFormat
}

export type IssuedNodeAccess = {
  nodeId: string
  userId: string
  externalId: string
  displayName: string
  email?: string | null
  publicKey: string
  allowedIps: string
  clientConfigFileName: string
  clientConfig: string
}

export type SetNodeAccessStateRequest = {
  isEnabled: boolean
}

export type DeletedNodeAccess = {
  nodeId: string
  accessId: string
  userId: string
  publicKey: string
  userDeleted: boolean
}

export type AccessConfig = {
  nodeId: string
  accessId: string
  userId: string
  publicKey: string
  clientConfigFileName: string
  clientConfig: string
}
