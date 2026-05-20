using FluentAssertions;
using Mcp.ComputerUse.Core;
using Xunit;

namespace Mcp.ComputerUse.Tests;

public class CoordinateMapperTests
{
    private readonly CoordinateMapper _mapper = new();

    [Fact]
    public void PlanFor_4K_16x9_picks_FWXGA_close_ratio()
    {
        var plan = _mapper.PlanFor(3840, 2160, monitorLeft: 100, monitorTop: 50);
        plan.ScaledWidth.Should().Be(1366);
        plan.ScaledHeight.Should().Be(768);
        plan.FactorX.Should().BeApproximately(1366.0 / 3840.0, 1e-6);
        plan.MonitorLeft.Should().Be(100);
    }

    [Fact]
    public void PlanFor_WUXGA_16x10_picks_WXGA()
    {
        var plan = _mapper.PlanFor(1920, 1200, 0, 0);
        plan.ScaledWidth.Should().Be(1280);
        plan.ScaledHeight.Should().Be(800);
    }

    [Fact]
    public void PlanFor_below_target_does_not_upscale()
    {
        var plan = _mapper.PlanFor(800, 600, 0, 0);
        plan.ScaledWidth.Should().Be(800);
        plan.ScaledHeight.Should().Be(600);
        plan.FactorX.Should().Be(1.0);
    }

    [Fact]
    public void ModelToScreen_round_trips_via_ScreenToModel()
    {
        var plan = _mapper.PlanFor(3840, 2160, 1920, 0);
        var (sx, sy) = _mapper.ModelToScreen(plan, 640, 400);
        sx.Should().BeInRange(1920, 1920 + 3840);
        sy.Should().BeInRange(0, 2160);

        var (mx, my) = _mapper.ScreenToModel(plan, sx, sy);
        mx.Should().BeInRange(638, 642);
        my.Should().BeInRange(398, 402);
    }

    [Fact]
    public void ModelToScreen_rejects_out_of_bounds()
    {
        var plan = _mapper.PlanFor(3840, 2160, 0, 0);
        Action act = () => _mapper.ModelToScreen(plan, plan.ScaledWidth + 5, 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
