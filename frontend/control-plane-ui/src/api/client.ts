import type {
  AccessConfig,
  DeletedNodeAccess,
  DashboardSnapshot,
  IssueNodeAccessRequest,
  IssuedNodeAccess,
  SetNodeAccessStateRequest,
  UserSummary,
} from '../types/dashboard'

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
  issueNodeAccess: (nodeId: string, payload: IssueNodeAccessRequest) =>
    request<IssuedNodeAccess>(`/api/nodes/${nodeId}/accesses`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  setNodeAccessState: (nodeId: string, userId: string, payload: SetNodeAccessStateRequest) =>
    request<UserSummary>(`/api/nodes/${nodeId}/accesses/${userId}/state`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  deleteNodeAccess: (nodeId: string, userId: string) =>
    request<DeletedNodeAccess>(`/api/nodes/${nodeId}/accesses/${userId}`, {
      method: 'DELETE',
    }),
  getNodeAccessConfig: (nodeId: string, userId: string) =>
    request<AccessConfig>(`/api/nodes/${nodeId}/accesses/${userId}/config`),
}
