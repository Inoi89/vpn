import { useRef, useState } from 'react'
import type { FormEvent } from 'react'
import type { AccessConfig, AccessConfigFormat, AccessSummary, IssueNodeAccessRequest, IssuedNodeAccess, SetNodeAccessStateRequest } from '../types/dashboard'
import { formatDateTime, formatRelativeTime } from '../utils/format'

type UserManagementProps = {
  accesses: AccessSummary[]
  selectedNodeId: string | null
  selectedNodeName: string | null
  issuedAccess?: IssuedNodeAccess
  isSaving: boolean
  onIssueAccess: (nodeId: string, payload: IssueNodeAccessRequest) => Promise<unknown>
  onSetAccessState: (nodeId: string, accessId: string, payload: SetNodeAccessStateRequest) => Promise<unknown>
  onDeleteAccess: (nodeId: string, accessId: string) => Promise<unknown>
  onDownloadAccessConfig: (nodeId: string, accessId: string, format: AccessConfigFormat) => Promise<AccessConfig>
}

const defaultConfigFormat: AccessConfigFormat = 'amnezia-vpn'

const defaultForm: IssueNodeAccessRequest = {
  displayName: '',
  configFormat: defaultConfigFormat,
}

type BusyAction = 'toggle' | 'delete' | 'download' | null

