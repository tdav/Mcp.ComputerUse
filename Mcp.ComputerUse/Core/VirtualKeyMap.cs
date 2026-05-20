namespace Mcp.ComputerUse.Core;

public static class VirtualKeyMap
{
    private static readonly Dictionary<string, ushort> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["enter"] = 0x0D, ["return"] = 0x0D,
        ["tab"] = 0x09, ["backspace"] = 0x08, ["escape"] = 0x1B, ["esc"] = 0x1B,
        ["space"] = 0x20, ["pgup"] = 0x21, ["pgdn"] = 0x22, ["end"] = 0x23, ["home"] = 0x24,
        ["left"] = 0x25, ["up"] = 0x26, ["right"] = 0x27, ["down"] = 0x28,
        ["insert"] = 0x2D, ["delete"] = 0x2E, ["del"] = 0x2E,
        ["win"] = 0x5B, ["lwin"] = 0x5B, ["rwin"] = 0x5C,
        ["ctrl"] = 0x11, ["control"] = 0x11, ["shift"] = 0x10, ["alt"] = 0x12,
        ["f1"] = 0x70, ["f2"] = 0x71, ["f3"] = 0x72, ["f4"] = 0x73, ["f5"] = 0x74, ["f6"] = 0x75,
        ["f7"] = 0x76, ["f8"] = 0x77, ["f9"] = 0x78, ["f10"] = 0x79, ["f11"] = 0x7A, ["f12"] = 0x7B,
        ["capslock"] = 0x14, ["numlock"] = 0x90, ["scrolllock"] = 0x91,
        ["printscreen"] = 0x2C, ["prtsc"] = 0x2C,
    };

    public static ushort Resolve(string key)
    {
        if (Map.TryGetValue(key, out var vk)) return vk;
        if (key.Length == 1)
        {
            char ch = char.ToUpperInvariant(key[0]);
            if (ch is (>= '0' and <= '9') or (>= 'A' and <= 'Z')) return ch;
            // Fall through to VkKeyScanW for layout-dependent symbols
        }
        if (key.Length == 1)
        {
            ushort scan = Native.Win32.VkKeyScanW(key[0]);
            return (ushort)(scan & 0xFF);
        }
        throw new ArgumentException($"Unknown key name: '{key}'.", nameof(key));
    }

    public static ushort[] ParseChord(string chord)
    {
        // "ctrl+shift+esc"
        return chord.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(Resolve)
                    .ToArray();
    }
}
