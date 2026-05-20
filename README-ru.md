# Mcp.ComputerUse

Windows computer-use MCP-сервер на .NET 10 с компиляцией в Native AOT. Даёт LLM-агенту (Claude Code, Claude Desktop, любой MCP-клиент) полный контроль над Windows-машиной: скриншоты мониторов, мышь, клавиатура, файлы, запуск процессов, PowerShell.

Один self-contained `.exe` ~10 MB, без рантайма .NET. Vision-first: модель получает PNG скриншота прямо во входе, отдаёт координаты в downscaled пиксельной системе, сервер маппит их обратно на физические пиксели экрана.

- **Версия:** v0.1.0
- **Платформа:** Windows 10/11 x64
- **Транспорт:** stdio
- **MCP SDK:** ModelContextProtocol 1.3.0
- **Лицензия кода:** см. CLAUDE.md / отсутствует — добавь по своему усмотрению

---

## Возможности

16 MCP-инструментов, сгруппированных по слоям:

| Категория | Инструменты | Назначение |
|---|---|---|
| Discovery | `ping`, `list_monitors` | Проверка связи, перечисление мониторов с DPI |
| Vision | `screenshot` | Снимок монитора с автоматическим downscale до WXGA, возврат через `image`-блок MCP-протокола |
| Mouse | `mouse_move`, `mouse_click`, `mouse_down`, `mouse_up`, `mouse_drag`, `mouse_scroll`, `cursor_position` | Полный набор действий мышью с координатным remap |
| Keyboard | `type_text`, `key_press`, `key_hold`, `key_hotkey`, `wait` | Юникод-ввод, хоткеи, паузы |
| Files | `read_file`, `write_file`, `create_folder` | Файловые операции, UTF-8/ASCII/UTF-16/binary |
| Process | `launch_app`, `shell` | Запуск программ через ShellExecute, PowerShell с таймаутом |

