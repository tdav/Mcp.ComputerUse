using System.Text.Json.Serialization;

namespace Mcp.ComputerUse.Json;

public sealed record PingResult(string Message, long ServerTimeUnixMs);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PingResult))]
internal partial class McpJsonContext : JsonSerializerContext;