export function UserManagement({
  accesses,
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
  const [busyAccessId, setBusyAccessId] = useState<string | null>(null)
  const [busyAction, setBusyAction] = useState<BusyAction>(null)
  const [exportFormat, setExportFormat] = useState<AccessConfigFormat>(defaultConfigFormat)
  const formRef = useRef<HTMLFormElement | null>(null)
  const normalizedQuery = query.trim().toLowerCase()

  const sortedAccesses = [...accesses].sort((left, right) => {
    const leftActive = left.sessionState === 'Active' ? 1 : 0
    const rightActive = right.sessionState === 'Active' ? 1 : 0

    if (leftActive !== rightActive) {
      return rightActive - leftActive
    }

    if (left.isEnabled !== right.isEnabled) {
      return Number(right.isEnabled) - Number(left.isEnabled)
    }

    const leftActivity = left.lastActivityAtUtc ?? ''
    const rightActivity = right.lastActivityAtUtc ?? ''
    if (leftActivity !== rightActivity) {
      return rightActivity.localeCompare(leftActivity)
    }

    return left.displayName.localeCompare(right.displayName, 'ru-RU')
  })

  const filteredAccesses = sortedAccesses.filter((access) => {
    if (!normalizedQuery) {
      return true
    }

    const haystack = [
      access.displayName,
      access.externalId,
      access.email ?? '',
      access.allowedIps,
      access.publicKey,
      access.nodeName,
      access.accountDisplayName ?? '',
      access.accountEmail ?? '',
      access.deviceName ?? '',
      access.devicePlatform ?? '',
      access.deviceFingerprint ?? '',
    ]
      .join(' ')
      .toLowerCase()

    return haystack.includes(normalizedQuery)
  })

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    if (!selectedNodeId) {
      return
    }

    await onIssueAccess(selectedNodeId, form)
    setForm({ displayName: '', configFormat: exportFormat })
  }

  async function handleToggleAccess(access: AccessSummary) {
    setBusyAccessId(access.id)
    setBusyAction('toggle')

    try {
      await onSetAccessState(access.nodeId, access.id, { isEnabled: !access.isEnabled })
    } finally {
      setBusyAccessId(null)
      setBusyAction(null)
    }
  }

  async function handleDeleteAccess(access: AccessSummary) {
    const confirmed = window.confirm(
      `Удалить доступ "${access.displayName}" с ноды "${access.nodeName}"? Это удалит peer и конфиг с сервера.`,
    )

    if (!confirmed) {
      return
    }

    setBusyAccessId(access.id)
    setBusyAction('delete')

    try {
      await onDeleteAccess(access.nodeId, access.id)
    } finally {
      setBusyAccessId(null)
      setBusyAction(null)
    }
  }

  async function handleDownloadConfig(access: AccessSummary) {
    setBusyAccessId(access.id)
    setBusyAction('download')

    try {
      const config = await onDownloadAccessConfig(access.nodeId, access.id, exportFormat)
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
      setBusyAccessId(null)
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

  function renderExternalId(access: AccessSummary) {
    if (!access.externalId || access.externalId.startsWith('issued-')) {
      return null
    }

    return <div className="text-muted small">{access.externalId}</div>
  }

  function renderOwner(access: AccessSummary) {
    const ownerName = access.accountDisplayName ?? null
    const ownerEmail = access.accountEmail ?? access.email ?? null
    const deviceLine = [access.deviceName, access.devicePlatform].filter(Boolean).join(' · ')
    const versionLine = [access.clientVersion, access.deviceFingerprint].filter(Boolean).join(' · ')

    return (
      <div className="d-flex flex-column gap-1">
        <strong>{ownerName ?? ownerEmail ?? 'Локальный доступ'}</strong>
        {ownerEmail && ownerEmail !== ownerName ? <div className="text-muted small">{ownerEmail}</div> : null}
        {deviceLine ? <div className="text-muted small">{deviceLine}</div> : null}
        {versionLine ? <div className="text-muted small">{versionLine}</div> : null}
      </div>
    )
  }

  function renderAccessName(access: AccessSummary) {
    return (
      <div className="d-flex flex-column gap-1">
        <strong>{access.displayName}</strong>
        {renderExternalId(access)}
        <div className="text-muted small font-monospace">{access.publicKey}</div>
      </div>
    )
  }

  const hasSelectedNode = Boolean(selectedNodeId)
  const visibleIssuedAccess = hasSelectedNode && issuedAccess?.nodeId === selectedNodeId ? issuedAccess : undefined
  const downloadLabel = exportFormat === 'amnezia-vpn' ? 'Скачать .vpn' : 'Скачать .conf'
  const exportFormatHint =
    exportFormat === 'amnezia-vpn'
      ? 'Базовый формат Amnezia (.vpn)'
      : 'Оригинальный конфиг AmneziaWG (.conf)'

  return (
    <div className="row">
      <div className="col-12">
        <div className="card">
          <div className="card-header">
            <div className="d-flex flex-wrap justify-content-between align-items-start gap-2">
              <div>
                <h5>{selectedNodeName ? `Доступы ноды ${selectedNodeName}` : 'Каталог доступов'}</h5>
                <small>
                  {selectedNodeName
                    ? 'Peer-доступы выбранной ноды с выданным IP, ключом, владельцем, устройством и управлением состоянием.'
                    : 'Общий каталог peer-доступов по всему контуру. Для выдачи нового доступа откройте конкретную ноду.'}
                </small>
              </div>
              <div className="d-flex flex-wrap align-items-end gap-2">
                <div>
                  <label className="form-label mb-1">Формат выдачи</label>
                  <select
                    className="form-select form-select-sm"
                    value={exportFormat}
                    onChange={(event) => {
                      const nextFormat = event.target.value as AccessConfigFormat
                      setExportFormat(nextFormat)
                      setForm((current) => ({ ...current, configFormat: nextFormat }))
                    }}
                  >
                    <option value="amnezia-vpn">Amnezia (.vpn)</option>
                    <option value="amnezia-awg-native">AmneziaWG (.conf)</option>
                  </select>
                  <small className="text-muted">{exportFormatHint}</small>
                </div>
                <button type="button" className="btn btn-primary btn-sm" disabled={!hasSelectedNode} onClick={focusForm}>
                  Добавить
                </button>
              </div>
            </div>
          </div>
          <div className="card-body">
            <div className="form-search m-b-20">
              <input
                type="search"
                className="form-control"
                placeholder="имя, email, внешний идентификатор, IP, ключ или устройство"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
              />
            </div>

            {filteredAccesses.length === 0 ? (
              <div className="empty-panel">Ничего не найдено по текущему фильтру.</div>
            ) : (
              <div className="table-responsive">
                <table className="table table-hover table-align-center mb-0">
                  <thead>
                    <tr>
                      <th>Доступ</th>
                      {!selectedNodeId ? <th>Нода</th> : null}
                      <th>Владелец / устройство</th>
                      <th>Выданный IP</th>
                      <th>Статус</th>
                      <th>Последняя активность</th>
                      <th>Действия</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredAccesses.map((access) => {
                      const isActive = access.sessionState === 'Active'
                      const isBusy = busyAccessId === access.id && isSaving

                      return (
                        <tr key={access.id}>
                          <td>{renderAccessName(access)}</td>
                          {!selectedNodeId ? <td>{access.nodeName}</td> : null}
                          <td>{renderOwner(access)}</td>
                          <td>
                            <strong className="font-monospace">{access.allowedIps}</strong>
                            <div className="text-muted small">{access.protocol}</div>
                          </td>
                          <td>
                            <div className="d-flex flex-column gap-1">
                              <span className={`badge ${isActive ? 'badge-light-success' : 'badge-light-secondary'}`}>
                                {isActive ? 'В сети' : 'Не в сети'}
                              </span>
                              <span className={`badge ${access.isEnabled ? 'badge-light-primary' : 'badge-light-secondary'}`}>
                                {access.isEnabled ? 'Ключ включён' : 'Ключ отключён'}
                              </span>
                            </div>
                          </td>
                          <td>
                            <strong>{formatRelativeTime(access.lastActivityAtUtc)}</strong>
                            <div className="text-muted small">{formatDateTime(access.lastActivityAtUtc)}</div>
                            {access.endpoint ? <div className="text-muted small font-monospace">{access.endpoint}</div> : null}
                          </td>
                          <td>
                            <div className="d-flex flex-wrap gap-2">
                              <button
                                type="button"
                                className="btn btn-sm btn-outline-primary"
                                disabled={isSaving}
                                onClick={() => void handleDownloadConfig(access)}
                              >
                                {isBusy && busyAction === 'download' ? 'Подготовка...' : downloadLabel}
                              </button>
                              <button
                                type="button"
                                className={`btn btn-sm ${access.isEnabled ? 'btn-light-danger' : 'btn-light-success'}`}
                                disabled={isSaving}
                                onClick={() => void handleToggleAccess(access)}
                              >
                                {isBusy && busyAction === 'toggle'
                                  ? 'Сохранение...'
                                  : access.isEnabled
                                    ? 'Отключить'
                                    : 'Включить'}
                              </button>
                              <button
                                type="button"
                                className="btn btn-sm btn-outline-danger"
                                disabled={isSaving}
                                onClick={() => void handleDeleteAccess(access)}
                              >
                                {isBusy && busyAction === 'delete' ? 'Удаление...' : 'Удалить'}
                              </button>
                            </div>
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
                  <div className="row g-3 align-items-end">
                    <div className="col-md-6">
                      <label className="form-label">Имя доступа</label>
                      <input
                        type="text"
                        className="form-control"
                        value={form.displayName}
                        onChange={(event) => setForm((current) => ({ ...current, displayName: event.target.value }))}
                        placeholder="Например: Alex iPhone"
                        required
                      />
                    </div>
                    <div className="col-md-3">
                      <label className="form-label">Формат</label>
                      <select
                        className="form-select"
                        value={form.configFormat}
                        onChange={(event) => setForm((current) => ({ ...current, configFormat: event.target.value as AccessConfigFormat }))}
                      >
                        <option value="amnezia-vpn">Amnezia (.vpn)</option>
                        <option value="amnezia-awg-native">AmneziaWG (.conf)</option>
                      </select>
                    </div>
                    <div className="col-md-3 d-grid">
                      <button type="submit" className="btn btn-primary" disabled={isSaving || !form.displayName.trim()}>
                        {isSaving ? 'Выдача...' : 'Выдать доступ'}
                      </button>
                    </div>
                  </div>
                </form>

                {visibleIssuedAccess ? (
                  <div className="alert alert-success m-t-20 m-b-0">
                    <div className="d-flex flex-wrap justify-content-between align-items-start gap-3">
                      <div>
                        <strong>Конфиг готов: {visibleIssuedAccess.displayName}</strong>
                        <div className="small text-muted m-t-5">
                          Public key: <span className="font-monospace">{visibleIssuedAccess.publicKey}</span>
                        </div>
                        <div className="small text-muted">Выданный IP: {visibleIssuedAccess.allowedIps}</div>
                      </div>
                      <button type="button" className="btn btn-sm btn-success" onClick={downloadIssuedConfig}>
                        Скачать сейчас
                      </button>
                    </div>
                  </div>
                ) : null}
              </div>
            </div>
          </div>
        </>
      ) : null}
    </div>
  )
}