Полный список параметров каждого tool — в разделе [Tool reference](#tool-reference).

---

## Архитектура

```
┌─────────────── Claude Code / MCP client ───────────────┐
│  Tool call: screenshot(monitor_index=1)                │
└──────────────────────┬─────────────────────────────────┘
                       │  JSON-RPC over stdio
                       ▼
┌──────────────── mcp-computeruse.exe ───────────────────┐
│  Program.cs        — Host.CreateApplicationBuilder      │
│  AppOptions        — CLI parsing                        │
│  Tools/            — ScreenTools, MouseTools, ...       │
│  Core/             — MonitorRegistry, CoordinateMapper, │
│                      ScreenCaptureService, InputService,│
│                      FileService, ScalePlanCache        │
│  Native/Win32.cs   — [LibraryImport] / [DllImport]      │
│  Json/             — JsonSerializerContext (AOT)        │
└──────────────────────┬─────────────────────────────────┘
                       │  Win32 P/Invoke
                       ▼
        ┌──── user32 / gdi32 / shcore ────┐
        │  EnumDisplayMonitors            │
        │  BitBlt + GetDIBits             │
        │  SendInput (mouse/keyboard)     │
        │  GetDpiForMonitor               │
        └─────────────────────────────────┘
```

Ключевые архитектурные решения:

- **AOT-чистота:** все DTO зарегистрированы в `McpJsonContext : JsonSerializerContext`, инструменты регистрируются явно через `.WithTools<T>()` (без рефлексии), никакого `WithToolsFromAssembly()`.
- **Per-Monitor V2 DPI:** через `app.manifest` + защитный `SetProcessDpiAwarenessContext` первой строкой Main. Без этого на 150%/200% scaled мониторах координаты «уплывают».
- **Координатный pipeline:** `screenshot` запоминает `ScalePlan` для монитора (origin + factor), все `mouse_*` по умолчанию работают в model-координатах и реверсятся через кэш.
- **Multi-monitor mouse:** `SendInput` с `MOUSEEVENTF_VIRTUALDESK | MOUSEEVENTF_ABSOLUTE` — без `VIRTUALDESK` мышь работала бы только на основном мониторе.
- **stderr-only логи:** `LogToStandardErrorThreshold = LogLevel.Trace`. Stdout зарезервирован под MCP-фрейминг.
- **Image delivery:** `ScreenTools.Screenshot` возвращает `CallToolResult` с двумя content-блоками — `image/png` (base64) + `text` с `ScreenshotMeta` (origW, scaledW, factorX, monitorLeft). Claude Code сам кладёт картинку во vision-контекст модели.

---

## Структура репозитория

```
Mcp.ComputerUse/
├── Mcp.ComputerUse/                    # основной проект
│   ├── Mcp.ComputerUse.csproj          # net10.0-windows, PublishAot, IsAotCompatible
│   ├── app.manifest                    # Per-Monitor V2 DPI
│   ├── Program.cs                      # DI host, stdio transport, AppOptions
│   ├── AppOptions.cs                   # CLI-флаги
│   ├── Native/
│   │   ├── Win32.cs                    # [LibraryImport]/[DllImport] декларации
│   │   └── NativeTypes.cs              # RECT, MONITORINFOEX, INPUT, ...
│   ├── Core/
│   │   ├── MonitorRegistry.cs          # EnumDisplayMonitors + кэш
│   │   ├── MonitorInfo.cs              # Rect, MonitorInfo records
│   │   ├── CoordinateMapper.cs         # ScalePlan, ModelToScreen, ScreenToModel
│   │   ├── ScalePlanCache.cs           # per-monitor план
│   │   ├── ScreenCaptureService.cs     # BitBlt + ImageSharp PNG
│   │   ├── ScreenshotStorage.cs        # сохранение PNG на диск
│   │   ├── InputService.cs             # SendInput mouse + keyboard
│   │   ├── MouseButton.cs              # enum
│   │   ├── VirtualKeyMap.cs            # парсинг "ctrl+shift+esc"
│   │   ├── VisualFlash.cs              # stub overlay (v0.2)
│   │   └── FileService.cs              # read/write/exec/shell
│   ├── Tools/
│   │   ├── PingTools.cs
│   │   ├── MonitorTools.cs             # list_monitors
│   │   ├── ScreenTools.cs              # screenshot
│   │   ├── MouseTools.cs               # все mouse_*
│   │   ├── KeyboardTools.cs            # все key_* + wait
│   │   └── FileTools.cs                # read_file/write_file/launch_app/shell
│   └── Json/
│       └── McpJsonContext.cs           # JsonSerializerContext для AOT
├── Mcp.ComputerUse.Tests/              # xUnit + FluentAssertions
│   ├── SmokeTests.cs
│   ├── MonitorRegistryTests.cs
│   ├── CoordinateMapperTests.cs
│   ├── ScreenCaptureSmokeTests.cs      # integration (нужен интерактивный desktop)
│   └── FileServiceTests.cs
├── docs/
│   ├── superpowers/
│   │   ├── specs/2026-05-21-computer-use-mcp-design.md   # design spec
│   │   └── plans/2026-05-21-computer-use-mcp.md          # implementation plan
│   ├── Building a Windows Computer-Use MCP Server in C# with Native AOT.pdf
│   └── Разработка MCP-сервера на .NET 10 AOT.pdf
├── claude-mcp.example.json             # пример конфига для Claude Code
└── README.md
```

---

## Требования

| | |
|---|---|
| **OS** | Windows 10 1903+ / Windows 11 |
| **.NET SDK** | 10.0+ |
| **MSVC Build Tools** | Только для AOT-публикации. Для dev-цикла (`dotnet build`/`dotnet test`) не нужны. |

### Установка MSVC Build Tools (один раз)

```powershell
winget install Microsoft.VisualStudio.2022.BuildTools --override "--add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
```

Альтернатива: запустить Visual Studio Installer → выбрать workload «Desktop development with C++».

### Если AOT publish жалуется на `'vswhere.exe' is not recognized`

`vswhere.exe` лежит в `C:\Program Files (x86)\Microsoft Visual Studio\Installer\`, но не в `PATH` по умолчанию. Лечится в текущей сессии:

```powershell
$env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"
```

Или из Developer PowerShell for VS — там `PATH` уже корректный.

---

## Сборка

### Dev-цикл (без AOT, быстрая итерация)

```powershell
cd C:\Works\Mcp.ComputerUse
dotnet build Mcp.ComputerUse/Mcp.ComputerUse.csproj -c Release
dotnet test
```

### Production AOT publish

```powershell
$env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"
dotnet publish Mcp.ComputerUse/Mcp.ComputerUse.csproj -c Release -r win-x64
```

Результат: `Mcp.ComputerUse/bin/Release/net10.0-windows/win-x64/publish/mcp-computeruse.exe` (~10 MB).

---

## Подключение к Claude Code

### Способ 1: через CLI

```powershell
claude mcp add computer-use "C:\Works\Mcp.ComputerUse\Mcp.ComputerUse\bin\Release\net10.0-windows\win-x64\publish\mcp-computeruse.exe"
```

### Способ 2: вручную через config

Открой конфиг Claude Code (`~/.config/claude/claude_desktop_config.json` или `%APPDATA%\Claude\claude_desktop_config.json`) и добавь:

```json
{
  "mcpServers": {
    "computer-use": {
      "command": "C:\\Works\\Mcp.ComputerUse\\Mcp.ComputerUse\\bin\\Release\\net10.0-windows\\win-x64\\publish\\mcp-computeruse.exe",
      "args": []
    }
  }
}
```

Перезапусти Claude Code. В новой сессии инструменты `computer-use__*` станут доступны.

### С опциями

```json
{
  "mcpServers": {
    "computer-use": {
      "command": "...\\mcp-computeruse.exe",
      "args": [
        "--screenshots-dir", "C:\\Users\\me\\Pictures\\agent-screens",
        "--scale-target", "wxga",
        "--default-monitor", "0",
        "--log-level", "debug"
      ]
    }
  }
}
```

---

## CLI-флаги и env-переменные

| Флаг | Env var | Default | Описание |
|---|---|---|---|
| `--screenshots-dir <path>` | `MCP_COMPUTERUSE_SCREENSHOTS_DIR` | `Environment.CurrentDirectory` | Куда сохранять PNG скриншотов. Файл всё равно отдаётся base64 в ответе — это аудит-копия. |
| `--scale-target <name>` | — | `wxga` | Дефолтная цель downscale: `xga` (1024×768), `wxga` (1280×800), `fwxga` (1366×768), `none`. |
| `--default-monitor <n>` | — | `0` | Монитор по умолчанию (используется в v0.2 — сейчас всегда передаётся явно). |
| `--no-flash` | — | enabled | Выключает визуальный flash overlay (в v0.1 это stub, без визуала). |
| `--log-level <level>` | — | `Information` | `Trace`/`Debug`/`Information`/`Warning`/`Error` — пишется в stderr. |

---

## Tool reference

JSON-параметры всех инструментов в snake_case. Возврат всегда внутри стандартного MCP `CallToolResult.content`.

### `ping`

Echo + timestamp. Проверка связи.

```jsonc
// args
{ "message": "hello" }
// result text-block содержит:
{ "message": "hello", "server_time_unix_ms": 1747834567890 }
```

### `list_monitors`

Без аргументов. Перечисляет все мониторы.

```jsonc
// result
{
  "monitors": [
    {
      "index": 0,
      "device_name": "\\\\.\\DISPLAY1",
      "bounds":    { "x": 0,    "y": 0, "width": 2560, "height": 1440 },
      "work_area": { "x": 0,    "y": 0, "width": 2560, "height": 1400 },
      "is_primary": true,
      "dpi_x": 144, "dpi_y": 144
    },
    {
      "index": 1,
      "device_name": "\\\\.\\DISPLAY2",
      "bounds":    { "x": 2560, "y": 0, "width": 1920, "height": 1080 },
      ...
    }
  ]
}
```

### `screenshot`

```jsonc
{
  "monitor_index": 0,
  "downscale": true,           // default true
  "grayscale": false,          // default false
  "save_path": null            // optional override
}
```

Возврат: `image`-блок (base64 PNG) + `text`-блок с метаданными:

```jsonc
{
  "monitor_index": 0,
  "orig_width": 2560, "orig_height": 1440,
  "scaled_width": 1366, "scaled_height": 768,
  "factor_x": 0.534,  "factor_y": 0.533,
  "monitor_left": 0,  "monitor_top": 0,
  "target_name": "FWXGA",
  "saved_to": "C:\\Users\\me\\screenshot-mon0-20260521-102345-678.png"
}
```

**Важно:** после `screenshot` сервер запоминает `ScalePlan` для этого монитора. Все последующие `mouse_*` с `coord_space="model"` используют его, чтобы развернуть model-координаты обратно в физические пиксели.

### Mouse tools

Все принимают `monitor_index`, `coord_space` (`"model"` default или `"screen"`), плюс свои координаты.

```jsonc
// mouse_move
{ "monitor_index": 0, "x": 683, "y": 384 }

// mouse_click
{ "monitor_index": 0, "x": 683, "y": 384, "button": "left", "clicks": 1 }
// button: "left" | "right" | "middle"
// clicks: 1 (single), 2 (double), 3 (triple)

// mouse_down / mouse_up — те же поля, без clicks
// mouse_drag
{ "monitor_index": 0, "from_x": 100, "from_y": 100, "to_x": 500, "to_y": 500, "button": "left" }

// mouse_scroll
{ "monitor_index": 0, "x": 683, "y": 384, "clicks": 3, "direction": "vertical" }
// direction: "vertical" | "horizontal", clicks ±N (WHEEL_DELTA = 120 per click)

// cursor_position — возвращает позицию в model-координатах для этого монитора
{ "monitor_index": 0 }
// result: { "x": 683, "y": 384, "monitor_index": 0 }
```

`coord_space="screen"` пропускает remap — координаты трактуются как физические пиксели всего desktop.

### Keyboard tools

```jsonc
// type_text — Unicode-ввод через KEYEVENTF_UNICODE, не зависит от раскладки
{ "text": "Привет, мир!", "delay_ms": 0 }

// key_press — одиночное нажатие
{ "key": "Enter" }
// Поддержанные имена: Enter/Return, Tab, Backspace, Escape/Esc, Space,
// PgUp/PgDn, End, Home, Left/Right/Up/Down, Insert, Delete/Del,
// Win/LWin/RWin, Ctrl/Control, Shift, Alt, F1..F12,
// CapsLock, NumLock, ScrollLock, PrintScreen/PrtSc,
// одиночные символы (a-z, 0-9, символы через VkKeyScanW)

// key_hold — нажать → ждать → отпустить
{ "key": "shift", "ms": 1000 }

// key_hotkey — комбо
{ "keys": "ctrl+shift+esc" }
// модификаторы нажимаются по порядку, отпускаются в обратном

// wait
{ "ms": 1500 }
```

### File tools

```jsonc
// read_file
{ "path": "C:\\tmp\\hi.txt", "encoding": "utf8" }
// encoding: "utf8" | "ascii" | "utf16" | "binary" (binary возвращает base64)
// result: { "content": "...", "encoding": "utf8" }

// write_file
{ "path": "C:\\tmp\\hi.txt", "content": "Hello", "encoding": "utf8", "overwrite": false }
// overwrite=false бросит IOException если файл существует

// create_folder
{ "path": "C:\\tmp\\agent" }

// launch_app — ShellExecute, поддерживает PATH и file associations
{ "path": "notepad.exe", "args": "C:\\tmp\\hi.txt", "working_dir": null }
// result: { "pid": 12345 }

// shell — PowerShell с таймаутом
{ "command": "Get-Process | Where-Object Name -eq 'notepad'", "working_dir": null, "timeout_ms": 30000 }
// result: { "exit_code": 0, "stdout": "...", "stderr": "" }
```

---

## Координатная система — детально

Модели зрения работают эффективнее на изображениях ~1280×800 пикселей, чем на 4K. Anthropic явно рекомендует **делать downscale в инструменте**, а не полагаться на их server-side resize (последнее снижает точность модели).

Поэтому:

1. **При `screenshot`**: native 2560×1440 → downscale до FWXGA 1366×768 → отправка модели. Сохраняется `ScalePlan { orig=(2560,1440), scaled=(1366,768), factor=(0.534, 0.533), origin=(0,0) }`.
2. **Модель** видит картинку 1366×768 и говорит: «кликни в (683, 384)».
3. **При `mouse_click(monitor_index=0, x=683, y=384, coord_space="model")`**:
   - `ModelToScreen` → `(round(683/0.534)+0, round(384/0.533)+0)` = `(1279, 720)` — физический пиксель экрана.
   - Нормализация в virtual desktop 0..65535: `(round(1279 * 65535 / (vsWidth-1)), ...)`.
   - `SendInput` с `MOVE | ABSOLUTE | VIRTUALDESK`, потом `LEFTDOWN+LEFTUP`.

**Escape hatch:** `coord_space="screen"` пропускает шаг (1) и трактует x/y как физические пиксели desktop. Полезно, если у тебя уже есть точные координаты.

**Если `mouse_*` зовётся без предварительного `screenshot`** на этот монитор — сервер бросит ошибку `InvalidOperationException: No ScalePlan cached for monitor N. Call screenshot first, or pass coord_space='screen'.`

---

## Тестирование

### Юнит-тесты

```powershell
dotnet test Mcp.ComputerUse.Tests/Mcp.ComputerUse.Tests.csproj
```

Покрытие v0.1.0:

- `CoordinateMapperTests` — round-trip model↔screen, граничные случаи, выбор aspect-ratio target
- `MonitorRegistryTests` — реальный `EnumDisplayMonitors`
- `ScreenCaptureSmokeTests` — реальный `BitBlt` + downscale (integration, нужен интерактивный desktop)
- `FileServiceTests` — UTF-8 round-trip с кириллицей, отказ overwrite, binary base64
- `SmokeTests` — Ping

Итого: 12 тестов.

### E2E чек-лист (в Claude Code после подключения)

1. «List my monitors» → массив с DPI совпадает с твоей настройкой
2. «Take a screenshot of monitor 0» → картинка появляется **прямо в чате**
3. Ссылаясь на скриншот: «Click on the Start button» → курсор летит куда нужно
4. «Open Notepad, click center, type Привет, press Ctrl+S, type C:\\tmp\\hi.txt, press Enter» → сквозной сценарий
5. «Read C:\\tmp\\hi.txt» → `Привет`
6. «Run powershell Get-Date» → результат

---

## Troubleshooting

| Симптом | Причина | Решение |
|---|---|---|
| `'vswhere.exe' is not recognized` при `dotnet publish` | MSVC installer не в PATH | `$env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"` |
| Клики падают мимо на ~2-4 px на DPI-scaled мониторе | manifest не применён | Проверь, что `app.manifest` встроен в exe (`dumpbin /headers` должен показать `application/dpiAware`). Перепубликуй. |
| `BitBlt` возвращает чёрный экран для конкретного окна | HW-accelerated DWM-композиция (Chrome, Electron, некоторые DRM) | Известное ограничение GDI. В v0.2 будет fallback на `Windows.Graphics.Capture`. |
| Tool возвращает «No ScalePlan cached for monitor N» | вызвали `mouse_*` без предварительного `screenshot` этого монитора | сделай `screenshot(monitor_index=N)` сначала, или передай `coord_space="screen"` |
| MCP-host не видит инструменты | где-то лог уехал в stdout, ломая JSON-RPC framing | проверь, что нет `Console.WriteLine` ни в одном файле; все логи через `ILogger`, `LogToStandardErrorThreshold=Trace` |
| Мышь работает только на основном мониторе | в `SendInput.dwFlags` нет `MOUSEEVENTF_VIRTUALDESK` | проверь `InputService.MouseMoveScreen` — должно быть `MOVE \| ABSOLUTE \| VIRTUALDESK` |
| `dotnet test` падает с «System.Threading.Lock not found» | старый язык-уровень | csproj должен иметь `<TargetFramework>net10.0-windows</TargetFramework>` — `Lock` тип появился в .NET 9 |
| ImageSharp warnings NU1902/NU1903 при сборке | пин на 3.1.5 (последняя Apache-2.0 версия) | ожидаемо. 4.x требует платную лицензию. CVE не эксплуатируемые в нашем pipeline (мы не загружаем сторонние PNG/GIF — только BitBlt-буфер). |

### Логи

Сервер пишет в stderr. Claude Code сохраняет их (зависит от версии — обычно `~/.claude/logs/` или `%APPDATA%\Claude\logs\`). Для подробной отладки запускай с `--log-level Debug` или `Trace`.

---

## Известные ограничения v0.1

1. **`zoom` action** (computer_20251124) не реализован — для Claude Opus 4.7, который поддерживает 1:1 координаты до 2576px, оптимальный workflow ещё не оптимизирован. Используй `downscale=false` для нативного разрешения.
2. **`VisualFlash` — stub.** Не рисует overlay, только логирует. Реальный layered window — в v0.2.
3. **`Windows.Graphics.Capture` отсутствует** — для HW-accelerated окон BitBlt вернёт чёрный. v0.2 добавит fallback через CsWinRT.
4. **`shell` quoting** — простой `Replace("\"", "\\\"")`. Не справится со сложными цепочками. Workaround: пиши команды без вложенных кавычек.
5. **`read_file binary`** грузит файл целиком в память + base64. Для больших бинарей это OOM-риск. Лимитировано System.IO лимитами .NET.
6. **MCP SDK 1.3.0** — pin. Минорные апдейты могут менять имена content-блоков (см. memory `project_mcp_sdk_api.md` если поднимаешь версию).

---

## Roadmap (v0.2)

- Реальный VisualFlash через `CreateWindowExW` + `SetLayeredWindowAttributes` (200 ms красная рамка на месте клика)
- `Windows.Graphics.Capture` fallback для DRM/HW-accelerated окон
- `zoom` tool — sub-region capture без downscale (для Opus 4.7)
- In-process MCP-клиент в тестах (без зависимости от Claude Code)
- `<NoWarn>NU1902;NU1903</NoWarn>` в csproj с комментарием-обоснованием
- Bounds-check в `MouseTools` через injected `MonitorRegistry`
- Нормальное shell-quoting через `-EncodedCommand <base64-UTF16LE>`
- UIA accessibility-tree снапшот как опциональный tool (за флагом)

---

## Дизайн-документы и история

- **Спека:** [docs/superpowers/specs/2026-05-21-computer-use-mcp-design.md](docs/superpowers/specs/2026-05-21-computer-use-mcp-design.md)
- **План реализации:** [docs/superpowers/plans/2026-05-21-computer-use-mcp.md](docs/superpowers/plans/2026-05-21-computer-use-mcp.md)
- **Research PDF:** [docs/Building a Windows Computer-Use MCP Server in C# with Native AOT.pdf](docs/Building%20a%20Windows%20Computer-Use%20MCP%20Server%20in%20C%23%20with%20Native%20AOT.pdf)
- **Git tag:** `v0.1.0` (`git log v0.1.0 --oneline` — 20 коммитов от bootstrap до harden)

---

## Безопасность

Этот сервер даёт MCP-клиенту полный контроль над твоей машиной: ввод от лица пользователя, чтение/запись любых файлов, запуск любых процессов, выполнение PowerShell. **НЕ подключай его к ненадёжному LLM-клиенту.** Сервер запускается с правами того процесса, который его spawn'ит — обычно это твой пользователь.

Sandbox-возможностей нет (по дизайну — спека явно исключает их из v1). Если нужна изоляция — используй виртуальную машину или контейнер.

---

## Credits

- **Anthropic** — Action schema `computer_20250124`, downscale алгоритм (claude-quickstarts/computer-use-demo)
- **CursorTouch/Windows-MCP** — таксономия Windows-инструментов (Click/Type/Scroll/Shortcut...)
- **modelcontextprotocol/csharp-sdk** — официальный C# SDK
- **SixLabors.ImageSharp 3.1.5** — fully managed PNG/codec layer (Apache-2.0)
