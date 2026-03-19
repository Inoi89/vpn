export type RegisterAccountRequest = {
  email: string
  password: string
  displayName?: string | null
}

export type LoginRequest = {
  email: string
  password: string
}

export type RefreshTokenRequest = {
  refreshToken: string
}

export type SubscriptionSummary = {
  subscriptionId: string
  planName: string
  status: string
  maxDevices: number
  maxConcurrentSessions: number
  startsAtUtc: string
  endsAtUtc: string
}

export type AuthResponse = {
  accountId: string
  email: string
  displayName: string
  sessionId: string
  accessToken: string
  expiresAtUtc: string
  refreshToken: string
  refreshTokenExpiresAtUtc: string
}

export type MeResponse = {
  accountId: string
  email: string
  displayName: string
  status: string
  subscription: SubscriptionSummary | null
}

export type SessionResponse = {
  sessionId: string
  status: string
  ipAddress?: string | null
  userAgent?: string | null
  createdAtUtc: string
  lastSeenAtUtc: string
  expiresAtUtc: string
  isCurrent: boolean
}

export type DeviceResponse = {
  deviceId: string
  deviceName: string
  platform: string
  clientVersion?: string | null
  fingerprint: string
  status: string
  lastSeenAtUtc: string
}

export type AccessGrantResponse = {
  accessGrantId: string
  deviceId: string
  deviceName: string
  nodeId?: string | null
  peerPublicKey?: string | null
  configFormat: string
  status: string
  issuedAtUtc: string
  expiresAtUtc?: string | null
  revokedAtUtc?: string | null
}

export type StoredAuth = {
  accessToken: string
  refreshToken: string
  sessionId: string
  accountId: string
  email: string
  displayName: string
  expiresAtUtc: string
  refreshTokenExpiresAtUtc: string
}

export type CabinetProfile = {
  me: MeResponse
  sessions: SessionResponse[]
  devices: DeviceResponse[]
  accessGrants: AccessGrantResponse[]
}
