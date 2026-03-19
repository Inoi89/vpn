# Product Platform Web

Минимальный кабинет для `VpnProductPlatform`.

## Dev

```powershell
cd frontend/product-platform-web
npm install
npm run dev
```

## Build

```powershell
cd frontend/product-platform-web
npm run build
```

## Docker

```powershell
cd frontend/product-platform-web
docker build -t product-platform-web .
docker run --rm -p 8080:80 product-platform-web
```

## API

По умолчанию фронт берет адрес API из `VITE_API_BASE_URL`.

```powershell
$env:VITE_API_BASE_URL = "http://localhost:7101"
npm run dev
```

Используемые endpoint'ы:

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`
- `GET /api/me`
- `GET /api/devices`
- `POST /api/devices`
- `DELETE /api/devices/{deviceId}`
- `GET /api/sessions`
- `DELETE /api/sessions/{sessionId}`

Сейчас нет отдельного публичного API для "активного VPN access grant" на уровне пользователя. В UI это показано через подписку, устройства и сессии, а точный grant summary отмечен как следующий API gap.
