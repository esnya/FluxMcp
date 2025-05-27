using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Threading.Channels;

namespace NetfxMcp.Tests;

[TestClass]
public class McpServerBuilderTests
    {
    private sealed class DummyLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
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
    public void BuildReturnsServerWhenAssemblyHasTools()
    {
        var logger = new DummyLogger();
#pragma warning disable CA2000 // Dispose objects before losing scope - DummyTransport doesn't implement IDisposable
        var transport = new DummyTransport();
#pragma warning restore CA2000 // Dispose objects before losing scope

        var server = McpServerBuilder.Build(logger, transport, typeof(SampleTools).Assembly);

        Assert.IsNotNull(server);
    }
}
