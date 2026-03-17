import type { TrafficPoint } from '../types/dashboard'
import { formatBytes, formatTime } from '../utils/format'

type TrafficChartProps = {
  traffic: TrafficPoint[]
}

export function TrafficChart({ traffic }: TrafficChartProps) {
  const points = traffic.slice(-8)
  const totalsByUser = new Map<string, number>()

  for (const point of points) {
    totalsByUser.set(point.userDisplayName, (totalsByUser.get(point.userDisplayName) ?? 0) + point.rxBytes + point.txBytes)
  }

  const leaderboard = [...totalsByUser.entries()].sort((left, right) => right[1] - left[1]).slice(0, 5)
  const maxValue = Math.max(...points.map((point) => point.rxBytes + point.txBytes), 1)

  return (
    <div className="card support-bar overflow-hidden">
      <div className="card-body pb-0">
        <h5>Общая динамика сети</h5>
        <span className="text-primary">Только для общего обзора</span>
        <p className="mb-3 mt-3">Последние замеры трафика по всему контуру и пользователи с максимальной нагрузкой.</p>
      </div>

      <div className="traffic-widget">
        {points.length === 0 ? (
          <div className="empty-panel">Пока нет замеров трафика.</div>
        ) : (
          <div className="traffic-bars">
            {points.map((point) => {
              const total = point.rxBytes + point.txBytes
              const height = `${Math.max((total / maxValue) * 100, 10)}%`

              return (
                <div className="traffic-bar-item" key={`${point.capturedAtUtc}-${point.userDisplayName}`}>
                  <div className="traffic-bar-stack" style={{ height }}>
                    <span
                      className="traffic-bar-rx"
                      style={{ height: `${(point.rxBytes / Math.max(total, 1)) * 100}%` }}
                    />
                    <span
                      className="traffic-bar-tx"
                      style={{ height: `${(point.txBytes / Math.max(total, 1)) * 100}%` }}
                    />
                  </div>
                  <strong>{point.userDisplayName}</strong>
                  <small>{formatTime(point.capturedAtUtc)}</small>
                </div>
              )
            })}
          </div>
        )}
      </div>

      <div className="card-footer border-0">
        <div className="leader-table">
          {leaderboard.length === 0 ? (
            <div className="empty-panel empty-panel-compact">Нет пользователей с активным трафиком.</div>
          ) : (
            leaderboard.map(([userDisplayName, total]) => (
              <div className="leader-row" key={userDisplayName}>
                <div>
                  <strong>{userDisplayName}</strong>
                  <span>Суммарный объём за последние точки</span>
                </div>
                <b>{formatBytes(total)}</b>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  )
}
