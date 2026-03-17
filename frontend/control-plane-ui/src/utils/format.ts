export function formatBytes(bytes: number) {
  if (bytes <= 0) {
    return '0 Б'
  }

  const units = ['Б', 'КБ', 'МБ', 'ГБ', 'ТБ']
  const index = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1)
  const value = bytes / 1024 ** index
  return `${value.toFixed(value >= 100 ? 0 : 1)} ${units[index]}`
}

export function formatDateTime(value?: string | null) {
  if (!value) {
    return 'никогда'
  }

  return new Date(value).toLocaleString('ru-RU')
}

export function formatTime(value?: string | null) {
  if (!value) {
    return 'нет'
  }

  return new Date(value).toLocaleTimeString('ru-RU', {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  })
}

export function formatRelativeTime(value?: string | null) {
  if (!value) {
    return 'никогда'
  }

  const target = new Date(value).getTime()
  const diffSeconds = Math.round((target - Date.now()) / 1000)
  const absoluteSeconds = Math.abs(diffSeconds)

  if (absoluteSeconds < 10) {
    return 'только что'
  }

  const intervals = [
    { limit: 60, unit: 'second' },
    { limit: 3600, unit: 'minute' },
    { limit: 86400, unit: 'hour' },
    { limit: 604800, unit: 'day' },
  ] as const

  const formatter = new Intl.RelativeTimeFormat('ru-RU', { numeric: 'auto' })

  for (const interval of intervals) {
    if (absoluteSeconds < interval.limit) {
      const divisor =
        interval.unit === 'second'
          ? 1
          : interval.unit === 'minute'
            ? 60
            : interval.unit === 'hour'
              ? 3600
              : 86400

      return formatter.format(Math.round(diffSeconds / divisor), interval.unit)
    }
  }

  return formatter.format(Math.round(diffSeconds / 604800), 'week')
}

export function shortKey(value: string, visible = 10) {
  if (value.length <= visible * 2) {
    return value
  }

  return `${value.slice(0, visible)}...${value.slice(-visible)}`
}

export function formatNodeStatus(status: string) {
  switch (status) {
    case 'Healthy':
      return 'В норме'
    case 'Unreachable':
      return 'Недоступна'
    case 'Provisioning':
      return 'Подключается'
    case 'Disabled':
      return 'Отключена'
    default:
      return status
  }
}

export function formatSessionState(state: string) {
  switch (state) {
    case 'Active':
      return 'Активна'
    case 'Disconnected':
      return 'Отключена'
    default:
      return state
  }
}
