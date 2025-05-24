
using ModelContextProtocol.Client;
using ResoniteModLoader;
using ModelContextProtocol.Protocol;
using Microsoft.Extensions.Logging;
using System.Net.Sockets;
using FrooxEngine;

namespace FluxMcp.Tests;

[TestClass]
public sealed class McpServerTests
{
    [TestMethod]
    [Timeout(30000)]
    public async Task McpServer_ShouldReturnNonEmptyToolsArray()
    {
        var mod = new FluxMcpMod();
        FluxMcpMod.RegisterHotReloadAction = null;

        typeof(ResoniteModBase).GetProperty("FinishedLoading", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(mod, true);

        mod.OnEngineInit();

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync("127.0.0.1", 5001);
        using var networkStream = tcpClient.GetStream();

        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        var clientTransport = new StreamClientTransport(networkStream, networkStream, loggerFactory);
        var client = await McpClientFactory.CreateAsync(clientTransport);

        var tools = await client.ListToolsAsync().ConfigureAwait(false);
        Console.WriteLine($"{tools.Count} tools found:\n{string.Join(", ", tools.Select(t => '\t' + t.Name + "\n"))}");

        Assert.IsNotNull(tools, "Tools array should not be null.");
        Assert.IsTrue(tools.Count > 0, "Tools array should not be empty.");

        var manager = new CoroutineManager(default!, default!);
        CoroutineManager.Manager.Value = manager;

        //var engine = new Engine();
        //var worldManager = new WorldManager();
        //foreach (var property in typeof(Engine).GetProperties())
        //{
        //    if (!property.CanWrite) continue;

        //    if (property.Name == nameof(Engine.Current))
        //    {
        //        property.SetValue(null, engine);
        //    }
        //    else if (property.Name == nameof(Engine.WorldManager))
        //    {
        //        property.SetValue(engine, worldManager);
        //    }
        //}

        //var arguments = new Dictionary<string, object?>(){
        //    { "type", "CallInputNode" },
        //};
        //var task = client.CallToolAsync("createNode", arguments: arguments);

        //var n1 = manager.ExecuteWorldQueue(1.0/90);
        //var n2 = manager.ExecuteWorldQueue(1.0/90);
        //var res = await task;
        //Console.WriteLine(res);

        await client.DisposeAsync();

        FluxMcpMod.BeforeHotReload();
    }
}
