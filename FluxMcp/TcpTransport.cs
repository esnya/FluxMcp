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

        private (Pipe, Pipe) PipeSet { get; } = (new Pipe(), new Pipe());

        public TcpTransport(string ipAddress, int port)
        {
            _listener = new TcpListener(IPAddress.Parse(ipAddress), port);
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _listener.Start();
            Console.WriteLine($"TCP server started on {_listener.LocalEndpoint}");

            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            ResoniteMod.Msg($"Client {client.Client.RemoteEndPoint} connected");

            using var networkStream = client.GetStream();

            var mcpServer = McpServerBuilder.Build(
                PipeSet.Item1.Reader,
                PipeSet.Item2.Writer);
            _ = mcpServer.RunAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                var inputLoop = Task.Run(async () =>
                {
                    var buffer = new byte[8192];
                    var writer = PipeSet.Item1.Writer.AsStream();
                    int bytesRead;
                    while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0 && !cancellationToken.IsCancellationRequested)
                    {
                        await writer.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }, cancellationToken);

                var outputLoop = Task.Run(async () =>
                {
                    var buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await PipeSet.Item2.Reader.AsStream().ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0 && !cancellationToken.IsCancellationRequested)
                    {
                        await networkStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                    }
                }, cancellationToken);

                await Task.WhenAll(inputLoop, outputLoop).ConfigureAwait(false);
            }
            finally
            {
                client.Close();
                await mcpServer.DisposeAsync().ConfigureAwait(false);
                ResoniteMod.Msg("MCP client disconnected."); 
            }
        }

        public void Stop()
        {
            _listener.Stop();
            ResoniteMod.Msg("TCP server stopped.");
        }
    }
}