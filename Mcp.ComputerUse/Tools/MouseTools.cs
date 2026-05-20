using System.ComponentModel;
using Mcp.ComputerUse.Core;
using Mcp.ComputerUse.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Mcp.ComputerUse.Tools;

[McpServerToolType]
public sealed class MouseTools
{
    private readonly InputService _input;
    private readonly ScalePlanCache _planCache;
    private readonly CoordinateMapper _mapper;
    private readonly MonitorRegistry _registry;
    private readonly ILogger<MouseTools> _log;

    public MouseTools(InputService input, ScalePlanCache planCache, CoordinateMapper mapper, MonitorRegistry registry, ILogger<MouseTools> log)
    {
        _input = input;
        _planCache = planCache;
        _mapper = mapper;
        _registry = registry;
        _log = log;
    }

    [McpServerTool, Description("Move the cursor to (x,y). coord_space='model' uses the scaled coords from the last screenshot of this monitor (default). coord_space='screen' uses physical desktop pixels.")]
    public OkResult MouseMove(
        [Description("Monitor index from list_monitors.")] int monitorIndex,
        [Description("X coordinate")] int x,
        [Description("Y coordinate")] int y,
        [Description("'model' (default) or 'screen'.")] string coordSpace = "model")
    {
        _log.LogDebug("tool_call tool={Tool} monitor={Monitor} x={X} y={Y} space={Space}", nameof(MouseMove), monitorIndex, x, y, coordSpace);
        var (sx, sy) = ToScreen(monitorIndex, x, y, coordSpace);
        _input.MouseMoveScreen(sx, sy);
        return new OkResult();
    }

    [McpServerTool, Description("Click at (x,y). button: left|right|middle. clicks: 1 (single), 2 (double), 3 (triple).")]
    public OkResult MouseClick(int monitorIndex, int x, int y, string button = "left", int clicks = 1, string coordSpace = "model")
    {
        _log.LogDebug("tool_call tool={Tool} monitor={Monitor} x={X} y={Y} button={Button} clicks={Clicks} space={Space}", nameof(MouseClick), monitorIndex, x, y, button, clicks, coordSpace);
        var (sx, sy) = ToScreen(monitorIndex, x, y, coordSpace);
        _input.MouseClickScreen(sx, sy, ParseButton(button), clicks);
        return new OkResult();
    }

    [McpServerTool, Description("Press a mouse button without releasing.")]
    public OkResult MouseDown(int monitorIndex, int x, int y, string button = "left", string coordSpace = "model")
    {
        _log.LogDebug("tool_call tool={Tool} monitor={Monitor} button={Button}", nameof(MouseDown), monitorIndex, button);
        var (sx, sy) = ToScreen(monitorIndex, x, y, coordSpace);
        _input.MouseDownScreen(sx, sy, ParseButton(button));
        return new OkResult();
    }

    [McpServerTool, Description("Release a previously pressed mouse button.")]
    public OkResult MouseUp(int monitorIndex, int x, int y, string button = "left", string coordSpace = "model")
    {
        _log.LogDebug("tool_call tool={Tool} monitor={Monitor} button={Button}", nameof(MouseUp), monitorIndex, button);
        var (sx, sy) = ToScreen(monitorIndex, x, y, coordSpace);
        _input.MouseUpScreen(sx, sy, ParseButton(button));
        return new OkResult();
    }

    [McpServerTool, Description("Drag with the specified button held from (fromX, fromY) to (toX, toY).")]
    public OkResult MouseDrag(int monitorIndex, int fromX, int fromY, int toX, int toY, string button = "left", string coordSpace = "model")
    {
        _log.LogDebug("tool_call tool={Tool} monitor={Monitor} button={Button}", nameof(MouseDrag), monitorIndex, button);
        var (sx1, sy1) = ToScreen(monitorIndex, fromX, fromY, coordSpace);
        var (sx2, sy2) = ToScreen(monitorIndex, toX, toY, coordSpace);
        _input.MouseDragScreen(sx1, sy1, sx2, sy2, ParseButton(button));
        return new OkResult();
    }

    [McpServerTool, Description("Scroll the wheel by N clicks (positive = up/right, negative = down/left). direction: vertical|horizontal.")]
    public OkResult MouseScroll(int monitorIndex, int x, int y, int clicks, string direction = "vertical", string coordSpace = "model")
    {
        _log.LogDebug("tool_call tool={Tool} monitor={Monitor} clicks={Clicks} dir={Direction}", nameof(MouseScroll), monitorIndex, clicks, direction);
        var (sx, sy) = ToScreen(monitorIndex, x, y, coordSpace);
        _input.MouseScrollScreen(sx, sy, clicks, horizontal: direction.Equals("horizontal", StringComparison.OrdinalIgnoreCase));
        return new OkResult();
    }

    [McpServerTool, Description("Return current cursor position in MODEL coordinates for the given monitor (using its last ScalePlan). If no screenshot has been taken, returns screen-coords.")]
    public CursorPositionResult CursorPosition(int monitorIndex)
    {
        _log.LogDebug("tool_call tool={Tool} monitor={Monitor}", nameof(CursorPosition), monitorIndex);
        var (sx, sy) = _input.GetCursorPos();
        var plan = _planCache.Get(monitorIndex);
        if (plan is null) return new CursorPositionResult(sx, sy, monitorIndex);
        var (mx, my) = _mapper.ScreenToModel(plan.Value, sx, sy);
        return new CursorPositionResult(mx, my, monitorIndex);
    }

    private (int sx, int sy) ToScreen(int monitorIndex, int x, int y, string coordSpace)
    {
        if (coordSpace.Equals("screen", StringComparison.OrdinalIgnoreCase))
            return (x, y);

        var plan = _planCache.Get(monitorIndex)
            ?? throw new InvalidOperationException(
                $"No ScalePlan cached for monitor {monitorIndex}. Call screenshot first, or pass coord_space='screen'.");
        return _mapper.ModelToScreen(plan, x, y);
    }

    private static MouseButton ParseButton(string s) => s.ToLowerInvariant() switch
    {
        "left" => MouseButton.Left,
        "right" => MouseButton.Right,
        "middle" => MouseButton.Middle,
        _ => throw new ArgumentException($"Unknown button '{s}'.", nameof(s)),
    };
}
