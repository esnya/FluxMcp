using ModelContextProtocol.Server;
using ResoniteModLoader;
using System;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace FluxMcp
{
    public class TcpTransport
    {
        private readonly TcpListener _listener;

        public TcpTransport(string ipAddress, int port)
        {
            _listener = new TcpListener(IPAddress.Parse(ipAddress), port);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _listener.Start();
                ResoniteMod.Msg($"TCP server started on {_listener.LocalEndpoint}");
                ResoniteMod.Debug($"Listener started at {_listener.LocalEndpoint}");

                while (!cancellationToken.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    ResoniteMod.Debug($"Accepted connection from {client.Client.RemoteEndPoint}");
                    _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
                }

                ResoniteMod.Msg("TCP server stopped.");
            }
            catch (Exception ex)
            {
                ResoniteMod.Warn($"Error in StartAsync: {ex.Message}");
                throw;
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            try
            {
                var inputPipe = new Pipe();
                var outputPipe = new Pipe();

                ResoniteMod.Msg($"Client {client.Client.RemoteEndPoint} connected");

                ResoniteMod.Debug("Building streams");

                using var networkStream = client.GetStream();

                using var inputReaderStream = inputPipe.Reader.AsStream();
                using var outputWriterStream = outputPipe.Writer.AsStream();
                await using var transport = new StreamServerTransport(inputReaderStream, outputWriterStream);

                ResoniteMod.Debug("Bulding MCP Server");
                var mcpServer = McpServerBuilder.Build(transport);
                ResoniteMod.DebugFunc(() => $"MCP Server built ${mcpServer}.");

                ResoniteMod.Debug("Starting MCP Server");
                _ = mcpServer.RunAsync(cancellationToken).ConfigureAwait(false);

                ResoniteMod.Debug("Starting input loop");
                var inputLoop = Task.Run(async () =>
                {
                    using var inputStream = inputPipe.Writer.AsStream();
                    var buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0 && !cancellationToken.IsCancellationRequested)
                    {
                        ResoniteMod.Debug($"Received {bytesRead} bytes from {client.Client.RemoteEndPoint}");
                        await inputStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                        await inputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }, cancellationToken);

                ResoniteMod.Debug("Starting output loop");
                var outputLoop = Task.Run(async () =>
                {
                    using var outputStream = outputPipe.Reader.AsStream();
                    var buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await outputStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0 && !cancellationToken.IsCancellationRequested)
                    {
                        ResoniteMod.Debug($"Sending {bytesRead} bytes to {client.Client.RemoteEndPoint}");
                        await networkStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                    }
                }, cancellationToken);

                await Task.WhenAll(inputLoop, outputLoop).ConfigureAwait(false);
                ResoniteMod.Debug($"Client {client.Client.RemoteEndPoint} communication loops completed");
                await mcpServer.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ResoniteMod.Warn($"Error while handling client {client.Client.RemoteEndPoint}: {ex.Message}");
                throw;
            }
            finally
            {
                client.Close();
                ResoniteMod.Msg($"MCP client {client.Client.RemoteEndPoint} disconnected.");
            }
        }

        public void Stop()
        {
            ResoniteMod.Msg("Stopping TCP server...");
            _listener.Stop();
        }
    }
}