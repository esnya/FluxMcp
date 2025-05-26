using ModelContextProtocol.Client;
using ResoniteModLoader;
using ModelContextProtocol.Protocol;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using FrooxEngine;
using Elements.Core;
using FluxMcp.Tools;
using Moq;

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
            await Task.Delay(100);
        }

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        var clientTransport = new SseClientTransport(
            new()
            {
                Endpoint = new Uri("http://127.0.0.1:5000/mcp"),
                UseStreamableHttp = true,
            },
            loggerFactory: loggerFactory
        );
        var client = await McpClientFactory.CreateAsync(clientTransport);

        var tools = await client.ListToolsAsync().ConfigureAwait(false);
        Console.WriteLine($"{tools.Count} tools found:\n{string.Join(", ", tools.Select(t => '\t' + t.Name + "\n"))}");

        Assert.IsNotNull(tools, "Tools array should not be null.");
        Assert.IsTrue(tools.Count > 0, "Tools array should not be empty.");

        await client.DisposeAsync();

        FluxMcpMod.BeforeHotReload();
    }
}
