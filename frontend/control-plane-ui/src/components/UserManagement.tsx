import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import type { UpsertUserRequest, UserSummary } from '../types/dashboard'
import { formatDateTime, formatRelativeTime } from '../utils/format'

type UserManagementProps = {
  users: UserSummary[]
  activeUserIds: Set<string>
  selectedNodeName: string | null
  isSaving: boolean
  onSave: (request: UpsertUserRequest) => Promise<unknown>
}

const defaultForm: UpsertUserRequest = {
  externalId: '',
  displayName: '',
  email: '',
  isEnabled: true,
}

export function UserManagement({ users, activeUserIds, selectedNodeName, isSaving, onSave }: UserManagementProps) {
  const [form, setForm] = useState(defaultForm)
  const [query, setQuery] = useState('')
  const [busyExternalId, setBusyExternalId] = useState<string | null>(null)
  const formRef = useRef<HTMLFormElement | null>(null)

  const normalizedQuery = query.trim().toLowerCase()
  const sortedUsers = [...users].sort((left, right) => {
    const leftActive = activeUserIds.has(left.id) ? 1 : 0
    const rightActive = activeUserIds.has(right.id) ? 1 : 0

    if (leftActive !== rightActive) {
      return rightActive - leftActive
    }

    const leftActivity = left.lastActivityAtUtc ?? ''
    const rightActivity = right.lastActivityAtUtc ?? ''
    if (leftActivity !== rightActivity) {
      return rightActivity.localeCompare(leftActivity)
    }

    if (left.isEnabled !== right.isEnabled) {
      return Number(right.isEnabled) - Number(left.isEnabled)
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
    await onSave(form)
    setForm(defaultForm)
  }

  async function handleToggleUser(user: UserSummary) {
    setBusyExternalId(user.externalId)

    try {
      await onSave({
        externalId: user.externalId,
        displayName: user.displayName,
        email: user.email ?? '',
        isEnabled: !user.isEnabled,
      })
    } finally {
      setBusyExternalId(null)
    }
  }

  function focusForm() {
    formRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' })
    const firstInput = formRef.current?.querySelector('input')
    firstInput?.focus()
  }

  return (
    <div className="row">
      <div className="col-12">
        <div className="card">
          <div className="card-header">
            <div className="d-flex flex-wrap justify-content-between align-items-start gap-2">
              <div>
                <h5>{selectedNodeName ? `Пользователи ноды ${selectedNodeName}` : 'Каталог доступа'}</h5>
                <small>
                  {selectedNodeName
                    ? 'Список ключей и доступов, привязанных к выбранной ноде.'
                    : 'Общий каталог ключей и быстрые действия включения и отключения доступа.'}
                </small>
              </div>
              <button type="button" className="btn btn-primary btn-sm" onClick={focusForm}>
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
                      const isBusy = busyExternalId === user.externalId && isSaving

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
                              <span className={`badge ${user.isEnabled ? 'badge-light-primary' : 'badge-light-secondary'}`}>
                                {user.isEnabled ? 'Доступ открыт' : 'Доступ отключён'}
                              </span>
                            </div>
                          </td>
                          <td>{user.peerCount}</td>
                          <td>
                            <strong>{formatRelativeTime(user.lastActivityAtUtc)}</strong>
                            <div className="text-muted small">{formatDateTime(user.lastActivityAtUtc)}</div>
                          </td>
                          <td>
                            <button
                              type="button"
                              className={`btn btn-sm ${user.isEnabled ? 'btn-light-danger' : 'btn-light-success'}`}
                              disabled={isSaving}
                              onClick={() => void handleToggleUser(user)}
                            >
                              {isBusy ? 'Сохранение...' : user.isEnabled ? 'Отключить' : 'Включить'}
                            </button>
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

      <div className="col-12">
        <div className="card">
          <div className="card-header">
            <h5>Добавить пользователя</h5>
          </div>
          <div className="card-body">
            <form onSubmit={handleSubmit} ref={formRef}>
              <div className="form-group">
                <label>Внешний идентификатор</label>
                <input
                  className="form-control"
                  required
                  value={form.externalId}
                  onChange={(event) => setForm((current) => ({ ...current, externalId: event.target.value }))}
                />
              </div>

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

              <div className="form-check m-b-20">
                <input
                  className="form-check-input"
                  id="enable-user"
                  type="checkbox"
                  checked={form.isEnabled}
                  onChange={(event) => setForm((current) => ({ ...current, isEnabled: event.target.checked }))}
                />
                <label className="form-check-label" htmlFor="enable-user">
                  Сразу включить доступ
                </label>
              </div>

              <button className="btn btn-primary" disabled={isSaving} type="submit">
                {isSaving ? 'Сохранение...' : 'Сохранить пользователя'}
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  )
}
