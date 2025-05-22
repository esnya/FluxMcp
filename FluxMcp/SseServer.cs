// Startup.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Owin;
using Microsoft.Owin.Hosting;
using Owin;
using ResoniteModLoader; // Add this import for logging
using System;
using System.IO; 
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[assembly: OwinStartup(typeof(FluxMcp.Startup))]

namespace FluxMcp
{
    /// <summary>
    /// Helper to explicitly start and stop the MCP SSE server (OWIN self-host).
    /// </summary>
    public sealed class McpSseServerHost : IDisposable
    {
        private readonly string _url; // Changed to readonly to resolve CA1051
        private IDisposable? _listener; // Reverted readonly to fix CS0191
        private IHost? _host;
        private int _started;                 // Prevents double start

        public bool IsRunning => Volatile.Read(ref _started) == 1;

        public McpSseServerHost(Uri url)
        {
            if (url == null) throw new ArgumentNullException(nameof(url)); // Updated parameter type to Uri for CA1054

            _url = url.ToString().EndsWith("/", StringComparison.Ordinal) ? url.ToString() : url + "/"; // Added StringComparison.Ordinal for CA1310
        }

        /// <summary>Start the server asynchronously.</summary>
        public async Task StartAsync(CancellationToken token = default)
        {
            if (Interlocked.Exchange(ref _started, 1) == 1)
                throw new InvalidOperationException("Server already started.");

            try
            {
                var startup = new Startup();
                _listener = WebApp.Start(_url, startup.Configuration);
                _host = startup.HostInstance;
                if (_host != null)
                {
                    await _host.StartAsync(token).ConfigureAwait(false);
                }
                ResoniteMod.Debug($"Server started at {_url}");
            }
            catch (Exception ex)
            {
                ResoniteMod.Error($"Error starting server: {ex.Message}");
                throw;
            }
        }

        /// <summary>Stop immediately; call StartAsync again to reuse.</summary>
        public void Stop()
        {
            if (Interlocked.Exchange(ref _started, 0) == 0) return;

            try
            {
                if (_host != null)
                {
                    _host.StopAsync().GetAwaiter().GetResult();
                    _host.Dispose();
                    _host = null;
                }
                _listener?.Dispose();
                _listener = null;
                ResoniteMod.Debug("Server stopped.");
            }
            catch (ObjectDisposedException ex)
            {
                ResoniteMod.Error($"Error stopping server: {ex.Message}"); // Catching specific exception for CA1031
            }
        }

        public void Dispose() => Stop();
    }


    public sealed class Startup
    {
        public IHost? HostInstance { get; private set; }
        public (Pipe, Pipe)? PipeSet { get; private set; }

        public void Configuration(IAppBuilder app)
        {
            PipeSet = (new Pipe(), new Pipe());

            var builder = Host.CreateApplicationBuilder();

            builder.Services
                    .AddMcpServer()
                    .WithToolsFromAssembly()
                    .WithStreamServerTransport(
                        PipeSet.Value.Item1.Reader.AsStream(),   // inbound  (HTTP → MCP)
                        PipeSet.Value.Item2.Writer.AsStream())  // outbound (MCP → HTTP)
                    ;

            HostInstance = builder.Build();

            app.Map("/mcp", map =>
            {
                map.Run(async ctx =>
                {
                    var res = ctx.Response;
                    var token = ctx.Request.CallCancelled;

                    res.ContentType = "text/event-stream";
                    res.Headers.Set("Cache-Control", "no-cache");
                    res.Headers.Set("Access-Control-Allow-Origin", "*");

                    // Notify the SSE client of the endpoint first
                    await res.WriteAsync("event: endpoint\ndata: /message\n\n").ConfigureAwait(false);
                    await res.Body.FlushAsync().ConfigureAwait(false);

                    // Forward the MCP to HTTP stream into SSE
                    using var reader = new StreamReader(PipeSet.Value.Item2.Reader.AsStream());
                    _ = Task.Run(async () =>
                    {
                        while (!token.IsCancellationRequested)
                        {
                            string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                            if (line == null) break;

                            // Each line is expected to be one JSON-RPC response
                            await res.WriteAsync($"data: {line}\n\n").ConfigureAwait(false);
                            await res.Body.FlushAsync().ConfigureAwait(false);
                        }
                    }, token);

                    /* keep-alive */
                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(15000, token).ConfigureAwait(false);
                        await res.WriteAsync($": keep-alive {DateTime.UtcNow:o}\n\n").ConfigureAwait(false);
                        await res.Body.FlushAsync().ConfigureAwait(false);
                    }
                });
            });

            /* --- /message (HTTP → MCP) --- */
            app.Map("/message", map =>
            {
                map.Run(async ctx =>
                {
                    string json = await new StreamReader(ctx.Request.Body).ReadToEndAsync().ConfigureAwait(false);
                    // Write JSON from HTTP into the duplex pipe
                    byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");
                    await PipeSet.Value.Item1.Writer.WriteAsync(bytes, ctx.Request.CallCancelled).ConfigureAwait(false);

                    // Immediately return 202 Accepted; response arrives via SSE
                    ctx.Response.StatusCode = 202;
                });
            });
        }
    }
}
