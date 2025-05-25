using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ResoniteModLoader;
using System;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FluxMcp
{
    internal class DuplexPipe : IDuplexPipe
    {
        public PipeReader Input { get; private set; }

        public PipeWriter Output { get; private set; }

        public DuplexPipe(PipeReader input, PipeWriter output)
        {
            Input = input;
            Output = output;
        }
    }


    public class McpHttpStreamingServer : IAsyncDisposable
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposed in DisposeAsync")]
        private readonly StatelessHttpServerTransport _transport = new StatelessHttpServerTransport();

        private readonly IMcpServer _mcpServer;
        private readonly HttpListener _listener = new HttpListener();

        public bool IsRunning => _listener.IsListening;

        public McpHttpStreamingServer(Func<ITransport, IMcpServer> serverBuilder, string prefix = "http://+:8080/")
        {
            if (serverBuilder is null)
            {
                throw new ArgumentNullException(nameof(serverBuilder), "Server builder function cannot be null.");
            }

            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException("URL prefix cannot be null or empty.", nameof(prefix));
            }

            _listener.Prefixes.Add(prefix);
            _mcpServer = serverBuilder(_transport);
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                ResoniteMod.Debug("Starting MCP Server...");
                var server = _mcpServer.RunAsync(cancellationToken);

                try
                {
                    ResoniteMod.Debug("Starting HTTP listener...");
                    _listener.Start();

                    ResoniteMod.Msg($"MCP Streamable HTTP server listening on {_listener.Prefixes}");

                    ResoniteMod.Debug("Listening for incoming requests...");
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

                        ResoniteMod.Debug($"Received request: {ctx.Request.HttpMethod} {ctx.Request.Url}");
                        _ = Task.Run(() => HandleContextAsync(ctx, cancellationToken), cancellationToken);
                    }
                }
                finally
                {
                    await server.ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                ResoniteMod.Warn($"Error in StartAsync: {ex.Message}");
                throw;
            }
            finally
            {
                ResoniteMod.Msg("MCP HTTP server stopped.");
            }
        }

        public void Stop()
        {
            ResoniteMod.Msg("Stopping MCP HTTP server...");
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

                ResoniteMod.Debug($"Handling request: {request.HttpMethod} {request.Url}");
                var duplexPipe = new DuplexPipe(PipeReader.Create(request.InputStream), PipeWriter.Create(response.OutputStream));
                var bodyWritten = await _transport.HandlePostRequest(duplexPipe, cancellationToken).ConfigureAwait(false);
                ResoniteMod.DebugFunc(() => $"Request body written: {bodyWritten}");
                await response.OutputStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                try
                {
                    response.Close();
                }
                catch (HttpListenerException ex)
                {
                    ResoniteMod.Warn($"Error closing response: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                ResoniteMod.Warn($"{ex.GetType()}: {ex.Message}\n\t{ex.StackTrace}");
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

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);

            try
            {
                ResoniteMod.Debug("Disposing MCP HTTP server...");
                Stop();
                await _mcpServer.DisposeAsync().ConfigureAwait(false);
                _listener.Close();
            }
            catch (Exception ex)
            {
                ResoniteMod.Warn($"Error disposing MCP HTTP server: {ex.Message}");
                throw;
            }
        }
    }
}
