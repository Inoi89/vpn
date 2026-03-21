# macOS Tester Handoff

Current checkpoint:

- the app can now be built and launched on macOS
- auth works
- managed access issuance works
- helper/runtime bridge work
- the current blocker for real VPN traffic is Apple signing for
  `NetworkExtension`

See:

- [macos-current-state.md](/c:/Users/rrese/source/repos/vpn/docs/macos-current-state.md)

Эта инструкция не для разработчика. Она для человека, которому нужно один раз
собрать `etoVPN` на Mac и прогнать первый smoke-тест.

## Что ему отправить

Самый безопасный вариант:

- один `.zip`-архив со всем репозиторием `vpn`

Чтобы архив был легче, можно не включать:

- `.git/`
- `.vs/`
- `.tmp/`
- `.codex-tmp/`
- `artifacts/`
- все `bin/`
- все `obj/`
- `TestResults/`

Но обязательно должны остаться:

- `Application/`
- `Core/`
- `Infrastructure/`
- `UI/`
- `Updater/`
- `deploy/`
- `docs/`
- `native/`
- `third_party/`
- `.research/amnezia-client/`
- `Directory.Build.props`
- `VpnClient.sln`

Особенно важны внутри `.research/amnezia-client/`:

- `client/3rd/amneziawg-apple/`
- `client/3rd-prebuilt/`
- `client/macos/`

Если этих папок не будет, сборка на Mac станет заметно сложнее и может
потребовать `git` и `go`.

## Что заранее важно понять

Для реального VPN-smoke нужен Apple signing context для `NetworkExtension`.

То есть:

- просто "есть Mac" недостаточно
- `Xcode` должен уметь подписать app и packet tunnel extension
- если Xcode упрется в provisioning, entitlement или signing, это уже не баг
  нашего кода, а ограничение Apple-аккаунта или профиля

Практически это значит так:

- если сборка доходит до готового `.app`, но VPN не стартует из-за signing,
  это уже полезный результат
- если extension вообще не собирается из-за `NetworkExtension` capability,
  значит нужен Mac с нормальным Apple developer/signing setup

## Что ему установить

Минимум:

1. `Xcode` из App Store
2. `Xcode Command Line Tools`
3. `.NET 8 SDK`
4. `XcodeGen`

`git` и `go` обычно не нужны, если ты отправляешь полный архив репозитория уже
с включенной `.research/amnezia-client/`.

## Что ему написать

Можешь переслать ему это почти как есть:

```text
Привет. Нужно один раз собрать и проверить macOS-версию нашего VPN-клиента.

1. Распакуй архив с проектом, например в ~/Desktop/vpn
2. Установи:
   - Xcode
   - Xcode Command Line Tools
   - .NET 8 SDK
   - XcodeGen
3. Открой Terminal
4. Выполни:

cd ~/Desktop/vpn
chmod +x native/macos/build-native.sh deploy/client/publish-macos.sh
xcode-select --install
uname -m

Если uname -m показывает arm64, выполняй:

./native/macos/build-native.sh --configuration Release --runtime osx-arm64
RUNTIME_IDENTIFIER=osx-arm64 ./deploy/client/publish-macos.sh

Если uname -m показывает x86_64, выполняй:

./native/macos/build-native.sh --configuration Release --runtime osx-x64
RUNTIME_IDENTIFIER=osx-x64 ./deploy/client/publish-macos.sh

Потом открой:

artifacts/client-publish/<runtime>/etoVPN.app

Если macOS пишет, что приложение от неизвестного разработчика:
- сначала попробуй правой кнопкой -> Open
- если не помогает, выполни:
  xattr -dr com.apple.quarantine artifacts/client-publish/<runtime>/etoVPN.app
  open artifacts/client-publish/<runtime>/etoVPN.app

Напиши мне:
- собралось или нет
- если нет, пришли полный вывод Terminal и скрин ошибки
- если да, открылось ли приложение
- получается ли нажать Connect
- есть ли реальный трафик через VPN
- работает ли Disconnect
- что происходит после перезапуска приложения
```

