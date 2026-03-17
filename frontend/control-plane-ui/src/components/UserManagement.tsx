import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import type { AccessConfig, IssueNodeAccessRequest, IssuedNodeAccess, SetNodeAccessStateRequest, UserSummary } from '../types/dashboard'
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
  onDeleteAccess: (nodeId: string, userId: string) => Promise<unknown>
  onDownloadAccessConfig: (nodeId: string, userId: string) => Promise<AccessConfig>
}

const defaultForm: IssueNodeAccessRequest = {
  displayName: '',
}

type BusyAction = 'toggle' | 'delete' | 'download' | null

export function UserManagement({
  users,
  activeUserIds,
  selectedNodeId,
  selectedNodeName,
  issuedAccess,
  isSaving,
  onIssueAccess,
  onSetAccessState,
  onDeleteAccess,
  onDownloadAccessConfig,
}: UserManagementProps) {
  const [form, setForm] = useState(defaultForm)
  const [query, setQuery] = useState('')
  const [busyUserId, setBusyUserId] = useState<string | null>(null)
  const [busyAction, setBusyAction] = useState<BusyAction>(null)
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

    const haystack = [user.displayName, user.externalId].join(' ').toLowerCase()
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
    setBusyAction('toggle')

    try {
      const isEnabledOnNode = user.enabledNodeIds.includes(selectedNodeId)
      await onSetAccessState(selectedNodeId, user.id, { isEnabled: !isEnabledOnNode })
    } finally {
      setBusyUserId(null)
      setBusyAction(null)
    }
  }

  async function handleDeleteUser(user: UserSummary) {
    if (!selectedNodeId) {
      return
    }

    const confirmed = window.confirm(
      `Удалить доступ "${user.displayName}" с ноды "${selectedNodeName ?? selectedNodeId}"? Это удалит peer и конфиг с сервера.`,
    )

    if (!confirmed) {
      return
    }

    setBusyUserId(user.id)
    setBusyAction('delete')

    try {
      await onDeleteAccess(selectedNodeId, user.id)
    } finally {
      setBusyUserId(null)
      setBusyAction(null)
    }
  }

  async function handleDownloadConfig(user: UserSummary) {
    if (!selectedNodeId) {
      return
    }

    setBusyUserId(user.id)
    setBusyAction('download')

    try {
      const config = await onDownloadAccessConfig(selectedNodeId, user.id)
      const blob = new Blob([config.clientConfig], { type: 'text/plain;charset=utf-8' })
      const objectUrl = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = objectUrl
      anchor.download = config.clientConfigFileName
      anchor.click()
      URL.revokeObjectURL(objectUrl)
    } catch (error) {
      window.alert(error instanceof Error ? error.message : 'Не удалось сформировать конфиг для скачивания.')
    } finally {
      setBusyUserId(null)
      setBusyAction(null)
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

  function renderIdentifier(user: UserSummary) {
    if (!user.externalId || user.externalId.startsWith('issued-')) {
      return null
    }

    return <div className="text-muted small">{user.externalId}</div>
  }

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
                    ? 'Реальные peer-доступы выбранной ноды с включением, отключением, удалением, скачиванием и выдачей новых конфигов.'
                    : 'Общий каталог ключей. Для выдачи и управления открой конкретную ноду.'}
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
                placeholder="имя или внешний идентификатор"
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
                      <th>Доступ</th>
                      <th>Статус</th>
                      <th>Ключей</th>
                      <th>Последняя активность</th>
                      <th>Действия</th>
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
                            {renderIdentifier(user)}
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
                              <div className="d-flex flex-wrap gap-2">
                                <button
                                  type="button"
                                  className="btn btn-sm btn-outline-primary"
                                  disabled={isSaving}
                                  onClick={() => void handleDownloadConfig(user)}
                                >
                                  {isBusy && busyAction === 'download' ? 'Подготовка...' : 'Скачать .conf'}
                                </button>
                                <button
                                  type="button"
                                  className={`btn btn-sm ${isEnabledOnNode ? 'btn-light-danger' : 'btn-light-success'}`}
                                  disabled={isSaving}
                                  onClick={() => void handleToggleUser(user)}
                                >
                                  {isBusy && busyAction === 'toggle'
                                    ? 'Сохранение...'
                                    : isEnabledOnNode
                                      ? 'Отключить'
                                      : 'Включить'}
                                </button>
                                <button
                                  type="button"
                                  className="btn btn-sm btn-outline-danger"
                                  disabled={isSaving}
                                  onClick={() => void handleDeleteUser(user)}
                                >
                                  {isBusy && busyAction === 'delete' ? 'Удаление...' : 'Удалить'}
                                </button>
                              </div>
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
