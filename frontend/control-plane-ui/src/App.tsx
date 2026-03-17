import { useEffect, useState } from 'react'
import { NodeInspector } from './components/NodeInspector'
import { NodeList } from './components/NodeList'
import { SessionsPanel } from './components/SessionsPanel'
import { TrafficChart } from './components/TrafficChart'
import { UserManagement } from './components/UserManagement'
import { useDashboardData } from './hooks/useDashboardData'
import { formatDateTime, formatNodeStatus, formatRelativeTime } from './utils/format'

function App() {
  const { dashboard, isLoading, isError, error, refresh, issueNodeAccess, setNodeAccessState, isSavingUser, issuedAccess } =
    useDashboardData()
  const [selectedNodeId, setSelectedNodeId] = useState<string | null | undefined>(undefined)

  useEffect(() => {
    if (!dashboard?.nodes.length) {
      setSelectedNodeId(undefined)
      return
    }

    setSelectedNodeId((current) => {
      if (current === undefined) {
        return null
      }

      if (current === null) {
        return current
      }

      return dashboard.nodes.some((node) => node.id === current) ? current : null
    })
  }, [dashboard?.nodes])

  if (isLoading) {
    return <div className="shell-state">Загрузка панели управления...</div>
  }

  if (isError || !dashboard) {
    return <div className="shell-state">Не удалось загрузить данные панели: {String(error)}</div>
  }

  const selectedNode = selectedNodeId ? dashboard.nodes.find((node) => node.id === selectedNodeId) ?? null : null
  const scopedSessions = selectedNode
    ? dashboard.sessions.filter((session) => session.nodeId === selectedNode.id)
    : dashboard.sessions
  const scopedUsers = selectedNode ? dashboard.users.filter((user) => user.nodeIds.includes(selectedNode.id)) : dashboard.users
  const activeUserIds = new Set(scopedSessions.map((session) => session.userId))
  const healthyNodes = dashboard.nodes.filter((node) => node.status === 'Healthy').length
  const lastTelemetry = selectedNode
    ? selectedNode.lastSeenAtUtc
    : dashboard.nodes
        .map((node) => node.lastSeenAtUtc)
        .filter((value): value is string => Boolean(value))
        .sort((left, right) => right.localeCompare(left))[0] ?? null

  const pageTitle = selectedNode ? selectedNode.name : 'Общий обзор VPN'
  const breadcrumbItems = selectedNode ? ['Контур', 'Ноды', selectedNode.name] : ['Контур', 'Общий обзор']

  return (
    <>
      <nav className="pc-sidebar dark-sidebar">
        <div className="m-header">
          <div className="b-brand">
            <span className="logo logo-lg">VPN Control</span>
          </div>
        </div>

        <div className="navbar-content">
          <NodeList nodes={dashboard.nodes} selectedNodeId={selectedNodeId ?? null} onSelectNode={setSelectedNodeId} />
        </div>
      </nav>

      <header className="pc-header">
        <div className="header-wrapper">
          <div className="me-auto d-flex align-items-center">
            <div className="pc-h-item">
              <span className="pc-head-link active">Контур</span>
            </div>
          </div>

          <div className="ms-auto d-flex align-items-center gap-2 flex-wrap">
            <div className="pc-h-item">
              <span className="pc-head-link user-name">
                <span>
                  <span className="user-name">Статус</span>
                  <span className="user-desc">
                    {selectedNode ? formatNodeStatus(selectedNode.status) : `${healthyNodes}/${dashboard.nodes.length}`}
                  </span>
                </span>
              </span>
            </div>
            <div className="pc-h-item">
              <span className="pc-head-link user-name">
                <span>
                  <span className="user-name">Последний опрос</span>
                  <span className="user-desc">{formatRelativeTime(lastTelemetry)}</span>
                </span>
              </span>
            </div>
            <div className="pc-h-item">
              <button className="btn btn-primary" onClick={() => void refresh()}>
                Обновить сейчас
              </button>
            </div>
          </div>
        </div>
      </header>

      <div className="page-header">
        <div className="page-block">
          <div className="row align-items-center">
            <div className="col-md-8">
              <div className="page-header-title">
                <h5 className="m-b-10">{pageTitle}</h5>
              </div>
              <ul className="breadcrumb">
                {breadcrumbItems.map((item) => (
                  <li className="breadcrumb-item" key={item}>
                    <span>{item}</span>
                  </li>
                ))}
              </ul>
            </div>
            <div className="col-md-4 text-md-end">
              <div className="page-inline-stats">
                <span>Телеметрия: {formatDateTime(lastTelemetry)}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div className="pc-container">
        <div className="pcoded-content">
          <div className="row">
            {selectedNode ? (
              <>
                <div className="col-md-12">
                  <NodeInspector node={selectedNode} nodes={dashboard.nodes} sessions={scopedSessions} users={scopedUsers} />
                </div>
                <div className="col-md-12">
                  <UserManagement
                    users={scopedUsers}
                    activeUserIds={activeUserIds}
                    selectedNodeId={selectedNode.id}
                    selectedNodeName={selectedNode.name}
                    isSaving={isSavingUser}
                    issuedAccess={issuedAccess}
                    onIssueAccess={issueNodeAccess}
                    onSetAccessState={setNodeAccessState}
                  />
                </div>
                <div className="col-md-12">
                  <SessionsPanel sessions={scopedSessions} selectedNodeName={selectedNode.name} />
                </div>
              </>
            ) : (
              <>
                <div className="col-md-12">
                  <NodeInspector node={null} nodes={dashboard.nodes} sessions={scopedSessions} users={scopedUsers} />
                </div>
                <div className="col-md-12">
                  <TrafficChart traffic={dashboard.traffic} />
                </div>
                <div className="col-md-12">
                  <SessionsPanel sessions={scopedSessions} selectedNodeName={null} />
                </div>
                <div className="col-md-12">
                  <UserManagement
                    users={scopedUsers}
                    activeUserIds={activeUserIds}
                    selectedNodeId={null}
                    selectedNodeName={null}
                    isSaving={isSavingUser}
                    issuedAccess={issuedAccess}
                    onIssueAccess={issueNodeAccess}
                    onSetAccessState={setNodeAccessState}
                  />
                </div>
              </>
            )}
          </div>
        </div>
      </div>
    </>
  )
}

export default App
