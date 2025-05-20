using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace FluxMcp
{
    public class SimpleHttpServer
    {
        private readonly HttpListener _listener;
        private bool _isRunning;
        private readonly IHost _mcpHost;

        public SimpleHttpServer(string prefix, IHost mcpHost)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _mcpHost = mcpHost;
        }

        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _listener.Start();
            Task.Run(() => HandleRequests());
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _listener.Stop();
            _listener.Close();
        }

        private async Task HandleRequests()
        {
            while (_isRunning)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    var request = context.Request;
                    var response = context.Response;

                    // Check if the request is for SSE
                    if (request.Headers["Accept"] == "text/event-stream")
                    {
                        await HandleSseConnection(response);
                    }
                    else
                    {
                        // Forward non-SSE requests to mcpHost
                        await HandleHttpRequestWithMcpHost(request, response);
                    }
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped, exit the loop
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling request: {ex}");
                }
            }
        }

        private async Task HandleSseConnection(HttpListenerResponse response)
        {
            response.ContentType = "text/event-stream";
            response.Headers["Cache-Control"] = "no-cache";
            response.Headers["Connection"] = "keep-alive";

            using var writer = new StreamWriter(response.OutputStream, Encoding.UTF8);

            // Example: Send a message every second
            for (int i = 0; i < 10; i++)
            {
                await writer.WriteLineAsync($"data: Message {i}\n");
                await writer.FlushAsync();
                await Task.Delay(1000);
            }
        }

        private async Task HandleHttpRequestWithMcpHost(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                // Example: Use mcpHost to process the request
                var responseString = "Handled by mcpHost";

                // Integrate mcpHost logic
                if (_mcpHost != null)
                {
                    // Example: Call a service or method from mcpHost
                    // This is a placeholder for actual integration logic
                    responseString = "McpHost processed the request.";
                }

                var buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing request with mcpHost: {ex}");
                response.StatusCode = 500;
                response.Close();
            }
        }
    }
}