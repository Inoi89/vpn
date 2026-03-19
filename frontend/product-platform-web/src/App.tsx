import { useEffect, useState } from 'react'
import { createCabinetApi, toStoredAuth } from './lib/api'
import { loadStoredAuth, saveStoredAuth } from './lib/storage'
import type {
  AccessGrantResponse,
  AuthResponse,
  DeviceResponse,
  MeResponse,
  SessionResponse,
  StoredAuth,
} from './lib/types'

type ScreenState =
  | { status: 'loading' }
  | { status: 'anonymous' }
  | { status: 'authenticated'; auth: StoredAuth; me: MeResponse; sessions: SessionResponse[]; devices: DeviceResponse[]; accessGrants: AccessGrantResponse[] }

type AuthMode = 'login' | 'register'

type BannerTone = 'success' | 'error' | 'info'

type Banner = {
  tone: BannerTone
  title: string
  text: string
}

const dateFormatter = new Intl.DateTimeFormat('ru-RU', {
  dateStyle: 'medium',
  timeStyle: 'short',
})

export default function App() {
  const [screen, setScreen] = useState<ScreenState>({ status: 'loading' })
  const [mode, setMode] = useState<AuthMode>('login')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [banner, setBanner] = useState<Banner | null>(null)
  const [verifyBanner, setVerifyBanner] = useState<Banner | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isResendingVerification, setIsResendingVerification] = useState(false)

  useEffect(() => {
    void bootstrap()
    void handleVerifyQuery()
  }, [])

  async function bootstrap() {
    const stored = loadStoredAuth()

    if (!stored) {
      setScreen({ status: 'anonymous' })
      return
    }

    try {
      const profile = await loadProfile(stored)
      setScreen({ status: 'authenticated', auth: stored, ...profile })
      return
    } catch {
      // Try a refresh token before forcing a logout.
    }

    try {
      const refreshed = await refreshAuth(stored)
      saveStoredAuth(refreshed)
      const profile = await loadProfile(refreshed)
      setScreen({ status: 'authenticated', auth: refreshed, ...profile })
    } catch {
      saveStoredAuth(null)
      setScreen({ status: 'anonymous' })
    }
  }

  async function handleVerifyQuery() {
    const token = new URLSearchParams(window.location.search).get('verify')
    if (!token) {
      return
    }

    clearVerifyQuery()
    setVerifyBanner({
      tone: 'info',
      title: 'Подтверждение почты',
      text: 'Проверяем ссылку подтверждения...',
    })

    try {
      const api = createCabinetApi()
      await api.verifyEmail({ token })

      setVerifyBanner({
        tone: 'success',
        title: 'Почта подтверждена',
        text: 'Email успешно подтверждён. Если вы уже были в кабинете, данные обновятся автоматически.',
      })

      const stored = loadStoredAuth()
      if (!stored) {
        return
      }

      try {
        const profile = await loadProfile(stored)
        setScreen({ status: 'authenticated', auth: stored, ...profile })
      } catch {
        // Keep current view. The token was accepted; refresh can happen later.
      }
    } catch (error) {
      setVerifyBanner({
        tone: 'error',
        title: 'Не удалось подтвердить почту',
        text: getErrorMessage(error),
      })
    }
  }

  async function loadProfile(auth: StoredAuth) {
    const api = createCabinetApi({ accessToken: auth.accessToken })
    const [me, sessions, devices, accessGrants] = await Promise.all([
      api.me(),
      api.sessions(),
      api.devices(),
      api.accessGrants(),
    ])

    return { me, sessions, devices, accessGrants }
  }

  async function refreshAuth(auth: StoredAuth): Promise<StoredAuth> {
    const api = createCabinetApi()
    const response = await api.refresh({ refreshToken: auth.refreshToken })
    return toStoredAuth(response)
  }

  async function handleSubmit() {
    setBanner(null)

    const api = createCabinetApi()
    setIsSubmitting(true)

    try {
      const response: AuthResponse =
        mode === 'login'
          ? await api.login({ email, password })
          : await api.register({ email, password, displayName })

      const stored = toStoredAuth(response)
      saveStoredAuth(stored)
      const profile = await loadProfile(stored)
      setScreen({ status: 'authenticated', auth: stored, ...profile })
      setBanner({
        tone: 'success',
        title: mode === 'login' ? 'Вход выполнен' : 'Аккаунт создан',
        text: mode === 'login' ? 'Сессия кабинета обновлена.' : 'Аккаунт создан и сохранён в кабинете.',
      })
    } catch (error) {
      setBanner({
        tone: 'error',
        title: 'Не удалось выполнить действие',
        text: getErrorMessage(error),
      })
    } finally {
      setIsSubmitting(false)
    }
  }

  async function handleLogout() {
    if (screen.status !== 'authenticated') {
      return
    }

    setBanner(null)
    const api = createCabinetApi({ accessToken: screen.auth.accessToken })

    try {
      await api.logout()
    } catch {
      // Local logout is still valid even if the backend rejects this request.
    }

    saveStoredAuth(null)
    setScreen({ status: 'anonymous' })
    setBanner({
      tone: 'success',
      title: 'Сессия завершена',
      text: 'Локальные данные кабинета очищены.',
    })
  }

  async function handleRefresh() {
    if (screen.status !== 'authenticated') {
      return
    }

    setBanner(null)

    try {
      const profile = await loadProfile(screen.auth)
      setScreen({ ...screen, ...profile })
      setBanner({
        tone: 'success',
        title: 'Данные обновлены',
        text: 'Кабинет получил свежий снимок аккаунта.',
      })
    } catch (error) {
      setBanner({
        tone: 'error',
        title: 'Не удалось обновить данные',
        text: getErrorMessage(error),
      })
    }
  }

  async function handleResendVerificationEmail() {
    if (screen.status !== 'authenticated') {
      return
    }

    setBanner(null)
    setIsResendingVerification(true)

    try {
      const api = createCabinetApi({ accessToken: screen.auth.accessToken })
      await api.resendVerificationEmail()
      setBanner({
        tone: 'success',
        title: 'Письмо отправлено',
        text: 'Проверьте почту и перейдите по ссылке подтверждения.',
      })
    } catch (error) {
      setBanner({
        tone: 'error',
        title: 'Не удалось отправить письмо',
        text: getErrorMessage(error),
      })
    } finally {
      setIsResendingVerification(false)
    }
  }

  async function handleRevokeDevice(deviceId: string) {
    if (screen.status !== 'authenticated') {
      return
    }

    setBanner(null)

    try {
      const api = createCabinetApi({ accessToken: screen.auth.accessToken })
      await api.revokeDevice(deviceId)
      const profile = await loadProfile(screen.auth)
      setScreen({ ...screen, ...profile })
      setBanner({
        tone: 'success',
        title: 'Устройство отозвано',
        text: 'Доступ устройства удалён.',
      })
    } catch (error) {
      setBanner({
        tone: 'error',
        title: 'Не удалось отозвать устройство',
        text: getErrorMessage(error),
      })
    }
  }

  async function handleRevokeSession(sessionId: string) {
    if (screen.status !== 'authenticated') {
      return
    }

    setBanner(null)

    try {
      const api = createCabinetApi({ accessToken: screen.auth.accessToken })
      await api.revokeSession(sessionId)
      const profile = await loadProfile(screen.auth)
      setScreen({ ...screen, ...profile })
      setBanner({
        tone: 'success',
        title: 'Сессия отозвана',
        text: 'Сессия пользователя завершена.',
      })
    } catch (error) {
      setBanner({
        tone: 'error',
        title: 'Не удалось отозвать сессию',
        text: getErrorMessage(error),
      })
    }
  }

  const isPendingVerification =
    screen.status === 'authenticated' && normalizeStatus(screen.me.status) === 'pendingverification'

  return (
    <div className="app-shell">
      <header className="topbar">
        <div>
          <div className="eyebrow">VpnProductPlatform</div>
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

      {verifyBanner ? <BannerView banner={verifyBanner} /> : null}
      {banner ? <BannerView banner={banner} /> : null}

      {screen.status === 'loading' ? (
        <section className="surface loading-surface">
          <p>Загрузка кабинета...</p>
        </section>
      ) : screen.status === 'anonymous' ? (
        <section className="auth-layout">
          <div className="hero-panel surface">
            <div className="eyebrow">Пользовательский слой</div>
            <h2>Аккаунт, устройства и активный доступ без лишнего шума.</h2>
            <p>
              Здесь только то, что реально нужно пользователю: регистрация, вход, подтверждение почты, просмотр
              своей подписки, устройств и активных сессий.
            </p>
            <div className="note-box">
              VPN-узлы и операторский control plane остаются отдельным внутренним контуром. Этот кабинет отвечает
              только за аккаунт, устройства и пользовательский доступ.
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

            <button className="primary-button" type="button" onClick={() => void handleSubmit()} disabled={isSubmitting}>
              {isSubmitting ? 'Подождите...' : mode === 'login' ? 'Войти' : 'Создать аккаунт'}
            </button>
          </div>
        </section>
      ) : (
        <section className="dashboard-grid">
          {isPendingVerification ? (
            <section className="surface notice-banner warning full-width">
              <div>
                <div className="eyebrow">Требуется подтверждение</div>
                <h2>Почта ещё не подтверждена</h2>
                <p>
                  Доступ к подписке и устройствам уже есть, но для завершения регистрации нужно подтвердить email.
                  Если письмо не дошло, можно отправить его ещё раз.
                </p>
              </div>
              <button
                className="primary-button compact"
                type="button"
                onClick={() => void handleResendVerificationEmail()}
                disabled={isResendingVerification}
              >
                {isResendingVerification ? 'Отправка...' : 'Отправить письмо ещё раз'}
              </button>
            </section>
          ) : null}

          <div className="surface summary-panel">
            <div className="summary-head">
              <div>
                <div className="eyebrow">Аккаунт</div>
                <h2>{screen.me.displayName}</h2>
              </div>
              <span className="badge">{screen.me.status}</span>
            </div>

            <div className="summary-list">
              <div>
                <span>Почта</span>
                <strong>{screen.me.email}</strong>
              </div>
              <div>
                <span>Активных устройств</span>
                <strong>{activeDevices(screen.devices).length}</strong>
              </div>
              <div>
                <span>Активных сессий</span>
                <strong>{activeSessions(screen.sessions).length}</strong>
              </div>
              <div>
                <span>Всего устройств</span>
                <strong>{screen.devices.length}</strong>
              </div>
              <div>
                <span>Активных ключей</span>
                <strong>{activeAccessGrants(screen.accessGrants).length}</strong>
              </div>
            </div>
          </div>

          <div className="surface entitlement-panel">
            <div className="eyebrow">Подписка</div>
            {screen.me.subscription ? (
              <div className="entitlement-card">
                <h3>{screen.me.subscription.planName}</h3>
                <p>
                  Статус: {screen.me.subscription.status}. Лимит устройств: {screen.me.subscription.maxDevices}. Лимит
                  одновременных сессий: {screen.me.subscription.maxConcurrentSessions}.
                </p>
                <div className="muted-row">
                  <span>{formatDateTime(screen.me.subscription.startsAtUtc)}</span>
                  <span>{formatDateTime(screen.me.subscription.endsAtUtc)}</span>
                </div>
              </div>
            ) : (
              <div className="empty-state">Активная подписка не найдена.</div>
            )}
          </div>

          <div className="surface table-panel">
            <div className="panel-head">
              <div>
                <div className="eyebrow">VPN доступы</div>
                <h3>Активные и выданные ключи</h3>
              </div>
              <span className="badge subtle">{screen.accessGrants.length}</span>
            </div>

            <table className="table">
              <thead>
                <tr>
                  <th>Устройство</th>
                  <th>Формат</th>
                  <th>Статус</th>
                  <th>Выдан</th>
                  <th>Публичный ключ</th>
                </tr>
              </thead>
              <tbody>
                {screen.accessGrants.map((grant) => (
                  <tr key={grant.accessGrantId}>
                    <td>
                      <strong>{grant.deviceName}</strong>
                    </td>
                    <td>{grant.configFormat}</td>
                    <td>
                      <span className={normalizeStatus(grant.status) === 'active' ? 'pill success' : 'pill muted'}>
                        {grant.status}
                      </span>
                    </td>
                    <td>{formatDateTime(grant.issuedAtUtc)}</td>
                    <td className="mono-cell">{trimPublicKey(grant.peerPublicKey)}</td>
                  </tr>
                ))}
                {screen.accessGrants.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="empty-cell">
                      Пока нет выданных VPN-доступов.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>

          <div className="surface table-panel">
            <div className="panel-head">
              <div>
                <div className="eyebrow">Устройства</div>
                <h3>Установки с доступом</h3>
              </div>
              <span className="badge subtle">{screen.devices.length}</span>
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
                {screen.devices.map((device) => (
                  <tr key={device.deviceId}>
                    <td>
                      <strong>{device.deviceName}</strong>
                      <div className="subtle-text">{device.clientVersion ?? 'версия не указана'}</div>
                    </td>
                    <td>{device.platform}</td>
                    <td>
                      <span className={normalizeStatus(device.status) === 'active' ? 'pill success' : 'pill danger'}>
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
                {screen.devices.length === 0 ? (
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
              <span className="badge subtle">{screen.sessions.length}</span>
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
                {screen.sessions.map((session) => (
                  <tr key={session.sessionId}>
                    <td>
                      <span
                        className={
                          session.isCurrent ? 'pill current' : normalizeStatus(session.status) === 'active' ? 'pill success' : 'pill muted'
                        }
                      >
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
                {screen.sessions.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="empty-cell">
                      Пока нет активных сессий.
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  )
}

function BannerView({ banner }: { banner: Banner }) {
  return (
    <section className={`surface notice-banner ${banner.tone}`}>
      <div>
        <div className="eyebrow">{banner.title}</div>
        <p>{banner.text}</p>
      </div>
    </section>
  )
}

function clearVerifyQuery() {
  const nextUrl = `${window.location.pathname}${window.location.hash}`
  window.history.replaceState({}, document.title, nextUrl)
}

function activeDevices(devices: DeviceResponse[]): DeviceResponse[] {
  return devices.filter((device) => normalizeStatus(device.status) === 'active')
}

function activeSessions(sessions: SessionResponse[]): SessionResponse[] {
  return sessions.filter((session) => normalizeStatus(session.status) === 'active')
}

function activeAccessGrants(accessGrants: AccessGrantResponse[]): AccessGrantResponse[] {
  return accessGrants.filter((grant) => normalizeStatus(grant.status) === 'active')
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

function normalizeStatus(value: string): string {
  return value.toLowerCase().replace(/[^a-zа-я0-9]/gi, '')
}

function trimUserAgent(value?: string | null): string {
  if (!value) {
    return '—'
  }

  return value.length > 32 ? `${value.slice(0, 32)}...` : value
}

function trimPublicKey(value?: string | null): string {
  if (!value) {
    return '—'
  }

  return value.length > 18 ? `${value.slice(0, 18)}...` : value
}
