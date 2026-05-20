# Mcp.ComputerUse

Windows computer-use MCP server built on .NET 10 with Native AOT compilation. Gives an LLM agent (Claude Code, Claude Desktop, any MCP client) full control over a Windows machine: monitor screenshots, mouse, keyboard, files, process launch, PowerShell.

One self-contained `.exe`, ~10 MB, no .NET runtime required. Vision-first: the model receives a downscaled PNG directly in its input, returns coordinates in the scaled pixel space, and the server maps them back to physical screen pixels.

- **Version:** v0.1.0
- **Platform:** Windows 10/11 x64
- **Transport:** stdio
- **MCP SDK:** ModelContextProtocol 1.3.0
- **Language:** [Русский README](README-ru.md)
- **Code license:** not set yet — add one as appropriate

---

## Features

16 MCP tools grouped by layer:

| Category | Tools | Purpose |
|---|---|---|
| Discovery | `ping`, `list_monitors` | Wire check, enumerate monitors with DPI |
| Vision | `screenshot` | Monitor capture with automatic downscale to WXGA, returned via MCP `image` content block |
| Mouse | `mouse_move`, `mouse_click`, `mouse_down`, `mouse_up`, `mouse_drag`, `mouse_scroll`, `cursor_position` | Full mouse action set with coordinate remap |
| Keyboard | `type_text`, `key_press`, `key_hold`, `key_hotkey`, `wait` | Unicode input, hotkeys, delays |
| Files | `read_file`, `write_file`, `create_folder` | File ops with UTF-8 / ASCII / UTF-16 / binary |
| Process | `launch_app`, `shell` | Launch via ShellExecute, PowerShell with timeout |

