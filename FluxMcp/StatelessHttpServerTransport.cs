using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Net.ServerSentEvents;
using System.Text;

namespace FluxMcp;

public sealed class StatelessHttpServerTransport : ITransport
{
    internal sealed class PostTransport : ITransport
    {
        private readonly Channel<JsonRpcMessage> _messages = Channel.CreateBounded<JsonRpcMessage>(new BoundedChannelOptions(1)
        {
            SingleReader = false,
            SingleWriter = true,
        });

        private readonly IDuplexPipe _httpBodies;
        private readonly StatelessHttpServerTransport _parentTransport;
        private RequestId _pendingRequest;
        private readonly byte[] _messageEventPrefix = Encoding.UTF8.GetBytes("event: message\r\ndata: ");
        private readonly byte[] _messageEventSuffix = Encoding.UTF8.GetBytes("\r\n\r\n");

        public PostTransport(StatelessHttpServerTransport parentTransport, IDuplexPipe httpBodies)
        {
            _httpBodies = httpBodies ?? throw new ArgumentNullException(nameof(httpBodies), "HTTP bodies cannot be null.");
            _parentTransport = parentTransport ?? throw new ArgumentNullException(nameof(parentTransport), "Parent transport cannot be null.");
        }

        public async ValueTask<bool> RunAsync(CancellationToken cancellationToken)
        {
            var message = await JsonSerializer.DeserializeAsync<JsonRpcMessage>(_httpBodies.Input.AsStream(), cancellationToken: cancellationToken).ConfigureAwait(false);
            await OnMessageReceivedAsync(message, cancellationToken).ConfigureAwait(false);

            if (_pendingRequest.Id is null)
            {
                return false;
            }

            var channel = _messages.Reader;
            bool done = false;
            while (await channel.WaitToReadAsync(cancellationToken).ConfigureAwait(false) && !done)
            {
                while (channel.TryRead(out var mesasge))
                {
                    await _httpBodies.Output.WriteAsync(_messageEventPrefix, cancellationToken).ConfigureAwait(false);
                    await JsonSerializer.SerializeAsync(_httpBodies.Output.AsStream(), mesasge, cancellationToken: cancellationToken).ConfigureAwait(false);
                    await _httpBodies.Output.WriteAsync(_messageEventSuffix, cancellationToken).ConfigureAwait(false);

                    if (mesasge is JsonRpcMessageWithId response && response.Id == _pendingRequest)
                    {
                        done = true;
                        break;
                    }
                }
            }

            return true;
        }

        public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
        {
            if (message is JsonRpcRequest)
            {
                throw new InvalidOperationException("Server to client requests are not supported in stateless mode.");
            }

            await _messages.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        }

        private async IAsyncEnumerable<SseItem<JsonRpcMessage?>> StopOnFinalResponseFilter(IAsyncEnumerable<SseItem<JsonRpcMessage?>> messages, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (var message in messages.WithCancellation(cancellationToken))
            {
                yield return message;

                if (message.Data is JsonRpcMessageWithId response && response.Id == _pendingRequest)
                {
                    // Complete the SSE response stream now that all pending requests have been processed.
                    break;
                }
            }
        }

        private async ValueTask OnMessageReceivedAsync(JsonRpcMessage? message, CancellationToken cancellationToken)
        {
            if (message is null)
            {
                throw new InvalidOperationException("Received invalid null message.");
            }

            if (message is JsonRpcRequest request)
            {
                _pendingRequest = request.Id;

                if (request.Method == RequestMethods.Initialize)
                {
                    _parentTransport.InitializeRequest = JsonSerializer.Deserialize<InitializeRequestParams?>(request.Params);
                }
            }

            message.RelatedTransport = this;

            await _parentTransport.MessageWriter.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        }

        public ChannelReader<JsonRpcMessage> MessageReader => throw new NotSupportedException("JsonRpcMessage.RelatedTransport should only be used for sending messages.");

        public async ValueTask DisposeAsync()
        {
        }

    }

    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Channel<JsonRpcMessage> _incomingChannel = Channel.CreateBounded<JsonRpcMessage>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        SingleWriter = false,
    });

    private ChannelWriter<JsonRpcMessage> MessageWriter => _incomingChannel.Writer;
    public ChannelReader<JsonRpcMessage> MessageReader => _incomingChannel.Reader;
    public InitializeRequestParams? InitializeRequest { get; private set; }


    public async Task<bool> HandlePostRequest(IDuplexPipe httpBodies, CancellationToken cancellationToken)
    {
        using var postCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);
        await using var postTransport = new PostTransport(this, httpBodies);
        return await postTransport.RunAsync(postCts.Token).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _disposeCts.Dispose();
    }

    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Unsolicited server to client messages are not supported in stateless mode.");
    }
}
