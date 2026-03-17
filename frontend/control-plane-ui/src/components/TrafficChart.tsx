import type { TrafficPoint } from '../types/dashboard'

type TrafficChartProps = {
  traffic: TrafficPoint[]
}

function formatBytes(bytes: number) {
  if (bytes <= 0) {
    return '0 B'
  }

  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  const index = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), units.length - 1)
  const value = bytes / 1024 ** index
  return `${value.toFixed(value >= 100 ? 0 : 1)} ${units[index]}`
}

export function TrafficChart({ traffic }: TrafficChartProps) {
  const points = traffic.slice(-18)
  const maxValue = Math.max(...points.map((point) => point.rxBytes + point.txBytes), 1)

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <p className="eyebrow">Traffic</p>
          <h2>Recent transfer envelope</h2>
        </div>
        <p className="panel-meta">last {points.length} samples</p>
      </div>
      <div className="traffic-bars">
        {points.map((point) => {
          const total = point.rxBytes + point.txBytes
          const height = `${Math.max((total / maxValue) * 100, 8)}%`

          return (
            <div className="traffic-bar" key={`${point.capturedAtUtc}-${point.userDisplayName}`}>
              <div className="traffic-bar-stack" style={{ height }}>
                <span className="traffic-bar-rx" style={{ height: `${(point.rxBytes / Math.max(total, 1)) * 100}%` }} />
                <span className="traffic-bar-tx" style={{ height: `${(point.txBytes / Math.max(total, 1)) * 100}%` }} />
              </div>
              <strong>{point.userDisplayName}</strong>
              <p>{formatBytes(total)}</p>
            </div>
          )
        })}
      </div>
    </section>
  )
}
