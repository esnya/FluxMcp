using HarmonyLib;
using MonkeyLoader.Patching;
using MonkeyLoader.Resonite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FluxMcp
{
    [HarmonyPatch("SomeType", "SomeMethod")]
    [HarmonyPatchCategory(nameof(McpServer))]
    internal sealed class McpServer : ResoniteMonkey<McpServer>
    {
        private IHost? _mcpHost;

        protected override void OnEngineReady()
        {
            base.OnEngineReady();
            var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
            builder.Logging.AddConsole(opts => opts.LogToStandardErrorThreshold = LogLevel.Information);
            builder.Services.AddSingleton<NodeManager>();
            builder.Services.AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly();
            _mcpHost = builder.Build();
            _ = _mcpHost.StartAsync();
        }

        protected override void OnShutdown(bool isReload)
        {
            base.OnShutdown(isReload);
            if (_mcpHost != null)
            {
                _mcpHost.StopAsync().GetAwaiter().GetResult();
                _mcpHost.Dispose();
                _mcpHost = null;
            }
        }

        // The options for these should be provided by your game's game pack.
        protected override IEnumerable<IFeaturePatch> GetFeaturePatches() => Enumerable.Empty<IFeaturePatch>();

        private static void Postfix()
        {
            Logger.Info(() => "Postfix for SomeType.SomeMethod()!");
        }
    }
}