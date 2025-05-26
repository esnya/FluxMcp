using Elements.Core;
using FluxMcp.Tools;
using FrooxEngine;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;
using NetfxMcp;
using System.Text.Json;

namespace FluxMcp.Tests;

internal sealed class FakeWorldElement : IWorldElement
{
    public RefID ReferenceID => new RefID(0x12345678);

    public string Name => "FakeElement";

    public World World => throw new NotImplementedException();

    public IWorldElement Parent => default(IWorldElement)!;

    public bool IsLocalElement => false;

    public bool IsPersistent => true;

    public bool IsRemoved => false;

    public void ChildChanged(IWorldElement child) => throw new NotImplementedException();
    public string GetSyncMemberName(ISyncMember member) => throw new NotImplementedException();
    public void Load(DataTreeNode node, LoadControl control) => throw new NotImplementedException();
    public DataTreeNode Save(SaveControl control) => throw new NotImplementedException();
}


[McpServerToolType]
static class McpFakeTools
{
    [McpServerTool(Name = "returnString")]
    public static string ReturnString()
    {
        return "Hello, World!";
    }

    [McpServerTool(Name = "returnWorldElement")]
    public static object? ReturnWorldElement()
    {
        return NodeToolHelpers.Handle(() => new FakeWorldElement());
    }
}

internal sealed class TestLogger : INetfxMcpLogger
{
    public void Debug(string message) => System.Diagnostics.Debug.WriteLine(message);
    public void DebugFunc(Func<string> messageFunc) => System.Diagnostics.Debug.WriteLine(messageFunc());
    public void Msg(string message) => System.Diagnostics.Debug.WriteLine(message);
    public void Warn(string message) => System.Diagnostics.Debug.WriteLine(message);
}

[TestClass]
public sealed class McpFakeToolTests : IDisposable
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    private McpHttpStreamingServer _server;
    private CancellationTokenSource _serverCts;
    private Task _serverTask;
    private IMcpClient _client;
    private IList<McpClientTool> _tools;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.


    [TestInitialize]
    public async Task Setup()
    {
        var logger = new TestLogger();
        _server = new McpHttpStreamingServer(
            logger,
            transport => McpServerBuilder.Build(logger, transport, typeof(McpFakeTools).Assembly),
            $"http://127.0.0.1:5050/"
        );

        _serverCts = new CancellationTokenSource();
        _serverTask = _server.StartAsync(_serverCts.Token);

#pragma warning disable CA2000 // Dispose objects before losing scope - SseClientTransport is managed by McpClientFactory
        var clientTransport = new SseClientTransport(
            new()
            {
                Endpoint = new Uri("http://127.0.0.1:5050/mcp"),
                UseStreamableHttp = true,
            }
        );
#pragma warning restore CA2000 // Dispose objects before losing scope
        _client = await McpClientFactory.CreateAsync(clientTransport).ConfigureAwait(false);

        _tools = await _client.ListToolsAsync().ConfigureAwait(false);
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        if (_client != null)
        {
            await _client.DisposeAsync().ConfigureAwait(false);
        }

        if (_serverCts != null)
        {
            _serverCts.Cancel();
            _server.Stop();
            await _serverTask.ConfigureAwait(false);
        }
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task ShouldReturnsString()
    {
        var tool = _tools.FirstOrDefault(t => t.Name == "returnString");
        Assert.IsNotNull(tool, "Tool 'returnString' should be found.");

        var result = (JsonElement?)await tool.InvokeAsync().ConfigureAwait(false);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.ToString().Contains("\"text\":\"Hello, World!\""));
    }

    [TestMethod]
    [Timeout(10000)]
    public async Task ShouldReturnWorldElement()
    {
        var tool = _tools.FirstOrDefault(t => t.Name == "returnWorldElement");
        Assert.IsNotNull(tool, "Tool 'returnWorldElement' should be found.");

        var result = (JsonElement?)await tool.InvokeAsync(new AIFunctionArguments()).ConfigureAwait(false);
        Assert.IsNotNull(result);
        Assert.IsTrue(result.ToString().UnescapeUnicodeCharacters().Contains("\"refId\":\"ID12345678\""));
        Assert.IsTrue(result.ToString().UnescapeUnicodeCharacters().Contains("\"name\":\"FakeElement\""));
    }

    public void Dispose()
    {
        try
        {
            _client?.DisposeAsync().AsTask().Wait();
        }
        catch (AggregateException)
        {
            // Ignore disposal exceptions during test cleanup
        }

        try
        {
            _serverCts?.Cancel();
            _server?.Stop();
            _serverTask?.Wait();
            _server?.DisposeAsync().AsTask().Wait();
        }
        catch (AggregateException)
        {
            // Ignore disposal exceptions during test cleanup
        }

        _serverCts?.Dispose();
    }
}
