
using ModelContextProtocol.Client;
using ResoniteModLoader;
using ModelContextProtocol.Protocol;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;

namespace FluxMcp.Tests;

[TestClass]
public sealed class McpServerTests
{
    [TestMethod]
    [Timeout(10000)]
    public async Task McpServer_ShouldReturnNonEmptyToolsArray()
    {
        var mod = new FluxMcpMod();
        FluxMcpMod.RegisterHotReloadAction = null;

        typeof(ResoniteModBase).GetProperty("FinishedLoading", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(mod, true);

        mod.OnEngineInit();

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync("127.0.0.1", 5000);
        using var networkStream = tcpClient.GetStream();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        var clientTransport = new StreamClientTransport(networkStream, networkStream, loggerFactory);
        var client = await McpClientFactory.CreateAsync(clientTransport);

        var tools = await client.ListToolsAsync().ConfigureAwait(false);
        Console.WriteLine($"{tools.Count} tools found:\n{string.Join(", ", tools.Select(t => '\t' + t.Name))}");

        Assert.IsNotNull(tools, "Tools array should not be null.");
        Assert.IsTrue(tools.Count > 0, "Tools array should not be empty.");

        await client.DisposeAsync();

        FluxMcpMod.BeforeHotReload();
    }
}
