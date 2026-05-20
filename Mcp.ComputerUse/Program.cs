using Mcp.ComputerUse.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// stdio MCP: stdout is reserved for the protocol. Logs must go to stderr.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<PingTools>();

await builder.Build().RunAsync();
