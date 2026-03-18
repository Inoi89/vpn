import type { NodeSummary } from '../types/dashboard'
import { formatNodeStatus, formatRelativeTime } from '../utils/format'
import { getNodeDisplayName, getNodeOperatorMetadata, sortNodesForDisplay } from '../utils/nodeDisplay'

type NodeListProps = {
  nodes: NodeSummary[]
  selectedNodeId: string | null
  onSelectNode: (nodeId: string | null) => void
}

export function NodeList({ nodes, selectedNodeId, onSelectNode }: NodeListProps) {
  const displayNodes = sortNodesForDisplay(nodes)
  const healthyNodes = nodes.filter((node) => node.status === 'Healthy').length
  const liveSessions = nodes.reduce((total, node) => total + node.activeSessions, 0)

  return (
    <div className="pc-sidebar-content">
      <div className="pc-caption">
        <label>Обзор</label>
        <span>{healthyNodes} нод в норме</span>
      </div>

      <ul className="pc-navbar">
        <li className={`pc-item ${selectedNodeId === null ? 'active' : ''}`}>
          <button type="button" className="pc-link" onClick={() => onSelectNode(null)}>
            <span className="pc-mtext">Общий обзор</span>
            <small>{liveSessions} сессий в контуре</small>
          </button>
        </li>
      </ul>

      <div className="pc-caption">
        <label>Ноды</label>
        <span>{nodes.length} подключено</span>
      </div>

      <ul className="pc-navbar">
        {nodes.length === 0 ? (
          <li className="pc-item">
            <div className="pc-link empty-nav-item">Пока нет ни одной зарегистрированной ноды.</div>
          </li>
        ) : (
          displayNodes.map((node) => {
            const operatorMetadata = getNodeOperatorMetadata(node)

            return (
              <li className={`pc-item ${selectedNodeId === node.id ? 'active' : ''}`} key={node.id}>
                <button type="button" className="pc-link" onClick={() => onSelectNode(node.id)}>
                  <div className="d-flex align-items-center justify-content-between gap-2">
                    <span className="pc-mtext">{getNodeDisplayName(node)}</span>
                    <div className="node-link-badges">
                      {operatorMetadata ? (
                        <span className="badge badge-light-secondary node-flag-badge" title={operatorMetadata.country}>
                          <img
                            src={operatorMetadata.flagIcon}
                            alt={operatorMetadata.country}
                            className="node-flag-icon"
                          />
                        </span>
                      ) : null}
                    </div>
                  </div>
                  <small>
                    {formatNodeStatus(node.status)} · {node.activeSessions} сессий
                  </small>
                  {operatorMetadata ? <small>{operatorMetadata.provider} · {operatorMetadata.country}</small> : null}
                  <small>{node.enabledPeerCount} ключей включено</small>
                  <small>{formatRelativeTime(node.lastSeenAtUtc)}</small>
                </button>
              </li>
            )
          })
        )}
      </ul>
    </div>
  )
}