Full parameter reference for every tool is in [Tool reference](#tool-reference).

---

## Architecture

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

Key architectural decisions:

- **AOT cleanliness:** every DTO is registered in `McpJsonContext : JsonSerializerContext`, tools are registered explicitly via `.WithTools<T>()` (no reflection), no `WithToolsFromAssembly()`.
- **Per-Monitor V2 DPI:** via `app.manifest` plus a defensive `SetProcessDpiAwarenessContext` as the first statement in `Main`. Without this, coordinates drift on 150% / 200% scaled monitors.
- **Coordinate pipeline:** `screenshot` caches a `ScalePlan` (origin + factor) per monitor; every `mouse_*` defaults to model coordinates and reverses the mapping using the cache.
- **Multi-monitor mouse:** `SendInput` uses `MOUSEEVENTF_VIRTUALDESK | MOUSEEVENTF_ABSOLUTE` — without `VIRTUALDESK`, the mouse would only reach the primary monitor.
- **stderr-only logging:** `LogToStandardErrorThreshold = LogLevel.Trace`. Stdout is reserved for MCP framing.
- **Image delivery:** `ScreenTools.Screenshot` returns a `CallToolResult` with two content blocks — `image/png` (base64) plus a `text` block carrying `ScreenshotMeta` (origW, scaledW, factorX, monitorLeft). Claude Code feeds the image directly into the model's vision context.

---

## Repository layout

```
Mcp.ComputerUse/
├── Mcp.ComputerUse/                    # main project
│   ├── Mcp.ComputerUse.csproj          # net10.0-windows, PublishAot, IsAotCompatible
│   ├── app.manifest                    # Per-Monitor V2 DPI
│   ├── Program.cs                      # DI host, stdio transport, AppOptions
│   ├── AppOptions.cs                   # CLI flags
│   ├── Native/
│   │   ├── Win32.cs                    # [LibraryImport]/[DllImport] declarations
│   │   └── NativeTypes.cs              # RECT, MONITORINFOEX, INPUT, ...
│   ├── Core/
│   │   ├── MonitorRegistry.cs          # EnumDisplayMonitors + cache
│   │   ├── MonitorInfo.cs              # Rect, MonitorInfo records
│   │   ├── CoordinateMapper.cs         # ScalePlan, ModelToScreen, ScreenToModel
│   │   ├── ScalePlanCache.cs           # per-monitor plan
│   │   ├── ScreenCaptureService.cs     # BitBlt + ImageSharp PNG
│   │   ├── ScreenshotStorage.cs        # persist PNG to disk
│   │   ├── InputService.cs             # SendInput mouse + keyboard
│   │   ├── MouseButton.cs              # enum
│   │   ├── VirtualKeyMap.cs            # parses "ctrl+shift+esc"
│   │   ├── VisualFlash.cs              # stub overlay (v0.2)
│   │   └── FileService.cs              # read/write/exec/shell
│   ├── Tools/
│   │   ├── PingTools.cs
│   │   ├── MonitorTools.cs             # list_monitors
│   │   ├── ScreenTools.cs              # screenshot
│   │   ├── MouseTools.cs               # every mouse_*
│   │   ├── KeyboardTools.cs            # every key_* + wait
│   │   └── FileTools.cs                # read_file/write_file/launch_app/shell
│   └── Json/
│       └── McpJsonContext.cs           # JsonSerializerContext for AOT
├── Mcp.ComputerUse.Tests/              # xUnit + FluentAssertions
│   ├── SmokeTests.cs
│   ├── MonitorRegistryTests.cs
│   ├── CoordinateMapperTests.cs
│   ├── ScreenCaptureSmokeTests.cs      # integration (needs an interactive desktop)
│   └── FileServiceTests.cs
├── docs/
│   ├── superpowers/
│   │   ├── specs/2026-05-21-computer-use-mcp-design.md   # design spec
│   │   └── plans/2026-05-21-computer-use-mcp.md          # implementation plan
│   ├── Building a Windows Computer-Use MCP Server in C# with Native AOT.pdf
│   └── Разработка MCP-сервера на .NET 10 AOT.pdf
├── claude-mcp.example.json             # example Claude Code config
└── README.md
```

---

## Requirements

| | |
|---|---|
| **OS** | Windows 10 1903+ / Windows 11 |
| **.NET SDK** | 10.0+ |
| **MSVC Build Tools** | Only for AOT publish. The dev loop (`dotnet build` / `dotnet test`) does not need them. |

### One-time MSVC Build Tools install

```powershell
winget install Microsoft.VisualStudio.2022.BuildTools --override "--add Microsoft.VisualStudio.Workload.VCTools --includeRecommended"
```

Alternative: run Visual Studio Installer → select the "Desktop development with C++" workload.

### If AOT publish complains `'vswhere.exe' is not recognized`

`vswhere.exe` lives in `C:\Program Files (x86)\Microsoft Visual Studio\Installer\`, but is not on `PATH` by default. Fix for the current session:

```powershell
$env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"
```

Or launch a Developer PowerShell for VS — `PATH` is already configured there.

---

## Build

### Dev loop (no AOT, fast iteration)

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

Result: `Mcp.ComputerUse/bin/Release/net10.0-windows/win-x64/publish/mcp-computeruse.exe` (~10 MB).

---

## Connecting to Claude Code

### Option 1: via CLI

```powershell
claude mcp add computer-use "C:\Works\Mcp.ComputerUse\Mcp.ComputerUse\bin\Release\net10.0-windows\win-x64\publish\mcp-computeruse.exe"
```

### Option 2: manual config

Open the Claude Code config (`~/.config/claude/claude_desktop_config.json` or `%APPDATA%\Claude\claude_desktop_config.json`) and add:

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

Restart Claude Code. In a fresh session the `computer-use__*` tools will be available.

### With options

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

## CLI flags and env vars

| Flag | Env var | Default | Description |
|---|---|---|---|
| `--screenshots-dir <path>` | `MCP_COMPUTERUSE_SCREENSHOTS_DIR` | `Environment.CurrentDirectory` | Where to save screenshot PNGs. The file is also returned base64-encoded in the response — this is the audit copy. |
| `--scale-target <name>` | — | `wxga` | Default downscale target: `xga` (1024×768), `wxga` (1280×800), `fwxga` (1366×768), `none`. |
| `--default-monitor <n>` | — | `0` | Default monitor index (used by v0.2 — for now always passed explicitly). |
| `--no-flash` | — | enabled | Disables the visual flash overlay (in v0.1 this is a stub — no actual overlay drawn). |
| `--log-level <level>` | — | `Information` | `Trace` / `Debug` / `Information` / `Warning` / `Error` — written to stderr. |

---

## Tool reference

JSON parameters use snake_case across every tool. Returns are always inside the standard MCP `CallToolResult.content`.

### `ping`

Echo + timestamp. Wire check.

```jsonc
// args
{ "message": "hello" }
// result text block contains:
{ "message": "hello", "server_time_unix_ms": 1747834567890 }
```

### `list_monitors`

No arguments. Enumerates all monitors.

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

Returns: an `image` block (base64 PNG) plus a `text` block with metadata:

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

**Important:** after `screenshot`, the server caches the `ScalePlan` for that monitor. Every subsequent `mouse_*` with `coord_space="model"` uses it to reverse model coordinates back into physical pixels.

### Mouse tools

All accept `monitor_index`, `coord_space` (`"model"` default or `"screen"`), plus their own coordinates.

```jsonc
// mouse_move
{ "monitor_index": 0, "x": 683, "y": 384 }

// mouse_click
{ "monitor_index": 0, "x": 683, "y": 384, "button": "left", "clicks": 1 }
// button: "left" | "right" | "middle"
// clicks: 1 (single), 2 (double), 3 (triple)

// mouse_down / mouse_up — same fields, no clicks
// mouse_drag
{ "monitor_index": 0, "from_x": 100, "from_y": 100, "to_x": 500, "to_y": 500, "button": "left" }

// mouse_scroll
{ "monitor_index": 0, "x": 683, "y": 384, "clicks": 3, "direction": "vertical" }
// direction: "vertical" | "horizontal", clicks ±N (WHEEL_DELTA = 120 per click)

// cursor_position — returns cursor position in model coords for that monitor
{ "monitor_index": 0 }
// result: { "x": 683, "y": 384, "monitor_index": 0 }
```

`coord_space="screen"` skips the remap — x/y are treated as physical desktop pixels.

### Keyboard tools

```jsonc
// type_text — Unicode input via KEYEVENTF_UNICODE, layout-independent
{ "text": "Привет, мир!", "delay_ms": 0 }

// key_press — single key
{ "key": "Enter" }
// Supported names: Enter/Return, Tab, Backspace, Escape/Esc, Space,
// PgUp/PgDn, End, Home, Left/Right/Up/Down, Insert, Delete/Del,
// Win/LWin/RWin, Ctrl/Control, Shift, Alt, F1..F12,
// CapsLock, NumLock, ScrollLock, PrintScreen/PrtSc,
// single chars (a-z, 0-9, other symbols via VkKeyScanW)

// key_hold — press → wait → release
{ "key": "shift", "ms": 1000 }

// key_hotkey — chord
{ "keys": "ctrl+shift+esc" }
// modifiers go down in order, then up in reverse

// wait
{ "ms": 1500 }
```

### File tools

```jsonc
// read_file
{ "path": "C:\\tmp\\hi.txt", "encoding": "utf8" }
// encoding: "utf8" | "ascii" | "utf16" | "binary" (binary returns base64)
// result: { "content": "...", "encoding": "utf8" }

// write_file
{ "path": "C:\\tmp\\hi.txt", "content": "Hello", "encoding": "utf8", "overwrite": false }
// overwrite=false throws IOException if file exists

// create_folder
{ "path": "C:\\tmp\\agent" }

// launch_app — ShellExecute, honors PATH and file associations
{ "path": "notepad.exe", "args": "C:\\tmp\\hi.txt", "working_dir": null }
// result: { "pid": 12345 }

// shell — PowerShell with timeout
{ "command": "Get-Process | Where-Object Name -eq 'notepad'", "working_dir": null, "timeout_ms": 30000 }
// result: { "exit_code": 0, "stdout": "...", "stderr": "" }
```

---

## Coordinate system in detail

Vision models perform more accurately on images around 1280×800 pixels than on 4K. Anthropic explicitly recommends **doing the downscale inside the tool** rather than relying on their server-side resize (which lowers model accuracy).

So:

1. **On `screenshot`**: native 2560×1440 → downscale to FWXGA 1366×768 → sent to the model. The server stores `ScalePlan { orig=(2560,1440), scaled=(1366,768), factor=(0.534, 0.533), origin=(0,0) }`.
2. **The model** sees a 1366×768 image and says "click at (683, 384)".
3. **On `mouse_click(monitor_index=0, x=683, y=384, coord_space="model")`**:
   - `ModelToScreen` → `(round(683/0.534)+0, round(384/0.533)+0)` = `(1279, 720)` — a physical desktop pixel.
   - Normalize to the virtual desktop 0..65535 range: `(round(1279 * 65535 / (vsWidth-1)), ...)`.
   - `SendInput` with `MOVE | ABSOLUTE | VIRTUALDESK`, then `LEFTDOWN+LEFTUP`.

**Escape hatch:** `coord_space="screen"` skips step (1) and treats x/y as physical desktop pixels. Useful if you already have exact coordinates.

**If `mouse_*` is called without a prior `screenshot`** on that monitor, the server throws `InvalidOperationException: No ScalePlan cached for monitor N. Call screenshot first, or pass coord_space='screen'.`

---

## Testing

### Unit tests

```powershell
dotnet test Mcp.ComputerUse.Tests/Mcp.ComputerUse.Tests.csproj
```

v0.1.0 coverage:

- `CoordinateMapperTests` — model↔screen round-trip, edge cases, aspect-ratio target selection
- `MonitorRegistryTests` — real `EnumDisplayMonitors`
- `ScreenCaptureSmokeTests` — real `BitBlt` + downscale (integration, needs an interactive desktop)
- `FileServiceTests` — UTF-8 round-trip with Cyrillic, overwrite refusal, binary base64
- `SmokeTests` — Ping

Total: 12 tests.

### E2E checklist (inside Claude Code once the server is connected)

1. "List my monitors" → array matches your DPI setup
2. "Take a screenshot of monitor 0" → image appears **inline in chat**
3. Referencing the screenshot: "Click on the Start button" → cursor goes where it should
4. "Open Notepad, click center, type Hello, press Ctrl+S, type C:\\tmp\\hi.txt, press Enter" → end-to-end flow
5. "Read C:\\tmp\\hi.txt" → `Hello`
6. "Run powershell Get-Date" → output

---

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `'vswhere.exe' is not recognized` during `dotnet publish` | MSVC installer dir not on PATH | `$env:PATH = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer;$env:PATH"` |
| Clicks land ~2-4 px off on a DPI-scaled monitor | manifest not applied | Verify `app.manifest` is embedded (`dumpbin /headers` should show `application/dpiAware`). Republish. |
| `BitBlt` returns a black image for a specific window | HW-accelerated DWM composition (Chrome, Electron, some DRM) | Known GDI limitation. v0.2 will add a `Windows.Graphics.Capture` fallback. |
| Tool returns "No ScalePlan cached for monitor N" | called `mouse_*` without a prior `screenshot` for that monitor | Call `screenshot(monitor_index=N)` first, or pass `coord_space="screen"`. |
| MCP host doesn't see any tools | some log leaked to stdout, breaking JSON-RPC framing | Check no `Console.WriteLine` anywhere; all logs go through `ILogger` with `LogToStandardErrorThreshold=Trace`. |
| Mouse only reacts on the primary monitor | `SendInput.dwFlags` is missing `MOUSEEVENTF_VIRTUALDESK` | Check `InputService.MouseMoveScreen` — should be `MOVE \| ABSOLUTE \| VIRTUALDESK`. |
| `dotnet test` fails with "System.Threading.Lock not found" | language level too old | csproj must have `<TargetFramework>net10.0-windows</TargetFramework>` — the `Lock` type ships in .NET 9+. |
| ImageSharp NU1902/NU1903 warnings during build | pinned to 3.1.5 (last Apache-2.0 release) | Expected. 4.x requires a paid license. The CVEs are not exploitable in our pipeline — we never load third-party PNG/GIF data, only BitBlt buffers. |

### Logs

The server writes to stderr. Claude Code persists them (path depends on the version — usually `~/.claude/logs/` or `%APPDATA%\Claude\logs\`). For detailed debugging launch with `--log-level Debug` or `Trace`.

---

## Known limitations in v0.1

1. **`zoom` action** (computer_20251124) is not implemented — for Claude Opus 4.7, which supports 1:1 coordinates up to 2576px, the optimal workflow is not yet tuned. Use `downscale=false` for native resolution.
2. **`VisualFlash` is a stub.** It does not draw an overlay, only logs. The real layered window will land in v0.2.
3. **`Windows.Graphics.Capture` is absent** — for HW-accelerated windows BitBlt returns black. v0.2 will add a CsWinRT fallback.
4. **`shell` quoting** — a simple `Replace("\"", "\\\"")`. Won't handle complex command chains. Workaround: write commands without nested quotes.
5. **`read_file binary`** loads the whole file into memory and then into base64. For large binaries this is an OOM risk. Bounded by .NET's `System.IO` limits.
6. **MCP SDK 1.3.0** is pinned. Minor updates may rename content-block types (see the `project_mcp_sdk_api` memory note before bumping).

---

## Roadmap (v0.2)

- Real VisualFlash via `CreateWindowExW` + `SetLayeredWindowAttributes` (200 ms red frame at click site)
- `Windows.Graphics.Capture` fallback for DRM / HW-accelerated windows
- `zoom` tool — sub-region capture without downscale (for Opus 4.7)
- In-process MCP client for tests (no dependency on Claude Code)
- `<NoWarn>NU1902;NU1903</NoWarn>` in csproj with an explanatory comment
- Bounds-check in `MouseTools` using the injected `MonitorRegistry`
- Proper shell quoting via `-EncodedCommand <base64-UTF16LE>`
- UIA accessibility-tree snapshot as an optional tool (behind a flag)

---

## Design documents and history

- **Spec:** [docs/superpowers/specs/2026-05-21-computer-use-mcp-design.md](docs/superpowers/specs/2026-05-21-computer-use-mcp-design.md)
- **Implementation plan:** [docs/superpowers/plans/2026-05-21-computer-use-mcp.md](docs/superpowers/plans/2026-05-21-computer-use-mcp.md)
- **Research PDF:** [docs/Building a Windows Computer-Use MCP Server in C# with Native AOT.pdf](docs/Building%20a%20Windows%20Computer-Use%20MCP%20Server%20in%20C%23%20with%20Native%20AOT.pdf)
- **Git tag:** `v0.1.0` (`git log v0.1.0 --oneline` — 20 commits from bootstrap to harden)

---

## Security

This server grants the MCP client full control over your machine: synthetic input on your behalf, read/write of any file, launch of any process, arbitrary PowerShell. **Do not connect it to an untrusted LLM client.** The server runs with the privileges of the process that spawned it — typically your user account.

No sandboxing capabilities are provided (by design — the spec explicitly excludes them from v1). If you need isolation, use a virtual machine or container.

---

## Credits

- **Anthropic** — the `computer_20250124` action schema and downscale algorithm (claude-quickstarts/computer-use-demo)
- **CursorTouch/Windows-MCP** — the Windows tool taxonomy (Click/Type/Scroll/Shortcut/...)
- **modelcontextprotocol/csharp-sdk** — the official C# MCP SDK
- **SixLabors.ImageSharp 3.1.5** — fully managed PNG/codec layer (Apache-2.0)
