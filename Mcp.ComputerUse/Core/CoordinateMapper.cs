namespace Mcp.ComputerUse.Core;

public readonly record struct ScalingTarget(string Name, int Width, int Height);

public readonly record struct ScalePlan(
    int OrigWidth, int OrigHeight,
    int ScaledWidth, int ScaledHeight,
    double FactorX, double FactorY,
    int MonitorLeft, int MonitorTop,
    string TargetName);

public sealed class CoordinateMapper
{
    public static readonly ScalingTarget[] Targets =
    [
        new("XGA",   1024, 768),  // 4:3
        new("WXGA",  1280, 800),  // 16:10
        new("FWXGA", 1366, 768),  // ~16:9
    ];

    private const double AspectTolerance = 0.02;

    public ScalePlan PlanFor(int origW, int origH, int monitorLeft, int monitorTop)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(origW, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(origH, 0);

        double ratio = (double)origW / origH;
        ScalingTarget? best = null;
        foreach (var t in Targets)
        {
            double tRatio = (double)t.Width / t.Height;
            if (Math.Abs(tRatio - ratio) < AspectTolerance && t.Width < origW)
            {
                best = t;
                break;
            }
        }

        if (best is null)
            return new ScalePlan(origW, origH, origW, origH, 1.0, 1.0, monitorLeft, monitorTop, "NATIVE");

        var pick = best.Value;
        return new ScalePlan(
            origW, origH,
            pick.Width, pick.Height,
            (double)pick.Width / origW,
            (double)pick.Height / origH,
            monitorLeft, monitorTop,
            pick.Name);
    }

    public (int x, int y) ModelToScreen(ScalePlan p, int mx, int my)
    {
        if (mx < 0 || mx > p.ScaledWidth || my < 0 || my > p.ScaledHeight)
            throw new ArgumentOutOfRangeException(
                nameof(mx),
                $"Model coordinates ({mx},{my}) outside scaled bounds {p.ScaledWidth}x{p.ScaledHeight}.");
        return (
            (int)Math.Round(mx / p.FactorX) + p.MonitorLeft,
            (int)Math.Round(my / p.FactorY) + p.MonitorTop);
    }

    public (int x, int y) ScreenToModel(ScalePlan p, int sx, int sy) =>
        ((int)Math.Round((sx - p.MonitorLeft) * p.FactorX),
         (int)Math.Round((sy - p.MonitorTop) * p.FactorY));
}
