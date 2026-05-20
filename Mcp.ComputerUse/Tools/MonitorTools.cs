using System.ComponentModel;
using Mcp.ComputerUse.Core;
using Mcp.ComputerUse.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mcp.ComputerUse.Tools;

[McpServerToolType]
public sealed class MonitorTools
{
    private readonly MonitorRegistry _registry;
    private readonly ILogger<MonitorTools> _log;
    public MonitorTools(MonitorRegistry registry, ILogger<MonitorTools> log)
    {
        _registry = registry;
        _log = log;
    }

    [McpServerTool, Description("Enumerate connected monitors with bounds, work area, primary flag, and per-monitor DPI. Call this first to discover monitor indices.")]
    public ListMonitorsResult ListMonitors()
    {
        _log.LogDebug("tool_call tool={Tool}", nameof(ListMonitors));
        _registry.Refresh();
        var dtos = _registry.Monitors
            .Select(m => new MonitorDto(m.Index, m.DeviceName, m.Bounds, m.WorkArea, m.IsPrimary, m.DpiX, m.DpiY))
            .ToList();
        return new ListMonitorsResult(dtos);
    }
}
