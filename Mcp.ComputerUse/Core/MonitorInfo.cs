namespace Mcp.ComputerUse.Core;

public readonly record struct Rect(int X, int Y, int Width, int Height);

public sealed record MonitorInfo(
    int Index,
    string DeviceName,
    Rect Bounds,
    Rect WorkArea,
    bool IsPrimary,
    int DpiX,
    int DpiY);
