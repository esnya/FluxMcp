using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetfxMcp;

internal sealed class DuplexPipe : IDuplexPipe
    {
        public PipeReader Input { get; private set; }

        public PipeWriter Output { get; private set; }

        public DuplexPipe(PipeReader input, PipeWriter output)
        {
            Input = input;
            Output = output;
        }
    }


    /// <summary>
    /// HTTP streaming server implementation for Model Context Protocol communication.
    /// </summary>
    public class McpHttpStreamingServer : IAsyncDisposable
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed in DisposeAsync")]
        private readonly StatelessHttpServerTransport _transport;

        private readonly IMcpServer _mcpServer;
        private readonly HttpListener _listener = new HttpListener();
        private readonly ILogger _logger;

        /// <summary>
        /// Gets a value indicating whether the HTTP server is currently running.
        /// </summary>
        public bool IsRunning => _listener.IsListening;

        /// <summary>
        /// Initializes a new instance of the <see cref="McpHttpStreamingServer"/> class.
        /// </summary>
        /// <param name="logger">The logger instance to use.</param>
        /// <param name="serverBuilder">Function to build the MCP server with the provided transport.</param>
        /// <param name="prefix">The HTTP URL prefix to listen on.</param>
        /// <exception cref="ArgumentNullException">Thrown when logger or serverBuilder is null.</exception>
        /// <exception cref="ArgumentException">Thrown when prefix is null or empty.</exception>
        public McpHttpStreamingServer(ILogger logger, Func<ITransport, IMcpServer> serverBuilder, string prefix = "http://+:8080/")
        {
            if (serverBuilder is null)
            {
                throw new ArgumentNullException(nameof(serverBuilder), "Server builder function cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException("URL prefix cannot be null or empty.", nameof(prefix));
            }


            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _listener.Prefixes.Add(prefix);
            _transport = new StatelessHttpServerTransport();
            _mcpServer = serverBuilder(_transport);
        }

        /// <summary>
        /// Starts the HTTP server asynchronously and begins listening for MCP requests.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Starting MCP Server...");
                var server = _mcpServer.RunAsync(cancellationToken);

                try
                {
                    _logger.LogDebug("Starting HTTP listener...");
                    _listener.Start();

                    using var registration = cancellationToken.Register(() => _listener.Stop());

                    _logger.LogInformation("MCP Streamable HTTP server listening on {Prefixes}", _listener.Prefixes);

                    _logger.LogDebug("Listening for incoming requests...");
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        HttpListenerContext ctx;
                        try
                        {
                            ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                        }
                        catch (HttpListenerException)
                        {
                            break;
                        }
                        catch (ObjectDisposedException)
                        {
                            break;
                        }

                        _logger.LogDebug("Received request: {Method} {Url}", ctx.Request.HttpMethod, ctx.Request.Url);
                        await HandleContextAsync(ctx, cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    await server.ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error in StartAsync");
                throw;
            }
            finally
            {
                _logger.LogInformation("MCP HTTP server stopped.");
            }
        }

        /// <summary>
        /// Stops the HTTP server and ceases listening for requests.
        /// </summary>
        public void Stop()
        {
            _logger.LogInformation("Stopping MCP HTTP server...");
            _listener.Stop();
        }

        private async Task HandleContextAsync(HttpListenerContext ctx, CancellationToken cancellationToken = default)
        {
            try
            {
                var request = ctx.Request;
                var response = ctx.Response;

                if (request.HttpMethod != "POST")
                {
                    response.StatusCode = 405; // Method Not Allowed
                    response.Close();
                    return;
                }
                else if (request.Url.AbsolutePath != "/mcp")
                {
                    response.StatusCode = 404; // Not Found
                    response.Close();
                    return;
                }


                // response.KeepAlive = false;
                // response.SendChunked = true;
                // response.StatusCode = 202; // Accepted
                response.AddHeader("Content-Type", "text/event-stream");

                _logger.LogDebug("Handling request: {Method} {Url}", request.HttpMethod, request.Url);
                var duplexPipe = new DuplexPipe(PipeReader.Create(request.InputStream), PipeWriter.Create(response.OutputStream));
                var bodyWritten = await _transport.HandlePostRequest(duplexPipe, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Request body written: {Written}", bodyWritten);
                await response.OutputStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    response.Close();
                }
                catch (HttpListenerException ex)
                {
                    _logger.LogWarning(ex, "Error closing response");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "{Type}: {Message}\n\t{Stack}", ex.GetType(), ex.Message, ex.StackTrace);
                var response = ctx.Response;
                if (response.OutputStream.CanWrite)
                {
                    response.StatusCode = 500;
                    var bytes = Encoding.UTF8.GetBytes(ex.Message);
                    await response.OutputStream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
                    response.Close();
                }

                throw;
            }
        }

        /// <summary>
        /// Asynchronously releases all resources used by the HTTP server.
        /// </summary>
        /// <returns>A ValueTask representing the asynchronous dispose operation.</returns>
        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);

            try
            {
                _logger.LogDebug("Disposing MCP HTTP server...");
                Stop();
                await _mcpServer.DisposeAsync().ConfigureAwait(false);
                _listener.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing MCP HTTP server");
                throw;
            }
        }
    }
