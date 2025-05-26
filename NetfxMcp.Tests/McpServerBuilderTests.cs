using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Threading.Channels;
using NetfxMcp;

[TestClass]
public class McpServerBuilderTests
{
    private sealed class DummyLogger : INetfxMcpLogger
    {
        public void Debug(string message) { }
        public void Warn(string message) { }
        public void Msg(string message) { }
        public void DebugFunc(Func<string> messageFunc) { }
    }

    private sealed class DummyTransport : ITransport
    {
        private readonly Channel<JsonRpcMessage> _channel = Channel.CreateUnbounded<JsonRpcMessage>();
        public ChannelReader<JsonRpcMessage> MessageReader => _channel.Reader;
        public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default) => _channel.Writer.WriteAsync(message, cancellationToken).AsTask();
        public ValueTask DisposeAsync() => default;
    }

    [McpServerToolType]
    private static class SampleTools
    {
        [McpServerTool(Name = "ping")]
        public static string Ping() => "pong";
    }

    [TestMethod]
    public void Build_ReturnsServer_WhenAssemblyHasTools()
    {
        var logger = new DummyLogger();
        var transport = new DummyTransport();

        var server = McpServerBuilder.Build(logger, transport, typeof(SampleTools).Assembly);

        Assert.IsNotNull(server);
    }
}
