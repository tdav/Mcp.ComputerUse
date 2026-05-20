using System.ComponentModel;
using Mcp.ComputerUse.Core;
using Mcp.ComputerUse.Json;
using ModelContextProtocol.Server;

namespace Mcp.ComputerUse.Tools;

[McpServerToolType]
public sealed class KeyboardTools
{
    private readonly InputService _input;
    public KeyboardTools(InputService input) => _input = input;

    [McpServerTool, Description("Type Unicode text via KEYEVENTF_UNICODE — independent of keyboard layout. delay_ms throttles between characters.")]
    public OkResult TypeText(
        [Description("Text to type.")] string text,
        [Description("Delay in ms between characters. 0 = no delay.")] int delayMs = 0)
    {
        _input.TypeText(text, delayMs);
        return new OkResult();
    }

    [McpServerTool, Description("Press and release a single key. Examples: 'Enter', 'F4', 'a', 'Escape'.")]
    public OkResult KeyPress(string key)
    {
        _input.KeyPress(key);
        return new OkResult();
    }

    [McpServerTool, Description("Hold a key down for the specified duration in ms, then release.")]
    public OkResult KeyHold(string key, int ms)
    {
        _input.KeyHold(key, ms);
        return new OkResult();
    }

    [McpServerTool, Description("Press a chord such as 'ctrl+shift+esc'. All modifiers go down in order, then up in reverse order.")]
    public OkResult KeyHotkey([Description("Chord string with '+' separators.")] string keys)
    {
        _input.KeyHotkey(keys);
        return new OkResult();
    }

    [McpServerTool, Description("Pause for the specified number of milliseconds. Useful between UI actions to let animations settle.")]
    public OkResult Wait(int ms)
    {
        if (ms > 0) Thread.Sleep(ms);
        return new OkResult();
    }
}
