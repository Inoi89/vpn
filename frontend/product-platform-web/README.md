# Product Platform Web

Минимальный пользовательский кабинет для `VpnProductPlatform`.

## Задача

Кабинет показывает:

- регистрацию и вход
- текущий аккаунт
- статус подписки
- устройства
- сессии
- активные выданные доступы
- email verification flow

## Локальный запуск

```powershell
cd frontend/product-platform-web
npm install
npm run dev
```

## Сборка

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

## Конфигурация API

По умолчанию фронт использует тот же origin, что и страница.

Если нужен отдельный API host, задай:

```powershell
$env:VITE_API_BASE_URL = "https://api.etojesim.com"
npm run dev
```

Используемые endpoint'ы:

- `POST /api/auth/register`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/logout`
- `POST /api/auth/verify-email`
- `POST /api/auth/resend-verification-email`
- `GET /api/me`
- `GET /api/access-grants`
- `GET /api/devices`
- `POST /api/devices`
- `DELETE /api/devices/{deviceId}`
- `GET /api/sessions`
- `DELETE /api/sessions/{sessionId}`

## Поведение verification flow

Если в URL есть `?verify=<token>`, кабинет:

1. вызывает `POST /api/auth/verify-email`
2. показывает success/error banner
3. очищает query string из адресной строки

Если аккаунт в состоянии `PendingVerification`, кабинет показывает заметный баннер с кнопкой повторной отправки письма.

## Примечание

Кабинет специально сделан flat и простым. Здесь нет операторского control plane и нет логики нод.
