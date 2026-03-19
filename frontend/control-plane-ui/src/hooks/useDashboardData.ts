import { useEffect, useEffectEvent, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { apiClient } from '../api/client'
import type {
  AccessConfig,
  AccessConfigFormat,
  DeletedNodeAccess,
  DashboardSnapshot,
  IssueNodeAccessRequest,
  IssuedNodeAccess,
  NodeRealtimeEnvelope,
  SetNodeAccessStateRequest,
} from '../types/dashboard'

const dashboardKey = ['dashboard']

export function useDashboardData() {
  const queryClient = useQueryClient()
  const [issuedAccess, setIssuedAccess] = useState<IssuedNodeAccess | undefined>(undefined)

  const dashboardQuery = useQuery({
    queryKey: dashboardKey,
    queryFn: apiClient.getDashboard,
    refetchInterval: 15000,
  })

  const issueNodeAccessMutation = useMutation({
    mutationFn: ({ nodeId, payload }: { nodeId: string; payload: IssueNodeAccessRequest }) =>
      apiClient.issueNodeAccess(nodeId, payload),
    onSuccess: (result) => {
      setIssuedAccess(result)
      void queryClient.invalidateQueries({ queryKey: dashboardKey })
    },
  })

  const setNodeAccessStateMutation = useMutation({
    mutationFn: ({
      nodeId,
      accessId,
      payload,
    }: {
      nodeId: string
      accessId: string
      payload: SetNodeAccessStateRequest
    }) => apiClient.setNodeAccessState(nodeId, accessId, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: dashboardKey })
    },
  })

  const deleteNodeAccessMutation = useMutation({
    mutationFn: ({ nodeId, accessId }: { nodeId: string; accessId: string }) => apiClient.deleteNodeAccess(nodeId, accessId),
    onSuccess: (result: DeletedNodeAccess) => {
      if (issuedAccess?.userId === result.userId && issuedAccess.nodeId === result.nodeId) {
        setIssuedAccess(undefined)
      }

      void queryClient.invalidateQueries({ queryKey: dashboardKey })
    },
  })

  const onRealtimeUpdate = useEffectEvent((message: NodeRealtimeEnvelope) => {
    queryClient.setQueryData<DashboardSnapshot | undefined>(dashboardKey, (current) => {
      if (!current) {
        return current
      }

      const sessions = [
        ...current.sessions.filter((session) => session.nodeId !== message.nodeId),
        ...message.sessions,
      ].sort((left, right) => {
        const leftValue = left.latestHandshakeAtUtc ?? left.connectedAtUtc ?? ''
        const rightValue = right.latestHandshakeAtUtc ?? right.connectedAtUtc ?? ''
        return rightValue.localeCompare(leftValue)
      })

      const nodes = current.nodes.map((node) =>
        node.id === message.nodeId
          ? {
              ...node,
              activeSessions: message.sessions.length,
              lastSeenAtUtc: message.observedAtUtc,
              status: 'Healthy',
              lastError: null,
            }
          : node,
      )

      return {
        ...current,
        nodes,
        sessions,
        accesses: current.accesses.map((access) => {
          if (access.nodeId !== message.nodeId) {
            return access
          }

          const session = message.sessions.find((item) => item.publicKey === access.publicKey)
          if (!session) {
            return access.sessionState === 'Active'
              ? {
                  ...access,
                  endpoint: null,
                  sessionState: 'Disconnected',
                }
              : access
          }

          return {
            ...access,
            endpoint: session.endpoint,
            sessionState: session.state,
            latestHandshakeAtUtc: session.latestHandshakeAtUtc,
            lastActivityAtUtc: session.latestHandshakeAtUtc ?? session.connectedAtUtc ?? message.observedAtUtc,
          }
        }),
      }
    })
  })

  useEffect(() => {
    if (!apiClient.baseUrl || !apiClient.accessToken) {
      return
    }

    const connection = new HubConnectionBuilder()
      .withUrl(`${apiClient.baseUrl}/hubs/sessions`, {
        accessTokenFactory: () => apiClient.accessToken,
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    connection.on('sessionSnapshotUpdated', onRealtimeUpdate)
    void connection.start()

    return () => {
      connection.off('sessionSnapshotUpdated', onRealtimeUpdate)
      void connection.stop()
    }
  }, [onRealtimeUpdate])

  return {
    dashboard: dashboardQuery.data,
    isLoading: dashboardQuery.isLoading,
    isError: dashboardQuery.isError,
    error: dashboardQuery.error,
    refresh: dashboardQuery.refetch,
    issueNodeAccess: (nodeId: string, payload: IssueNodeAccessRequest) =>
      issueNodeAccessMutation.mutateAsync({ nodeId, payload }),
    setNodeAccessState: (nodeId: string, accessId: string, payload: SetNodeAccessStateRequest) =>
      setNodeAccessStateMutation.mutateAsync({ nodeId, accessId, payload }),
    deleteNodeAccess: (nodeId: string, accessId: string) => deleteNodeAccessMutation.mutateAsync({ nodeId, accessId }),
    getNodeAccessConfig: (nodeId: string, accessId: string, format: AccessConfigFormat) =>
      apiClient.getNodeAccessConfig(nodeId, accessId, format) as Promise<AccessConfig>,
    isSavingUser: issueNodeAccessMutation.isPending || setNodeAccessStateMutation.isPending || deleteNodeAccessMutation.isPending,
    issuedAccess,
  }
}
