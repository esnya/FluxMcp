using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;
using ModelContextProtocol.Protocol;
using System.Text.Json;
using System.Text;

namespace NetfxMcp;

/// <summary>
/// Stateless HTTP server transport implementation for Model Context Protocol communication.
/// </summary>
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
            _httpBodies = httpBodies;
            _parentTransport = parentTransport;
        }

        public async ValueTask<bool> RunAsync(CancellationToken cancellationToken)
        {
            try
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
                    while (channel.TryRead(out var queuedMessage))
                    {
                        await _httpBodies.Output.WriteAsync(_messageEventPrefix, cancellationToken).ConfigureAwait(false);
                        await JsonSerializer.SerializeAsync(_httpBodies.Output.AsStream(), queuedMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
                        await _httpBodies.Output.WriteAsync(_messageEventSuffix, cancellationToken).ConfigureAwait(false);

                        if (queuedMessage is JsonRpcMessageWithId response && response.Id == _pendingRequest)
                        {
                            done = true;
                            break;
                        }
                    }
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled, just exit gracefully.
                return false;
            }
        }

        public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
        {
            if (message is JsonRpcRequest)
            {
                throw new InvalidOperationException("Server to client requests are not supported in stateless mode.");
            }

            await _messages.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
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

        public ValueTask DisposeAsync()
        {
            return default;
        }

    }

    private readonly CancellationTokenSource _disposeCts = new();
    private readonly Channel<JsonRpcMessage> _incomingChannel = Channel.CreateBounded<JsonRpcMessage>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        SingleWriter = false,
    });

    private ChannelWriter<JsonRpcMessage> MessageWriter => _incomingChannel.Writer;

    /// <summary>
    /// Gets the channel reader for receiving JSON-RPC messages.
    /// </summary>
    public ChannelReader<JsonRpcMessage> MessageReader => _incomingChannel.Reader;

    /// <summary>
    /// Gets the initialization request parameters, if any have been received.
    /// </summary>
    public InitializeRequestParams? InitializeRequest { get; private set; }

    /// <summary>
    /// Handles an incoming HTTP POST request containing a JSON-RPC message.
    /// </summary>
    /// <param name="httpBodies">The HTTP request and response body pipes.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result indicates whether the operation completed successfully.</returns>
    public async Task<bool> HandlePostRequest(IDuplexPipe httpBodies, CancellationToken cancellationToken)
    {
        using var postCts = CancellationTokenSource.CreateLinkedTokenSource(_disposeCts.Token, cancellationToken);
        var postTransport = new PostTransport(this, httpBodies);
        try
        {
            return await postTransport.RunAsync(postCts.Token).ConfigureAwait(false);
        }
        finally
        {
            await postTransport.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Disposes the transport asynchronously.
    /// </summary>
    /// <returns>A value task that represents the asynchronous dispose operation.</returns>
    public ValueTask DisposeAsync()
    {
        _disposeCts.Cancel();
        _disposeCts.Dispose();
        return default;
    }

    /// <summary>
    /// Sends a JSON-RPC message asynchronously. This operation is not supported in stateless mode.
    /// </summary>
    /// <param name="message">The JSON-RPC message to send.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous send operation.</returns>
    /// <exception cref="InvalidOperationException">Always thrown as unsolicited server-to-client messages are not supported in stateless mode.</exception>
    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("Unsolicited server to client messages are not supported in stateless mode.");
    }
}
