import { NodeList } from './components/NodeList'
import { SessionsPanel } from './components/SessionsPanel'
import { TrafficChart } from './components/TrafficChart'
import { UserManagement } from './components/UserManagement'
import { useDashboardData } from './hooks/useDashboardData'

function App() {
  const { dashboard, isLoading, isError, error, refresh, upsertUser, isSavingUser } = useDashboardData()

  if (isLoading) {
    return <div className="shell-state">Loading control plane...</div>
  }

  if (isError || !dashboard) {
    return <div className="shell-state">Unable to load dashboard: {String(error)}</div>
  }

  return (
    <div className="app-shell">
      <header className="hero">
        <div>
          <p className="eyebrow">VPN Control Plane</p>
          <h1>Operational visibility for WireGuard and Amnezia nodes.</h1>
          <p className="hero-copy">
            Poll agents over mTLS, aggregate peer telemetry into PostgreSQL, and fan out session changes to the UI in
            real time.
          </p>
        </div>
        <button className="hero-action" onClick={() => void refresh()}>
          Refresh now
        </button>
      </header>

      <main className="dashboard-layout">
        <NodeList nodes={dashboard.nodes} />
        <SessionsPanel sessions={dashboard.sessions} />
        <TrafficChart traffic={dashboard.traffic} />
        <UserManagement users={dashboard.users} isSaving={isSavingUser} onSave={upsertUser} />
      </main>
    </div>
  )
}

export default App
