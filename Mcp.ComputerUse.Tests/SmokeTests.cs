using FluentAssertions;
using Mcp.ComputerUse.Tools;
using Xunit;

namespace Mcp.ComputerUse.Tests;

public class SmokeTests
{
    [Fact]
    public void Ping_returns_message_and_timestamp()
    {
        var result = PingTools.Ping("hi");
        result.Message.Should().Be("hi");
        result.ServerTimeUnixMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Ping_defaults_to_pong()
    {
        var result = PingTools.Ping(null);
        result.Message.Should().Be("pong");
    }
}
