using System.ComponentModel;
using Mcp.ComputerUse.Core;
using Mcp.ComputerUse.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Mcp.ComputerUse.Tools;

[McpServerToolType]
public sealed class ScreenTools
{
    private readonly ScreenCaptureService _capture;
    private readonly ScalePlanCache _planCache;
    private readonly ScreenshotStorage _storage;
    private readonly ILogger<ScreenTools> _log;

    public ScreenTools(ScreenCaptureService capture, ScalePlanCache planCache, ScreenshotStorage storage, ILogger<ScreenTools> log)
    {
        _capture = capture;
        _planCache = planCache;
        _storage = storage;
        _log = log;
    }

    [McpServerTool, Description("Capture a screenshot of the specified monitor. Returns both an image block (PNG, base64) for vision and a text block with the ScalePlan metadata. The model should use the scaled coordinates returned here for subsequent mouse_* calls with coord_space='model'.")]
    public CallToolResult Screenshot(
        [Description("Monitor index from list_monitors. 0 is typically primary.")] int monitorIndex,
        [Description("Downscale to XGA/WXGA/FWXGA (default true). Set false for 1:1 pixel coords.")] bool downscale = true,
        [Description("Convert to grayscale to reduce size (default false).")] bool grayscale = false,
        [Description("Optional override directory to save the PNG. Defaults to current working directory or MCP_COMPUTERUSE_SCREENSHOTS_DIR.")] string? savePath = null)
    {
        _log.LogDebug("tool_call tool={Tool} monitor={Monitor} downscale={Downscale} grayscale={Grayscale}", nameof(Screenshot), monitorIndex, downscale, grayscale);
        var result = _capture.CaptureMonitor(monitorIndex, downscale, grayscale);
        _planCache.Set(monitorIndex, result.Plan);
        var saved = _storage.Save(result.PngBytes, monitorIndex, savePath);

        var meta = new ScreenshotMeta(
            MonitorIndex: monitorIndex,
            OrigWidth: result.OrigWidth,
            OrigHeight: result.OrigHeight,
            ScaledWidth: result.Plan.ScaledWidth,
            ScaledHeight: result.Plan.ScaledHeight,
            FactorX: result.Plan.FactorX,
            FactorY: result.Plan.FactorY,
            MonitorLeft: result.Plan.MonitorLeft,
            MonitorTop: result.Plan.MonitorTop,
            TargetName: result.Plan.TargetName,
            SavedTo: saved);

        return new CallToolResult
        {
            Content =
            [
                ImageContentBlock.FromBytes(result.PngBytes, "image/png"),
                new TextContentBlock { Text = System.Text.Json.JsonSerializer.Serialize(meta, McpJsonContext.Default.ScreenshotMeta) }
            ]
        };
    }
}
