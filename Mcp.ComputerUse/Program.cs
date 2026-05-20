using Mcp.ComputerUse.Core;
using Mcp.ComputerUse.Native;
using Mcp.ComputerUse.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Defensive: ensure PerMonitorV2 even before manifest is honored.
Win32.SetProcessDpiAwarenessContext(Win32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

var builder = Host.CreateApplicationBuilder(args);

// stdio MCP: stdout is reserved for the protocol. Logs must go to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services
    .AddSingleton<MonitorRegistry>()
    .AddSingleton<CoordinateMapper>()
    .AddSingleton<ScalePlanCache>()
    .AddSingleton<ScreenshotStorage>()
    .AddSingleton<ScreenCaptureService>()
    .AddSingleton<InputService>();

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<PingTools>()
    .WithTools<MonitorTools>()
    .WithTools<ScreenTools>()
    .WithTools<MouseTools>()
    .WithTools<KeyboardTools>();

await builder.Build().RunAsync();
