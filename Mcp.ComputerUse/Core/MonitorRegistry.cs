using System.Runtime.InteropServices;
using System.Threading;
using Mcp.ComputerUse.Native;

namespace Mcp.ComputerUse.Core;

public sealed class MonitorRegistry
{
    private const int MDT_EFFECTIVE_DPI = 0;
    private const uint MONITORINFOF_PRIMARY = 1;

    private readonly Lock _gate = new();
    public IReadOnlyList<MonitorInfo> Monitors { get; private set; } = [];

    public MonitorRegistry()
    {
        Win32.SetProcessDpiAwarenessContext(Win32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        Refresh();
    }

    public void Refresh()
    {
        var list = new List<MonitorInfo>();
        int idx = 0;
        // Hold delegate in a local so the GC doesn't free it during the call.
        Win32.MonitorEnumProc proc = (IntPtr hMon, IntPtr _, ref RECT _, IntPtr _) =>
        {
            var mi = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>(), szDevice = string.Empty };
            if (!Win32.GetMonitorInfo(hMon, ref mi)) return true;
            Win32.GetDpiForMonitor(hMon, MDT_EFFECTIVE_DPI, out var dpiX, out var dpiY);
            list.Add(new MonitorInfo(
                Index: idx++,
                DeviceName: mi.szDevice,
                Bounds: new Rect(mi.rcMonitor.Left, mi.rcMonitor.Top,
                                 mi.rcMonitor.Right - mi.rcMonitor.Left,
                                 mi.rcMonitor.Bottom - mi.rcMonitor.Top),
                WorkArea: new Rect(mi.rcWork.Left, mi.rcWork.Top,
                                   mi.rcWork.Right - mi.rcWork.Left,
                                   mi.rcWork.Bottom - mi.rcWork.Top),
                IsPrimary: (mi.dwFlags & MONITORINFOF_PRIMARY) != 0,
                DpiX: (int)dpiX,
                DpiY: (int)dpiY));
            return true;
        };
        Win32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, proc, IntPtr.Zero);
        GC.KeepAlive(proc);

        lock (_gate)
        {
            Monitors = list;
        }
    }

    public MonitorInfo GetOrThrow(int index)
    {
        var snapshot = Monitors;
        if (index < 0 || index >= snapshot.Count)
        {
            Refresh();
            snapshot = Monitors;
        }
        if (index < 0 || index >= snapshot.Count)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Monitor index {index} not found. Available: 0..{snapshot.Count - 1}.");
        return snapshot[index];
    }
}