## Прямо по шагам для него

### 1. Распаковать архив

Например сюда:

`~/Desktop/vpn`

### 2. Поставить инструменты

Нужно:

- `Xcode`
- `Xcode Command Line Tools`
- `.NET 8 SDK`
- `XcodeGen`

Для `XcodeGen` самый простой путь:

```bash
brew install xcodegen
```

### 3. Открыть Terminal

Перейти в папку проекта:

```bash
cd ~/Desktop/vpn
```

Если архив пришел с Windows, нужно вернуть флаг исполняемости скриптам:

```bash
chmod +x native/macos/build-native.sh
chmod +x deploy/client/publish-macos.sh
```

### 4. Поставить Xcode Command Line Tools

```bash
xcode-select --install
```

Если macOS ответит, что tools уже установлены, это нормально.

### 5. Определить тип Mac

```bash
uname -m
```

Варианты:

- `arm64` = Apple Silicon
- `x86_64` = Intel Mac

### 6. Собрать native часть

Для Apple Silicon:

```bash
./native/macos/build-native.sh --configuration Release --runtime osx-arm64
```

Для Intel:

```bash
./native/macos/build-native.sh --configuration Release --runtime osx-x64
```

Ожидаемый результат:

- `artifacts/macos-native/<runtime>/etoVPNMacBridge.app`
- `artifacts/macos-native/<runtime>/etoVPNPacketTunnel.appex`

### 7. Собрать desktop app bundle

Для Apple Silicon:

```bash
RUNTIME_IDENTIFIER=osx-arm64 ./deploy/client/publish-macos.sh
```

Для Intel:

```bash
RUNTIME_IDENTIFIER=osx-x64 ./deploy/client/publish-macos.sh
```

Ожидаемый результат:

- `artifacts/client-publish/<runtime>/etoVPN.app`

### 8. Запустить приложение

Для Apple Silicon:

```bash
open artifacts/client-publish/osx-arm64/etoVPN.app
```

Для Intel:

```bash
open artifacts/client-publish/osx-x64/etoVPN.app
```

Если macOS блокирует запуск:

```bash
xattr -dr com.apple.quarantine artifacts/client-publish/<runtime>/etoVPN.app
open artifacts/client-publish/<runtime>/etoVPN.app
```

### 9. Что проверить в приложении

Минимальный smoke:

1. Приложение открывается.
2. UI не падает сразу.
3. Можно:
   - либо импортировать legacy `.vpn/.conf`
   - либо залогиниться
4. `Connect` запускает tunnel lifecycle.
5. Есть реальный трафик, а не только надпись "подключено".
6. `Disconnect` реально рвет tunnel.
7. После повторного запуска приложение не ведет себя странно.

## Что попросить прислать обратно

Если все хорошо:

- архитектура Mac: `arm64` или `x86_64`
- получилось ли собрать
- получилось ли открыть app
- работает ли `Connect`
- работает ли `Disconnect`
- есть ли интернет через VPN

Если есть ошибка:

- полный вывод из `Terminal`
- скрин ошибки
- на каком шаге упало:
  - `build-native.sh`
  - `publish-macos.sh`
  - запуск `.app`
  - `Connect`

## Самые вероятные проблемы

### 1. Signing, provisioning или entitlement

Если Xcode ругается на:

- signing
- provisioning profile
- entitlement
- `NetworkExtension`

Это значит, что проблема в Apple signing context, а не в нашем скрипте.

### 2. Нет `XcodeGen`

`build-native.sh` остановится сразу и явно скажет, чего не хватает.

### 3. Нет `.NET 8 SDK`

Тогда упадет `publish-macos.sh`.

### 4. Архив неполный

Если вырезать `native/`, `deploy/` или `.research/amnezia-client/`, smoke на
Mac почти наверняка сломается.
