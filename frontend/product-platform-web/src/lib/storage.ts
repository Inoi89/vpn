import type { StoredAuth } from './types'

const authKey = 'vpn-product-platform.auth'

export function loadStoredAuth(): StoredAuth | null {
  const raw = localStorage.getItem(authKey)
  if (!raw) {
    return null
  }

  try {
    return JSON.parse(raw) as StoredAuth
  } catch {
    return null
  }
}

export function saveStoredAuth(value: StoredAuth | null): void {
  if (!value) {
    localStorage.removeItem(authKey)
    return
  }

  localStorage.setItem(authKey, JSON.stringify(value))
}
