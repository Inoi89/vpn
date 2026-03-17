import { useState } from 'react'
import type { FormEvent } from 'react'
import type { UpsertUserRequest, UserSummary } from '../types/dashboard'

type UserManagementProps = {
  users: UserSummary[]
  isSaving: boolean
  onSave: (request: UpsertUserRequest) => Promise<unknown>
}

const defaultForm: UpsertUserRequest = {
  externalId: '',
  displayName: '',
  email: '',
  isEnabled: true,
}

export function UserManagement({ users, isSaving, onSave }: UserManagementProps) {
  const [form, setForm] = useState(defaultForm)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    await onSave(form)
    setForm(defaultForm)
  }

  return (
    <section className="panel split-panel">
      <div>
        <div className="panel-header">
          <div>
            <p className="eyebrow">Users</p>
            <h2>Provisioning</h2>
          </div>
          <p className="panel-meta">{users.length} managed</p>
        </div>

        <div className="user-list">
          {users.map((user) => (
            <article className="user-card" key={user.id}>
              <div>
                <h3>{user.displayName}</h3>
                <p>{user.email ?? user.externalId}</p>
              </div>
              <div className="user-card-meta">
                <span>{user.peerCount} peers</span>
                <span className={user.isEnabled ? 'chip chip-live' : 'chip'}>{user.isEnabled ? 'Enabled' : 'Disabled'}</span>
              </div>
            </article>
          ))}
        </div>
      </div>

      <form className="user-form" onSubmit={handleSubmit}>
        <p className="eyebrow">Upsert user</p>
        <label>
          External ID
          <input
            required
            value={form.externalId}
            onChange={(event) => setForm((current) => ({ ...current, externalId: event.target.value }))}
          />
        </label>
        <label>
          Display name
          <input
            required
            value={form.displayName}
            onChange={(event) => setForm((current) => ({ ...current, displayName: event.target.value }))}
          />
        </label>
        <label>
          Email
          <input
            type="email"
            value={form.email ?? ''}
            onChange={(event) => setForm((current) => ({ ...current, email: event.target.value }))}
          />
        </label>
        <label className="checkbox-row">
          <input
            type="checkbox"
            checked={form.isEnabled}
            onChange={(event) => setForm((current) => ({ ...current, isEnabled: event.target.checked }))}
          />
          Enabled
        </label>
        <button disabled={isSaving} type="submit">
          {isSaving ? 'Saving...' : 'Save user'}
        </button>
      </form>
    </section>
  )
}
