import type { SessionSummary } from '../types/dashboard'

type SessionsPanelProps = {
  sessions: SessionSummary[]
}

function formatBytes(bytes: number) {
  if (bytes <= 0) {
    return '0 B'
  }

  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  const index = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1)
  const value = bytes / 1024 ** index
  return `${value.toFixed(value >= 100 ? 0 : 1)} ${units[index]}`
}

export function SessionsPanel({ sessions }: SessionsPanelProps) {
  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <p className="eyebrow">Sessions</p>
          <h2>Active tunnels</h2>
        </div>
        <p className="panel-meta">{sessions.length} live</p>
      </div>
      <div className="session-table">
        <div className="session-table-header">
          <span>User</span>
          <span>Node</span>
          <span>Endpoint</span>
          <span>RX</span>
          <span>TX</span>
          <span>Handshake</span>
        </div>
        {sessions.map((session) => (
          <div className="session-row" key={session.id}>
            <div>
              <strong>{session.userDisplayName}</strong>
              <p>{session.publicKey.slice(0, 16)}...</p>
            </div>
            <span>{session.nodeName}</span>
            <span>{session.endpoint ?? 'n/a'}</span>
            <span>{formatBytes(session.rxBytes)}</span>
            <span>{formatBytes(session.txBytes)}</span>
            <span>{session.latestHandshakeAtUtc ? new Date(session.latestHandshakeAtUtc).toLocaleTimeString() : 'none'}</span>
          </div>
        ))}
      </div>
    </section>
  )
}
