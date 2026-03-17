# Operational State

Снимок текущего состояния VPN control plane и node agent слоёв в этом репозитории.

## Что уже работает

- `Control Plane API` живет на `192.168.1.2:7001`.
- `Control Plane UI` живет на `192.168.1.2:5080`.
- `Node Agent` запущен на всех активных Amnezia-нодах.
- Реальные действия по доступам работают end-to-end:
- выдача ключа;
- включение/отключение peer;
- удаление peer;
- выгрузка `.conf` и `.vpn`.

## Текущая топология

Сейчас в control plane зарегистрированы и видны следующие ноды:

- `5.61.37.29` - основная нода, должна идти первой в списке.
- `5.61.40.132`
- `38.180.137.249`
- `45.136.49.191`
- `185.21.10.217`
- `37.1.197.163`

Для UI закреплено правило:

- основная нода определяется по host `5.61.37.29`;
- она показывается первой в sidebar;
- рядом с ней отображается метка `Основная`.

Дополнительно в UI сделана нормализация имени:

- если нода называется `Amnezia Node 01`, UI показывает имя на базе IP;
- если имя ноды пустое или служебное, UI использует fallback через `agentIdentifier` или `agentBaseAddress`.

## Архитектура

### Control Plane

- ASP.NET Core 8 API.
- Clean Architecture: `Domain`, `Application`, `Infrastructure`, `Api`.
- `Hangfire` poll-джоба опрашивает все enabled nodes каждые 15 секунд.
- `PostgreSQL` хранит nodes, users, peer configs, sessions и traffic stats.
- `SignalR` пушит обновления сессий в UI.

### Node Agent

- Stateless service в Docker.
- Работает рядом с `amnezia-awg`.
- Читает:
- `wg0.conf`;
- `clientsTable`;
- `wireguard_psk.key`;
- `wireguard_server_public_key.key`.
- Делает snapshot через `wg show`.
- Создает/включает/отключает/удаляет peers.
- Для применения конфигурации использует `wg syncconf`, а не `wg set`.

### UI

- React + TypeScript + Vite.
- Основа в стиле DashboardKit.
- Sidebar всегда слева.
- Sidebar содержит список нод и общий обзор.
- Основной рабочий экран строится вокруг выбранной ноды.

## Что важно помнить

### 1. `test123` и другие старые файлы не являются эталоном

Ранее мы сравнивали старые конфиги из `Downloads`, но они не всегда соответствовали live peer на ноде. Это приводило к ложным выводам.

### 2. Две формы экспорта

- `.conf` - raw AmneziaWG config.
- `.vpn` - базовый Amnezia config, encoded через `qCompress` + base64url.

### 3. Current known gap

Проблема выдачи/импорта еще не считается полностью закрытой.

Что уже подтверждено:

- наша выдача теперь включает `DNS`;
- raw `.conf` теперь включает `MTU`;
- AWG-поля `Jc/Jmin/Jmax/S1/S2/S3/S4/H1/H2/H3/H4/I1..I5` сохраняются, если они есть в source config;
- node agent применяет изменения через `syncconf`;
- `5.61.37.29` теперь живет как primary node и отвечает на `/healthz`.

Что еще надо держать в голове:

- raw third-party import path в Amnezia может вести себя не совсем так же, как ключ, созданный внутри приложения;
- у нас уже были случаи, когда tunnel поднимался, а реального интернета у клиента не было;
- это не выглядит как один-единственный server-side баг.

### 4. Node Agent bootstrap

На `5.61.37.29` агент сначала был поднят в `aspnet`-образе без `docker-cli`, поэтому `/v1/agent/snapshot` падал.

Фикс:

- пересборка agent-образа с `docker.io` внутри;
- запуск `vpn-node-agent` в `Docker` mode;
- проверка snapshot через control plane снова стала успешной;
- нода перешла в `Healthy`.

## Деплойный паттерн

### Node Agent

1. Собрать publish output.
2. Скопировать publish на хост.
3. Запустить agent контейнер с:
- `ASPNETCORE_URLS=https://+:8443`;
- монтированным `node-agent.pfx`;
- `Agent__OperationMode=Docker`;
- `Agent__DockerContainerName=amnezia-awg`;
- `Agent__ConfigDirectory=/opt/amnezia/awg`;
- `Agent__AllowedClientThumbprints__0=<control-plane-client-thumbprint>`.
4. Проверить `GET /healthz`.
5. Проверить `GET /v1/agent/snapshot`.

### Control Plane registration

- `POST /api/nodes/register`
- `agentIdentifier` должен быть стабильным и уникальным.
- `agentBaseAddress` указывает на `https://<host>:8443`.
- `name` лучше держать человекочитаемым, но UI все равно нормализует generic names.

## Полезные endpoints

- `GET /api/dashboard?trafficPoints=120`
- `GET /api/nodes`
- `POST /api/nodes/register`
- `POST /api/nodes/{nodeId}/accesses`
- `POST /api/nodes/{nodeId}/accesses/{userId}/state`
- `DELETE /api/nodes/{nodeId}/accesses/{userId}`
- `GET /api/nodes/{nodeId}/accesses/{userId}/config?format=amnezia-vpn`
- `GET /api/nodes/{nodeId}/accesses/{userId}/config?format=amnezia-awg-native`

## Проверка

Проверки, которые уже проходили:

- `dotnet build VpnControlPlane.sln`
- `dotnet test VpnControlPlane.sln`
- `npm run build` в `frontend/control-plane-ui`

## Что осталось сделать

- Довести compatibility story для Amnezia import path до конца.
- Проверить, что новые `.conf` и `.vpn` ведут себя одинаково предсказуемо на всех клиентах.
- Если понадобится, убрать генерацию peer-конфига из UI-флоу и оставить только:
- `Добавить сервер из файла`;
- `Подключиться`.

