import type { NodeSummary } from '../types/dashboard'

type NodeListProps = {
  nodes: NodeSummary[]
}

export function NodeList({ nodes }: NodeListProps) {
  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <p className="eyebrow">Nodes</p>
          <h2>Fleet topology</h2>
        </div>
        <p className="panel-meta">{nodes.length} total</p>
      </div>
      <div className="node-grid">
        {nodes.map((node) => (
          <article className="node-card" key={node.id}>
            <div className="node-card-header">
              <div>
                <h3>{node.name}</h3>
                <p>{node.cluster}</p>
              </div>
              <span className={`status status-${node.status.toLowerCase()}`}>{node.status}</span>
            </div>
            <dl className="kv-list">
              <div>
                <dt>Endpoint</dt>
                <dd>{node.agentBaseAddress}</dd>
              </div>
              <div>
                <dt>Sessions</dt>
                <dd>{node.activeSessions}</dd>
              </div>
              <div>
                <dt>Agent</dt>
                <dd>{node.agentVersion ?? 'unknown'}</dd>
              </div>
              <div>
                <dt>Last seen</dt>
                <dd>{node.lastSeenAtUtc ? new Date(node.lastSeenAtUtc).toLocaleString() : 'never'}</dd>
              </div>
            </dl>
            {node.lastError ? <p className="node-error">{node.lastError}</p> : null}
          </article>
        ))}
      </div>
    </section>
  )
}
