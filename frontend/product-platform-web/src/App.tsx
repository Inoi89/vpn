import { useEffect, useState } from 'react'
import { createCabinetApi, toStoredAuth } from './lib/api'
import { loadStoredAuth, saveStoredAuth } from './lib/storage'
import type { AuthResponse, CabinetProfile, StoredAuth } from './lib/types'

type ScreenState =
  | { status: 'loading' }
  | { status: 'anonymous' }
  | { status: 'authenticated'; auth: StoredAuth; profile: CabinetProfile }

type AuthMode = 'login' | 'register'

const dateFormatter = new Intl.DateTimeFormat('ru-RU', {
  dateStyle: 'medium',
  timeStyle: 'short',
})

export default function App() {
  const [screen, setScreen] = useState<ScreenState>({ status: 'loading' })
  const [mode, setMode] = useState<AuthMode>('login')
  const [authError, setAuthError] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)
  const [actionInfo, setActionInfo] = useState<string | null>(null)
  const [email, setEmail] = useState('owner@example.com')
  const [password, setPassword] = useState('supersecret')
  const [displayName, setDisplayName] = useState('Owner')

  useEffect(() => {
    void bootstrap()
  }, [])

  async function bootstrap() {
    const stored = loadStoredAuth()
    if (!stored) {
      setScreen({ status: 'anonymous' })
      return
    }

    try {
      const profile = await loadProfile(stored)
      setScreen({ status: 'authenticated', auth: stored, profile })
    } catch {
      try {
        const refreshed = await refreshAuth(stored)
        const profile = await loadProfile(refreshed)
        saveStoredAuth(refreshed)
        setScreen({ status: 'authenticated', auth: refreshed, profile })
      } catch {
        saveStoredAuth(null)
        setScreen({ status: 'anonymous' })
      }
    }
  }

  async function loadProfile(auth: StoredAuth): Promise<CabinetProfile> {
    const api = createCabinetApi({ accessToken: auth.accessToken })
    const [me, sessions, devices] = await Promise.all([api.me(), api.sessions(), api.devices()])
    return { me, sessions, devices }
  }

  async function refreshAuth(auth: StoredAuth): Promise<StoredAuth> {
    const api = createCabinetApi()
    const response = await api.refresh({ refreshToken: auth.refreshToken })
    return toStoredAuth(response)
  }

  async function handleSubmit() {
    setAuthError(null)
    setActionError(null)
    setActionInfo(null)

    const api = createCabinetApi()

    try {
      const response: AuthResponse =
        mode === 'login'
          ? await api.login({ email, password })
          : await api.register({ email, password, displayName })

      const stored = toStoredAuth(response)
      saveStoredAuth(stored)
      const profile = await loadProfile(stored)
      setScreen({ status: 'authenticated', auth: stored, profile })
      setActionInfo(mode === 'login' ? 'Вход выполнен.' : 'Аккаунт создан.')
    } catch (error) {
      setAuthError(getErrorMessage(error))
    }
  }

  async function handleLogout() {
    if (screen.status !== 'authenticated') {
      return
    }

    setActionError(null)
    const api = createCabinetApi({ accessToken: screen.auth.accessToken })

    try {
      await api.logout()
    } catch {
      // Local logout should still work even if the server refuses the request.
    }

    saveStoredAuth(null)
    setScreen({ status: 'anonymous' })
    setActionInfo('Сессия завершена.')
  }

  async function handleRefresh() {
    if (screen.status !== 'authenticated') {
      return
    }

    setActionError(null)

    try {
      const profile = await loadProfile(screen.auth)
      setScreen({ ...screen, profile })
      setActionInfo('Данные обновлены.')
    } catch (error) {
      setActionError(getErrorMessage(error))
    }
  }

  async function handleRevokeDevice(deviceId: string) {
    if (screen.status !== 'authenticated') {
      return
    }

    const api = createCabinetApi({ accessToken: screen.auth.accessToken })
    setActionError(null)

    try {
      await api.revokeDevice(deviceId)
      const profile = await loadProfile(screen.auth)
      setScreen({ ...screen, profile })
      setActionInfo('Устройство отозвано.')
    } catch (error) {
      setActionError(getErrorMessage(error))
    }
  }

  async function handleRevokeSession(sessionId: string) {
    if (screen.status !== 'authenticated') {
      return
    }

    const api = createCabinetApi({ accessToken: screen.auth.accessToken })
    setActionError(null)

    try {
      await api.revokeSession(sessionId)
      const profile = await loadProfile(screen.auth)
      setScreen({ ...screen, profile })
      setActionInfo('Сессия отозвана.')
    } catch (error) {
      setActionError(getErrorMessage(error))
    }
  }

  const activeDevices = screen.status === 'authenticated'
    ? screen.profile.devices.filter((device) => device.status.toLowerCase() === 'active')
    : []
  const activeSessions = screen.status === 'authenticated'
    ? screen.profile.sessions.filter((session) => session.status.toLowerCase() === 'active')
    : []

  return (
    <div className="app-shell">
      <header className="topbar">
        <div>
          <div className="eyebrow">VPN кабинет</div>
          <h1>Личный кабинет</h1>
        </div>
        <div className="topbar-actions">
          {screen.status === 'authenticated' ? (
            <>
              <button className="ghost-button" type="button" onClick={() => void handleRefresh()}>
                Обновить
              </button>
              <button className="ghost-button" type="button" onClick={() => void handleLogout()}>
                Выйти
              </button>
            </>
          ) : null}
        </div>
      </header>

      {screen.status === 'loading' ? (
        <section className="surface loading-surface">
          <p>Загрузка кабинета...</p>
        </section>
      ) : screen.status === 'anonymous' ? (
        <section className="auth-layout">
          <div className="hero-panel surface">
            <div className="eyebrow">Простой VPN-клиент</div>
            <h2>Вход, устройства, сессии и подписка без лишнего шума.</h2>
            <p>
              Здесь только то, что нужно пользователю: регистрация, вход, просмотр активного доступа,
              устройств и сессий.
            </p>
            <div className="note-box">
              API-настройка и reverse proxy задаются на стороне деплоя. В самом UI нет скрытых dev-панелей.
            </div>
          </div>

          <div className="surface auth-card">
            <div className="auth-tabs">
              <button className={mode === 'login' ? 'tab active' : 'tab'} type="button" onClick={() => setMode('login')}>
                Вход
              </button>
              <button
                className={mode === 'register' ? 'tab active' : 'tab'}
                type="button"
                onClick={() => setMode('register')}
              >
                Регистрация
              </button>
            </div>

            <label className="field">
              <span>Почта</span>
              <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" autoComplete="email" />
            </label>

            {mode === 'register' ? (
              <label className="field">
                <span>Имя</span>
                <input value={displayName} onChange={(event) => setDisplayName(event.target.value)} type="text" />
              </label>
            ) : null}

            <label className="field">
              <span>Пароль</span>
              <input
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                type="password"
                autoComplete={mode === 'login' ? 'current-password' : 'new-password'}
              />
            </label>

            {authError ? <div className="status-banner error">{authError}</div> : null}
            {actionInfo ? <div className="status-banner success">{actionInfo}</div> : null}

            <button className="primary-button" type="button" onClick={() => void handleSubmit()}>
              {mode === 'login' ? 'Войти' : 'Создать аккаунт'}
            </button>
          </div>
        </section>
      ) : (
        <section className="dashboard-grid">
          <div className="surface summary-panel">
            <div className="summary-head">
              <div>
                <div className="eyebrow">Аккаунт</div>
                <h2>{screen.profile.me.displayName}</h2>
              </div>
              <span className="badge">{screen.profile.me.status}</span>
            </div>

            <div className="summary-list">
              <div>
                <span>Почта</span>
                <strong>{screen.profile.me.email}</strong>
              </div>
              <div>
                <span>Активных устройств</span>
                <strong>{activeDevices.length}</strong>
              </div>
              <div>
                <span>Активных сессий</span>
                <strong>{activeSessions.length}</strong>
              </div>
              <div>
                <span>Всего устройств</span>
                <strong>{screen.profile.devices.length}</strong>
              </div>
            </div>
          </div>

          <div className="surface entitlement-panel">
            <div className="eyebrow">Активный доступ</div>
            {screen.profile.me.subscription ? (
              <div className="entitlement-card">
                <h3>{screen.profile.me.subscription.planName}</h3>
                <p>
                  Статус: {screen.profile.me.subscription.status}. Лимит устройств: {screen.profile.me.subscription.maxDevices}. Лимит сессий:{' '}
                  {screen.profile.me.subscription.maxConcurrentSessions}.
                </p>
                <div className="muted-row">
                  <span>{formatDateTime(screen.profile.me.subscription.startsAtUtc)}</span>
                  <span>{formatDateTime(screen.profile.me.subscription.endsAtUtc)}</span>
                </div>
              </div>
            ) : (
              <div className="empty-state">Активная подписка не найдена.</div>
            )}
            <div className="note-box compact">
              Точный summary по VPN access grant пока не выделен отдельным API. Сейчас кабинет показывает подписку, устройства и сессии.
            </div>
          </div>

          <div className="surface table-panel">
            <div className="panel-head">
              <div>
                <div className="eyebrow">Устройства</div>
                <h3>Эти установки имеют доступ</h3>
              </div>
              <span className="badge subtle">{screen.profile.devices.length}</span>
            </div>

            <table className="table">
              <thead>
                <tr>
                  <th>Имя</th>
                  <th>Платформа</th>
                  <th>Статус</th>
                  <th>Последний визит</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {screen.profile.devices.map((device) => (
                  <tr key={device.deviceId}>
                    <td>
                      <strong>{device.deviceName}</strong>
                      <div className="subtle-text">{device.clientVersion ?? 'версия не указана'}</div>
                    </td>
                    <td>{device.platform}</td>
                    <td>
                      <span className={device.status.toLowerCase() === 'active' ? 'pill success' : 'pill danger'}>
                        {device.status}
                      </span>
                    </td>
                    <td>{formatDateTime(device.lastSeenAtUtc)}</td>
                    <td className="right-cell">
                      <button className="link-button" type="button" onClick={() => void handleRevokeDevice(device.deviceId)}>
                        Отозвать
                      </button>
                    </td>
                  </tr>
                ))}
                {screen.profile.devices.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="empty-cell">
                      Пока нет зарегистрированных устройств.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>

          <div className="surface table-panel">
            <div className="panel-head">
              <div>
                <div className="eyebrow">Сессии</div>
                <h3>Текущие и недавние входы</h3>
              </div>
              <span className="badge subtle">{screen.profile.sessions.length}</span>
            </div>

            <table className="table">
              <thead>
                <tr>
                  <th>Статус</th>
                  <th>IP</th>
                  <th>Клиент</th>
                  <th>Создана</th>
                  <th>Последняя активность</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {screen.profile.sessions.map((session) => (
                  <tr key={session.sessionId}>
                    <td>
                      <span className={session.isCurrent ? 'pill current' : session.status.toLowerCase() === 'active' ? 'pill success' : 'pill muted'}>
                        {session.isCurrent ? 'Текущая' : session.status}
                      </span>
                    </td>
                    <td>{session.ipAddress ?? '—'}</td>
                    <td>{trimUserAgent(session.userAgent)}</td>
                    <td>{formatDateTime(session.createdAtUtc)}</td>
                    <td>{formatDateTime(session.lastSeenAtUtc)}</td>
                    <td className="right-cell">
                      {session.isCurrent ? (
                        <span className="subtle-text">текущая сессия</span>
                      ) : (
                        <button className="link-button" type="button" onClick={() => void handleRevokeSession(session.sessionId)}>
                          Отозвать
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
                {screen.profile.sessions.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="empty-cell">
                      Пока нет активных сессий.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>

          {actionError ? <div className="status-banner error full-width">{actionError}</div> : null}
          {actionInfo ? <div className="status-banner success full-width">{actionInfo}</div> : null}
        </section>
      )}
    </div>
  )
}

function getErrorMessage(error: unknown): string {
  if (error instanceof Error) {
    return error.message
  }

  return 'Неизвестная ошибка.'
}

function formatDateTime(value: string): string {
  return dateFormatter.format(new Date(value))
}

function trimUserAgent(value?: string | null): string {
  if (!value) {
    return '—'
  }

  return value.length > 32 ? `${value.slice(0, 32)}...` : value
}
