using FluentAssertions;
using Mcp.ComputerUse.Core;
using Xunit;

namespace Mcp.ComputerUse.Tests;

public class MonitorRegistryTests
{
    [Fact]
    public void Refresh_returns_at_least_primary_monitor()
    {
        var reg = new MonitorRegistry();
        reg.Monitors.Should().NotBeEmpty();
        reg.Monitors.Should().Contain(m => m.IsPrimary);
        reg.Monitors[0].Bounds.Width.Should().BeGreaterThan(0);
        reg.Monitors[0].Bounds.Height.Should().BeGreaterThan(0);
        reg.Monitors[0].DpiX.Should().BeGreaterOrEqualTo(96);
    }
}
