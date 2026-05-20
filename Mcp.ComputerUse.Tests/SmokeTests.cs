using FluentAssertions;
using Mcp.ComputerUse.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Mcp.ComputerUse.Tests;

public class SmokeTests
{
    private static PingTools NewPing() => new(NullLogger<PingTools>.Instance);

    [Fact]
    public void Ping_returns_message_and_timestamp()
    {
        var result = NewPing().Ping("hi");
        result.Message.Should().Be("hi");
        result.ServerTimeUnixMs.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Ping_defaults_to_pong()
    {
        var result = NewPing().Ping(null);
        result.Message.Should().Be("pong");
    }
}
