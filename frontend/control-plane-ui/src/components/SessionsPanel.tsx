import { useState } from 'react'
import type { SessionSummary } from '../types/dashboard'
import { formatBytes, formatRelativeTime, formatSessionState, formatTime, shortKey } from '../utils/format'

type SessionsPanelProps = {
  sessions: SessionSummary[]
  selectedNodeName: string | null
}

export function SessionsPanel({ sessions, selectedNodeName }: SessionsPanelProps) {
  const [query, setQuery] = useState('')

  const normalizedQuery = query.trim().toLowerCase()
  const filteredSessions = sessions.filter((session) => {
    if (!normalizedQuery) {
      return true
    }

    const haystack = [session.userDisplayName, session.nodeName, session.endpoint ?? '', session.publicKey]
      .join(' ')
      .toLowerCase()

    return haystack.includes(normalizedQuery)
  })

  return (
    <div className="card">
      <div className="card-header">
        <div className="d-flex flex-wrap justify-content-between align-items-start gap-2">
          <div>
            <h5>{selectedNodeName ? `Активные туннели ноды ${selectedNodeName}` : 'Активные туннели'}</h5>
            <small>Текущие подключения пользователей, endpoint клиентов и счётчики трафика.</small>
          </div>
          <div className="form-search">
            <input
              type="search"
              className="form-control"
              placeholder="пользователь, адрес клиента, ключ"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
            />
          </div>
        </div>
      </div>
      <div className="card-body p-0">
        {filteredSessions.length === 0 ? (
          <div className="empty-panel">Нет активных сессий для текущего фильтра.</div>
        ) : (
          <div className="table-responsive">
            <table className="table table-hover table-align-center mb-0">
              <thead>
                <tr>
                  <th>Пользователь</th>
                  <th>Нода</th>
                  <th>Адрес клиента</th>
                  <th>Последний обмен</th>
                  <th>Входящий</th>
                  <th>Исходящий</th>
                </tr>
              </thead>
              <tbody>
                {filteredSessions.map((session) => (
                  <tr key={session.id}>
                    <td>
                      <strong>{session.userDisplayName}</strong>
                      <div className="text-muted small">{shortKey(session.publicKey, 8)}</div>
                    </td>
                    <td>
                      <strong>{session.nodeName}</strong>
                      <div className="text-muted small">{formatSessionState(session.state)}</div>
                    </td>
                    <td>
                      <strong>{session.endpoint ?? 'нет'}</strong>
                      <div className="text-muted small">Подключена {formatRelativeTime(session.connectedAtUtc)}</div>
                    </td>
                    <td>
                      <strong>{formatTime(session.latestHandshakeAtUtc)}</strong>
                      <div className="text-muted small">{formatRelativeTime(session.latestHandshakeAtUtc)}</div>
                    </td>
                    <td>{formatBytes(session.rxBytes)}</td>
                    <td>{formatBytes(session.txBytes)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  )
}
