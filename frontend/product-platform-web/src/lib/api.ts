import type {
  AccessGrantResponse,
  AuthResponse,
  DeviceResponse,
  LoginRequest,
  MeResponse,
  RefreshTokenRequest,
  RegisterAccountRequest,
  SessionResponse,
  StoredAuth,
} from './types'

const envBaseUrl = import.meta.env.VITE_API_BASE_URL?.trim() ?? ''

export type ApiError = Error & { status?: number }

type RequestContext = {
  baseUrl?: string
  accessToken?: string | null
}

export function createCabinetApi(context: RequestContext = {}) {
  const baseUrl = (context.baseUrl ?? envBaseUrl).replace(/\/$/, '')

  async function request<T>(path: string, init?: RequestInit): Promise<T> {
    const headers = new Headers(init?.headers)
    headers.set('Accept', 'application/json')

    if (init?.body !== undefined) {
      headers.set('Content-Type', 'application/json')
    }

    if (context.accessToken) {
      headers.set('Authorization', `Bearer ${context.accessToken}`)
    }

    const response = await fetch(`${baseUrl}${path}`, {
      ...init,
      headers,
    })

    if (!response.ok) {
      const error = new Error(await readErrorMessage(response)) as ApiError
      error.status = response.status
      throw error
    }

    if (response.status === 204) {
      return undefined as T
    }

    return (await response.json()) as T
  }

  return {
    baseUrl,
    register(payload: RegisterAccountRequest): Promise<AuthResponse> {
      return request<AuthResponse>('/api/auth/register', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
    },
    login(payload: LoginRequest): Promise<AuthResponse> {
      return request<AuthResponse>('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
    },
    refresh(payload: RefreshTokenRequest): Promise<AuthResponse> {
      return request<AuthResponse>('/api/auth/refresh', {
        method: 'POST',
        body: JSON.stringify(payload),
      })
    },
    logout(): Promise<void> {
      return request<void>('/api/auth/logout', { method: 'POST' })
    },
    me(): Promise<MeResponse> {
      return request<MeResponse>('/api/me')
    },
    sessions(): Promise<SessionResponse[]> {
      return request<SessionResponse[]>('/api/sessions')
    },
    devices(): Promise<DeviceResponse[]> {
      return request<DeviceResponse[]>('/api/devices')
    },
    accessGrants(): Promise<AccessGrantResponse[]> {
      return request<AccessGrantResponse[]>('/api/access-grants')
    },
    revokeDevice(deviceId: string): Promise<void> {
      return request<void>(`/api/devices/${deviceId}`, { method: 'DELETE' })
    },
    revokeSession(sessionId: string): Promise<void> {
      return request<void>(`/api/sessions/${sessionId}`, { method: 'DELETE' })
    },
  }
}

async function readErrorMessage(response: Response): Promise<string> {
  const fallback = `Request failed with status ${response.status}`
  const contentType = response.headers.get('Content-Type') ?? ''

  try {
    if (contentType.includes('application/json')) {
      const payload = (await response.json()) as { error?: string; title?: string; detail?: string; message?: string }
      return payload.error ?? payload.title ?? payload.detail ?? payload.message ?? fallback
    }

    const text = (await response.text()).trim()
    return text || fallback
  } catch {
    return fallback
  }
}

export function toStoredAuth(auth: AuthResponse): StoredAuth {
  return {
    accessToken: auth.accessToken,
    refreshToken: auth.refreshToken,
    sessionId: auth.sessionId,
    accountId: auth.accountId,
    email: auth.email,
    displayName: auth.displayName,
    expiresAtUtc: auth.expiresAtUtc,
    refreshTokenExpiresAtUtc: auth.refreshTokenExpiresAtUtc,
  }
}
