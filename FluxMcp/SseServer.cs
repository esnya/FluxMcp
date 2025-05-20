// Startup.cs
using FrooxEngine;
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
    /// MCP SSE サーバー (OWIN Self-Host) を明示的に起動／停止するヘルパー。
    /// </summary>
    public sealed class McpSseServerHost : IDisposable
    {
        private readonly string _url;
        private IDisposable? _listener;       // WebApp.Start が返すハンドル
        private IHost? _host;
        private int _started;                 // 二重起動防止

        public bool IsRunning => Volatile.Read(ref _started) == 1;

        public McpSseServerHost(string url = "http://localhost:5000/")
        {
            _url = url.EndsWith("/") ? url : url + "/";
        }

        /// <summary>非同期 (ノンブロッキング) でサーバーを起動。</summary>
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

        /// <summary>即時停止。再利用したい場合は StartAsync を再度呼び出す。</summary>
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
            catch (Exception ex)
            {
                ResoniteMod.Error($"Error stopping server: {ex.Message}");
            }
        }

        public void Dispose() => Stop();
    }


    public sealed class Startup
    {
        public IHost? HostInstance { get; private set; }
        public (Pipe, Pipe)? PipeSet;

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

                    // SSE クライアントへ最初に endpoint 通知
                    await res.WriteAsync("event: endpoint\ndata: /message\n\n").ConfigureAwait(false);
                    await res.Body.FlushAsync().ConfigureAwait(false);

                    // ② MCP → HTTP 方向のストリームを読み取り ⇒ SSE へ流す
                    using var reader = new StreamReader(PipeSet.Value.Item2.Reader.AsStream());
                    _ = Task.Run(async () =>
                    {
                        while (!token.IsCancellationRequested)
                        {
                            string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                            if (line == null) break;

                            // ここでは 1 行＝1 JSON-RPC 返信 という想定
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
                    // ③ HTTP から届いた JSON を duplexPipe に書き込むだけ
                    byte[] bytes = Encoding.UTF8.GetBytes(json + "\n");
                    await PipeSet.Value.Item1.Writer.WriteAsync(bytes, ctx.Request.CallCancelled).ConfigureAwait(false);

                    // ここでは即 202 Accepted を返す（返答は SSE 側で受信)
                    ctx.Response.StatusCode = 202;
                });
            });
        }
    }
}
