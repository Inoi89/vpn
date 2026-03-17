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
  email?: string
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
