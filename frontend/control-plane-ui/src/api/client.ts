import type { DashboardSnapshot, UpsertUserRequest, UserSummary } from '../types/dashboard'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? ''
const accessToken = import.meta.env.VITE_ACCESS_TOKEN ?? ''

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers)
  headers.set('Content-Type', 'application/json')

  if (accessToken) {
    headers.set('Authorization', `Bearer ${accessToken}`)
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers,
  })

  if (!response.ok) {
    throw new Error(`Request failed for ${path} with status ${response.status}`)
  }

  return response.json() as Promise<T>
}

export const apiClient = {
  accessToken,
  baseUrl: apiBaseUrl,
  getDashboard: () => request<DashboardSnapshot>('/api/dashboard?trafficPoints=120'),
  upsertUser: (payload: UpsertUserRequest) =>
    request<UserSummary>('/api/users', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
}
