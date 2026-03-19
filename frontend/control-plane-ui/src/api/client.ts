import type {
  AccessConfig,
  AccessConfigFormat,
  DeletedNodeAccess,
  DashboardSnapshot,
  IssueNodeAccessRequest,
  IssuedNodeAccess,
  SetNodeAccessStateRequest,
  AccessSummary,
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
    throw new Error(await buildRequestErrorMessage(path, response))
  }

  return response.json() as Promise<T>
}

async function buildRequestErrorMessage(path: string, response: Response): Promise<string> {
  const fallbackMessage = `Request failed for ${path} with status ${response.status}`
  const contentType = response.headers.get('Content-Type') ?? ''

  try {
    if (contentType.includes('application/json')) {
      const payload = (await response.json()) as {
        error?: string
        detail?: string
        title?: string
        message?: string
      }

      return payload.error ?? payload.detail ?? payload.message ?? payload.title ?? fallbackMessage
    }

    const text = (await response.text()).trim()
    return text || fallbackMessage
  } catch {
    return fallbackMessage
  }
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
  setNodeAccessState: (nodeId: string, accessId: string, payload: SetNodeAccessStateRequest) =>
    request<AccessSummary>(`/api/nodes/${nodeId}/accesses/${accessId}/state`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  deleteNodeAccess: (nodeId: string, accessId: string) =>
    request<DeletedNodeAccess>(`/api/nodes/${nodeId}/accesses/${accessId}`, {
      method: 'DELETE',
    }),
  getNodeAccessConfig: (nodeId: string, accessId: string, format: AccessConfigFormat) =>
    request<AccessConfig>(`/api/nodes/${nodeId}/accesses/${accessId}/config?format=${encodeURIComponent(format)}`),
}
