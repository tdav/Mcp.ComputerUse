using System.Runtime.InteropServices;
using Mcp.ComputerUse.Native;

namespace Mcp.ComputerUse.Core;

public sealed class InputService
{
    public void MouseMoveScreen(int sx, int sy)
    {
        var (nx, ny) = NormalizeToVirtualDesktop(sx, sy);
        var inputs = new[]
        {
            new INPUT
            {
                type = Win32.INPUT_MOUSE,
                U = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dx = nx, dy = ny,
                        dwFlags = Win32.MOUSEEVENTF_MOVE | Win32.MOUSEEVENTF_ABSOLUTE | Win32.MOUSEEVENTF_VIRTUALDESK,
                    }
                }
            }
        };
        SendOrThrow(inputs);
    }

    public void MouseClickScreen(int sx, int sy, MouseButton button, int clicks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(clicks, 1);
        MouseMoveScreen(sx, sy);
        var (down, up) = ButtonFlags(button);
        var list = new List<INPUT>(clicks * 2);
        for (int i = 0; i < clicks; i++)
        {
            list.Add(MouseFlag(down));
            list.Add(MouseFlag(up));
        }
        SendOrThrow(list.ToArray());
    }

    public void MouseDownScreen(int sx, int sy, MouseButton button)
    {
        MouseMoveScreen(sx, sy);
        var (down, _) = ButtonFlags(button);
        SendOrThrow([MouseFlag(down)]);
    }

    public void MouseUpScreen(int sx, int sy, MouseButton button)
    {
        MouseMoveScreen(sx, sy);
        var (_, up) = ButtonFlags(button);
        SendOrThrow([MouseFlag(up)]);
    }

    public void MouseDragScreen(int fromX, int fromY, int toX, int toY, MouseButton button)
    {
        MouseDownScreen(fromX, fromY, button);
        MouseMoveScreen(toX, toY);
        MouseUpScreen(toX, toY, button);
    }

    public void MouseScrollScreen(int sx, int sy, int clicks, bool horizontal)
    {
        MouseMoveScreen(sx, sy);
        uint flag = horizontal ? Win32.MOUSEEVENTF_HWHEEL : Win32.MOUSEEVENTF_WHEEL;
        SendOrThrow([new INPUT
        {
            type = Win32.INPUT_MOUSE,
            U = new InputUnion
            {
                mi = new MOUSEINPUT
                {
                    mouseData = (uint)(clicks * Win32.WHEEL_DELTA),
                    dwFlags = flag,
                }
            }
        }]);
    }

    public (int x, int y) GetCursorPos()
    {
        if (!Win32.GetCursorPos(out var p))
            throw new InvalidOperationException("GetCursorPos failed");
        return (p.X, p.Y);
    }

    private static (uint down, uint up) ButtonFlags(MouseButton b) => b switch
    {
        MouseButton.Left => (Win32.MOUSEEVENTF_LEFTDOWN, Win32.MOUSEEVENTF_LEFTUP),
        MouseButton.Right => (Win32.MOUSEEVENTF_RIGHTDOWN, Win32.MOUSEEVENTF_RIGHTUP),
        MouseButton.Middle => (Win32.MOUSEEVENTF_MIDDLEDOWN, Win32.MOUSEEVENTF_MIDDLEUP),
        _ => throw new ArgumentOutOfRangeException(nameof(b)),
    };

    private static INPUT MouseFlag(uint flag) => new()
    {
        type = Win32.INPUT_MOUSE,
        U = new InputUnion { mi = new MOUSEINPUT { dwFlags = flag } }
    };

    private static (int nx, int ny) NormalizeToVirtualDesktop(int sx, int sy)
    {
        int vsLeft = Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN);
        int vsTop = Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);
        int vsW = Win32.GetSystemMetrics(Win32.SM_CXVIRTUALSCREEN);
        int vsH = Win32.GetSystemMetrics(Win32.SM_CYVIRTUALSCREEN);
        int nx = (int)Math.Round((sx - vsLeft) * 65535.0 / Math.Max(1, vsW - 1));
        int ny = (int)Math.Round((sy - vsTop) * 65535.0 / Math.Max(1, vsH - 1));
        return (nx, ny);
    }

    private static void SendOrThrow(INPUT[] inputs)
    {
        uint sent = Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
            throw new InvalidOperationException($"SendInput sent {sent} of {inputs.Length} events. GetLastError={Marshal.GetLastWin32Error()}.");
    }

    // Keyboard methods added in Task 2.2.
    internal INPUT MakeUnicodeDown(ushort unit) => new()
    {
        type = Win32.INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wScan = unit, dwFlags = Win32.KEYEVENTF_UNICODE } }
    };
    internal INPUT MakeUnicodeUp(ushort unit) => new()
    {
        type = Win32.INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wScan = unit, dwFlags = Win32.KEYEVENTF_UNICODE | Win32.KEYEVENTF_KEYUP } }
    };
    internal static void SendBatch(INPUT[] inputs) => SendOrThrow(inputs);

    public void TypeText(string text, int delayMs)
    {
        ArgumentNullException.ThrowIfNull(text);
        foreach (var unit in text)
        {
            SendBatch([MakeUnicodeDown(unit), MakeUnicodeUp(unit)]);
            if (delayMs > 0) Thread.Sleep(delayMs);
        }
    }

    public void KeyPress(string key)
    {
        var vk = VirtualKeyMap.Resolve(key);
        SendBatch([VkDown(vk), VkUp(vk)]);
    }

    public void KeyHold(string key, int ms)
    {
        var vk = VirtualKeyMap.Resolve(key);
        SendBatch([VkDown(vk)]);
        Thread.Sleep(ms);
        SendBatch([VkUp(vk)]);
    }

    public void KeyHotkey(string chord)
    {
        var vks = VirtualKeyMap.ParseChord(chord);
        if (vks.Length == 0) return;
        var down = vks.Select(VkDown).ToArray();
        var up = vks.Reverse().Select(VkUp).ToArray();
        SendBatch(down);
        SendBatch(up);
    }

    private static INPUT VkDown(ushort vk) => new()
    {
        type = Win32.INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wVk = vk } }
    };

    private static INPUT VkUp(ushort vk) => new()
    {
        type = Win32.INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = Win32.KEYEVENTF_KEYUP } }
    };
}
