using System.Text.Json;
using ModelContextProtocol.Server;

namespace FluxMcp;

internal sealed class CustomSerializerOptionsProvider : McpSerializerOptionsProvider
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public override JsonSerializerOptions GetSerializerOptions() => _options;
}
