# Product Platform Web

Минимальный пользовательский кабинет для `VpnProductPlatform`.

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

По умолчанию фронт берёт адрес API из `VITE_API_BASE_URL`.

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
- `GET /api/access-grants`
- `GET /api/devices`
- `POST /api/devices`
- `DELETE /api/devices/{deviceId}`
- `GET /api/sessions`
- `DELETE /api/sessions/{sessionId}`

## Current MVP Deployment

Текущий временный rollout работает так:

- API origin живёт на `192.168.1.2` за nginx vhost `api.etojesim.com`
- прямой доступ к origin ограничен `127.0.0.1` и `5.61.37.29`
- сам кабинет поднят на `5.61.37.29:80`
- web-host проксирует `/api/` сервер-сайдно в `http://93.100.54.80` с `Host: api.etojesim.com`

Это позволяет проверять кабинет по IP веб-хоста без прямого browser-доступа к API.
