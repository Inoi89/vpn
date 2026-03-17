import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import type { IssueNodeAccessRequest, IssuedNodeAccess, SetNodeAccessStateRequest, UserSummary } from '../types/dashboard'
import { formatDateTime, formatRelativeTime } from '../utils/format'

type UserManagementProps = {
  users: UserSummary[]
  activeUserIds: Set<string>
  selectedNodeId: string | null
  selectedNodeName: string | null
  issuedAccess?: IssuedNodeAccess
  isSaving: boolean
  onIssueAccess: (nodeId: string, payload: IssueNodeAccessRequest) => Promise<unknown>
  onSetAccessState: (nodeId: string, userId: string, payload: SetNodeAccessStateRequest) => Promise<unknown>
}

const defaultForm: IssueNodeAccessRequest = {
  displayName: '',
  email: '',
}

export function UserManagement({
  users,
  activeUserIds,
  selectedNodeId,
  selectedNodeName,
  issuedAccess,
  isSaving,
  onIssueAccess,
  onSetAccessState,
}: UserManagementProps) {
  const [form, setForm] = useState(defaultForm)
  const [query, setQuery] = useState('')
  const [busyUserId, setBusyUserId] = useState<string | null>(null)
  const formRef = useRef<HTMLFormElement | null>(null)
  const normalizedQuery = query.trim().toLowerCase()

  const sortedUsers = [...users].sort((left, right) => {
    const leftActive = activeUserIds.has(left.id) ? 1 : 0
    const rightActive = activeUserIds.has(right.id) ? 1 : 0

    if (leftActive !== rightActive) {
      return rightActive - leftActive
    }

    const leftEnabled = selectedNodeId ? left.enabledNodeIds.includes(selectedNodeId) : left.isEnabled
    const rightEnabled = selectedNodeId ? right.enabledNodeIds.includes(selectedNodeId) : right.isEnabled
    if (leftEnabled !== rightEnabled) {
      return Number(rightEnabled) - Number(leftEnabled)
    }

    const leftActivity = left.lastActivityAtUtc ?? ''
    const rightActivity = right.lastActivityAtUtc ?? ''
    if (leftActivity !== rightActivity) {
      return rightActivity.localeCompare(leftActivity)
    }

    return left.displayName.localeCompare(right.displayName, 'ru-RU')
  })

  const filteredUsers = sortedUsers.filter((user) => {
    if (!normalizedQuery) {
      return true
    }

    const haystack = [user.displayName, user.email ?? '', user.externalId].join(' ').toLowerCase()
    return haystack.includes(normalizedQuery)
  })

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!selectedNodeId) {
      return
    }

    await onIssueAccess(selectedNodeId, form)
    setForm(defaultForm)
  }

  async function handleToggleUser(user: UserSummary) {
    if (!selectedNodeId) {
      return
    }

    setBusyUserId(user.id)

    try {
      const isEnabledOnNode = user.enabledNodeIds.includes(selectedNodeId)
      await onSetAccessState(selectedNodeId, user.id, { isEnabled: !isEnabledOnNode })
    } finally {
      setBusyUserId(null)
    }
  }

  function focusForm() {
    if (!selectedNodeId) {
      return
    }

    formRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    const firstInput = formRef.current?.querySelector('input')
    firstInput?.focus()
  }

  function downloadIssuedConfig() {
    if (!issuedAccess) {
      return
    }

    const blob = new Blob([issuedAccess.clientConfig], { type: 'text/plain;charset=utf-8' })
    const objectUrl = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = objectUrl
    anchor.download = issuedAccess.clientConfigFileName
    anchor.click()
    URL.revokeObjectURL(objectUrl)
  }

  const hasSelectedNode = Boolean(selectedNodeId)
  const visibleIssuedAccess = hasSelectedNode && issuedAccess?.nodeId === selectedNodeId ? issuedAccess : undefined

  return (
    <div className="row">
      <div className="col-12">
        <div className="card">
          <div className="card-header">
            <div className="d-flex flex-wrap justify-content-between align-items-start gap-2">
              <div>
                <h5>{selectedNodeName ? `Ключи ноды ${selectedNodeName}` : 'Каталог доступа'}</h5>
                <small>
                  {selectedNodeName
                    ? 'Реальные peer-доступы выбранной ноды с включением, отключением и выдачей новых конфигов.'
                    : 'Общий каталог ключей. Для выдачи и переключения состояния открой конкретную ноду.'}
                </small>
              </div>
              <button type="button" className="btn btn-primary btn-sm" disabled={!hasSelectedNode} onClick={focusForm}>
                Добавить
              </button>
            </div>
          </div>
          <div className="card-body">
            <div className="form-search m-b-20">
              <input
                type="search"
                className="form-control"
                placeholder="имя, почта или внешний идентификатор"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
              />
            </div>

            {filteredUsers.length === 0 ? (
              <div className="empty-panel">Ничего не найдено по текущему фильтру.</div>
            ) : (
              <div className="table-responsive">
                <table className="table table-hover table-align-center mb-0">
                  <thead>
                    <tr>
                      <th>Пользователь</th>
                      <th>Статус</th>
                      <th>Ключей</th>
                      <th>Последняя активность</th>
                      <th>Действие</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredUsers.map((user) => {
                      const isActive = activeUserIds.has(user.id)
                      const isEnabledOnNode = selectedNodeId ? user.enabledNodeIds.includes(selectedNodeId) : user.isEnabled
                      const isBusy = busyUserId === user.id && isSaving

                      return (
                        <tr key={user.id}>
                          <td>
                            <strong>{user.displayName}</strong>
                            <div className="text-muted small">{user.email ?? user.externalId}</div>
                          </td>
                          <td>
                            <div className="d-flex flex-column gap-1">
                              <span className={`badge ${isActive ? 'badge-light-success' : 'badge-light-secondary'}`}>
                                {isActive ? 'В сети' : 'Не в сети'}
                              </span>
                              <span className={`badge ${isEnabledOnNode ? 'badge-light-primary' : 'badge-light-secondary'}`}>
                                {isEnabledOnNode ? 'Ключ включён' : 'Ключ отключён'}
                              </span>
                            </div>
                          </td>
                          <td>{user.peerCount}</td>
                          <td>
                            <strong>{formatRelativeTime(user.lastActivityAtUtc)}</strong>
                            <div className="text-muted small">{formatDateTime(user.lastActivityAtUtc)}</div>
                          </td>
                          <td>
                            {selectedNodeId ? (
                              <button
                                type="button"
                                className={`btn btn-sm ${isEnabledOnNode ? 'btn-light-danger' : 'btn-light-success'}`}
                                disabled={isSaving}
                                onClick={() => void handleToggleUser(user)}
                              >
                                {isBusy ? 'Сохранение...' : isEnabledOnNode ? 'Отключить' : 'Включить'}
                              </button>
                            ) : (
                              <span className="text-muted small">Выберите ноду</span>
                            )}
                          </td>
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      </div>

      {hasSelectedNode ? (
        <>
          <div className="col-12">
            <div className="card">
              <div className="card-header">
                <h5>{selectedNodeName ? `Выдать доступ на ${selectedNodeName}` : 'Выдать доступ'}</h5>
              </div>
              <div className="card-body">
                <form onSubmit={handleSubmit} ref={formRef}>
                  <div className="form-group">
                    <label>Имя в панели</label>
                    <input
                      className="form-control"
                      required
                      value={form.displayName}
                      onChange={(event) => setForm((current) => ({ ...current, displayName: event.target.value }))}
                    />
                  </div>

                  <div className="form-group">
                    <label>Почта</label>
                    <input
                      className="form-control"
                      type="email"
                      value={form.email ?? ''}
                      onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
                    />
                  </div>

                  <button className="btn btn-primary" disabled={isSaving} type="submit">
                    {isSaving ? 'Выдача...' : 'Выдать ключ'}
                  </button>
                </form>
              </div>
            </div>
          </div>

          {visibleIssuedAccess ? (
            <div className="col-12">
              <div className="card">
                <div className="card-header">
                  <div className="d-flex flex-wrap justify-content-between align-items-start gap-2">
                    <div>
                      <h5>Последний выданный конфиг</h5>
                      <small>
                        {visibleIssuedAccess.displayName} · {visibleIssuedAccess.allowedIps}
                      </small>
                    </div>
                    <button type="button" className="btn btn-primary btn-sm" onClick={downloadIssuedConfig}>
                      Скачать .conf
                    </button>
                  </div>
                </div>
                <div className="card-body">
                  <div className="text-muted small m-b-10">{visibleIssuedAccess.clientConfigFileName}</div>
                  <textarea
                    className="form-control issued-config"
                    readOnly
                    rows={14}
                    value={visibleIssuedAccess.clientConfig}
                  />
                </div>
              </div>
            </div>
          ) : null}
        </>
      ) : null}
    </div>
  )
}
