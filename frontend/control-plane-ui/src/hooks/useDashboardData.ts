import { useEffect, useEffectEvent } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { apiClient } from '../api/client'
import type { DashboardSnapshot, NodeRealtimeEnvelope, UpsertUserRequest } from '../types/dashboard'

const dashboardKey = ['dashboard']

export function useDashboardData() {
  const queryClient = useQueryClient()

  const dashboardQuery = useQuery({
    queryKey: dashboardKey,
    queryFn: apiClient.getDashboard,
    refetchInterval: 15000,
  })

  const upsertUserMutation = useMutation({
    mutationFn: (payload: UpsertUserRequest) => apiClient.upsertUser(payload),
    onSuccess: () => {
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
    upsertUser: upsertUserMutation.mutateAsync,
    isSavingUser: upsertUserMutation.isPending,
  }
}
