using ModelContextProtocol.Server;
using System.ComponentModel;
using Mcp.ComputerUse.Json;

namespace Mcp.ComputerUse.Tools;

[McpServerToolType]
public sealed class PingTools
{
    [McpServerTool, Description("Returns a hello message and server timestamp. Confirms wire connectivity.")]
    public static PingResult Ping([Description("Optional message to echo back")] string? message = null)
        => new(message ?? "pong", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
}
