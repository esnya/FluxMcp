using ModelContextProtocol.Client;
using ResoniteModLoader;
using Microsoft.Extensions.Logging;

namespace FluxMcp.Tests;

[TestClass]
public sealed class McpServerTests
{
    [TestMethod]
    [Timeout(10000)]
    public async Task McpServer()
    {
        var mod = new FluxMcpMod();
        FluxMcpMod.RegisterHotReloadAction = null;

        typeof(ResoniteModBase).GetProperty("FinishedLoading", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(mod, true);

        mod.OnEngineInit();

        while (!FluxMcpMod.IsServerRunning)
        {
            // Console.WriteLine("Waiting for MCP server to start...");
            await Task.Delay(100).ConfigureAwait(false);
        }

        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
#pragma warning disable CA2000 // Dispose objects before losing scope - SseClientTransport is managed by McpClientFactory
#pragma warning disable CA2007 // Consider calling ConfigureAwait on the awaited task - await using pattern doesn't support ConfigureAwait
        await using var clientTransport = new SseClientTransport(
            new()
            {
                Endpoint = new Uri("http://127.0.0.1:5000/mcp"),
                UseStreamableHttp = true,
            },
            loggerFactory: loggerFactory
        );
#pragma warning restore CA2007 // Consider calling ConfigureAwait on the awaited task
#pragma warning restore CA2000 // Dispose objects before losing scope
        var client = await McpClientFactory.CreateAsync(clientTransport).ConfigureAwait(false);

        var tools = await client.ListToolsAsync().ConfigureAwait(false);
        Console.WriteLine($"{tools.Count} tools found: {string.Join(", ", tools.Select(t => t.Name))}");

        Assert.IsNotNull(tools, "Tools array should not be null.");
        Assert.IsTrue(tools.Count > 0, "Tools array should not be empty.");

        await client.DisposeAsync().ConfigureAwait(false);

#if DEBUG
        FluxMcpMod.BeforeHotReload();
#endif
    }
}
