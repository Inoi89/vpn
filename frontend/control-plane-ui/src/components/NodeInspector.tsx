import type { NodeSummary, SessionSummary, UserSummary } from '../types/dashboard'
import { formatDateTime, formatNodeStatus, formatRelativeTime } from '../utils/format'
import { getNodeBadgeLabel, getNodeDisplayName, isPrimaryNode } from '../utils/nodeDisplay'

type NodeInspectorProps = {
  node: NodeSummary | null
  nodes: NodeSummary[]
  sessions: SessionSummary[]
  users: UserSummary[]
}

export function NodeInspector({ node, nodes, sessions, users }: NodeInspectorProps) {
  const liveUsers = new Set(sessions.map((session) => session.userId)).size
  const healthyNodes = nodes.filter((item) => item.status === 'Healthy').length
  const activeNodes = nodes.filter((item) => item.activeSessions > 0).length
  const latestSeen =
    nodes
      .map((item) => item.lastSeenAtUtc)
      .filter((value): value is string => Boolean(value))
      .sort((left, right) => right.localeCompare(left))[0] ?? null

  const metrics = node
    ? [
        { title: 'Сессии сейчас', value: String(sessions.length), note: `${liveUsers} пользователей онлайн` },
        { title: 'Последний опрос', value: formatRelativeTime(node.lastSeenAtUtc), note: formatDateTime(node.lastSeenAtUtc) },
        { title: 'Версия агента', value: node.agentVersion ?? 'н/д', note: 'Агент ноды' },
        { title: 'Доступов включено', value: String(users.filter((user) => user.isEnabled).length), note: 'Каталог активных пользователей' },
      ]
    : [
        { title: 'Нод подключено', value: String(nodes.length), note: `${healthyNodes} из них в норме` },
        { title: 'Сессии сейчас', value: String(sessions.length), note: `${liveUsers} пользователей онлайн` },
        { title: 'Пользователи', value: String(users.length), note: `${users.filter((user) => user.isEnabled).length} доступов включено` },
        { title: 'Нод с активностью', value: String(activeNodes), note: `${nodes.length - activeNodes} без туннелей` },
      ]

  const details = node
    ? [
        { title: 'Имя', value: getNodeDisplayName(node) },
        { title: 'Статус', value: formatNodeStatus(node.status) },
        { title: 'Кластер', value: node.cluster },
        { title: 'Адрес агента', value: node.agentBaseAddress },
        { title: 'Идентификатор', value: node.agentIdentifier },
      ]
    : [
        { title: 'Режим', value: 'Весь контур' },
        { title: 'Нод без связи', value: String(nodes.filter((item) => item.status === 'Unreachable').length) },
        { title: 'Нод без сессий', value: String(nodes.filter((item) => item.activeSessions === 0).length) },
        { title: 'Последняя телеметрия', value: formatDateTime(latestSeen) },
      ]

  return (
    <div className="card">
      <div className="card-header">
        <div className="d-flex align-items-center gap-2 flex-wrap">
          <h5 className="mb-0">{node ? `Нода ${getNodeDisplayName(node)}` : 'Состояние контура'}</h5>
          {node && isPrimaryNode(node) ? <span className="badge badge-light-primary">{getNodeBadgeLabel(node)}</span> : null}
        </div>
      </div>
      <div className="card-body">
        <div className="row">
          {metrics.map((metric) => (
            <div className="col-sm-6 col-xl-3" key={metric.title}>
              <div className="insight-box">
                <span>{metric.title}</span>
                <h6>{metric.value}</h6>
                <p>{metric.note}</p>
              </div>
            </div>
          ))}
        </div>

        <div className="row m-t-20">
          {details.map((detail) => (
            <div className="col-sm-6 col-xl-3" key={detail.title}>
              <div className="detail-box">
                <span>{detail.title}</span>
                <h6>{detail.value}</h6>
              </div>
            </div>
          ))}
        </div>

        {node?.lastError ? <div className="alert alert-danger m-b-0 m-t-20">{node.lastError}</div> : null}
      </div>
    </div>
  )
}
