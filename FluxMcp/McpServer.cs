using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using ResoniteModLoader;

#if DEBUG
using ResoniteHotReloadLib;
#endif

namespace FluxMcp;

public partial class McpServer : ResoniteMod
{
    private static Assembly ModAssembly => typeof(McpServer).Assembly;

    public override string Name => ModAssembly.GetCustomAttribute<AssemblyTitleAttribute>()!.Title;
    public override string Author => ModAssembly.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company;
    public override string Version => ModAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
    public override string Link => ModAssembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(meta => meta.Key == "RepositoryUrl").Value;

    internal static string HarmonyId => $"com.nekometer.esnya.{ModAssembly.GetName().Name}";

    private static ModConfiguration? config;
    private static readonly Harmony harmony = new(HarmonyId);

    private IHost? _mcpHost;

    public override void OnEngineInit()
    {
        Init(this);

        var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
        builder.Logging.AddConsole(opts => opts.LogToStandardErrorThreshold = LogLevel.Information);
        builder.Services.AddSingleton<NodeManager>();
        builder.Services.AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();
        _mcpHost = builder.Build();
        _ = _mcpHost.StartAsync();

#if DEBUG
        HotReloader.RegisterForHotReload(this);
#endif
    }

    private static void Init(ResoniteMod modInstance)
    {
        harmony.PatchAll();
        config = modInstance?.GetConfiguration();
    }

#if DEBUG
    public static void BeforeHotReload()
    {
        harmony.UnpatchAll(HarmonyId);
    }

    public static void OnHotReload(ResoniteMod modInstance)
    {
        Init(modInstance);
    }
#endif
}
