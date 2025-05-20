using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.IO;
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

public partial class FluxMcpMod : ResoniteMod
{
    private static Assembly ModAssembly => typeof(FluxMcpMod).Assembly;

    public override string Name => ModAssembly.GetCustomAttribute<AssemblyTitleAttribute>()!.Title;
    public override string Author => ModAssembly.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company;
    public override string Version => ModAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
    public override string Link => ModAssembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(meta => meta.Key == "RepositoryUrl").Value;

    internal static string HarmonyId => $"com.nekometer.esnya.{ModAssembly.GetName().Name}";

    private static ModConfiguration? config;
    private static readonly Harmony harmony = new(HarmonyId);


    private static ModConfigurationKey<string> hostUrlKey = new ModConfigurationKey<string>("Host Binding URL");
    

    public static FluxMcpMod? Instance { get; private set; }

    // Delegate for HotReloader registration
    public static Action<ResoniteMod>? RegisterHotReloadAction = mod =>
    {
#if DEBUG
        HotReloader.RegisterForHotReload(mod);
#endif
    };
    private static McpSseServerHost? _server;
    private static Task? _serverTask;

    public override void OnEngineInit()
    {
        Instance = this; // Ensure Instance is initialized first

        Init(this);

        _server = new McpSseServerHost(config?.GetValue(hostUrlKey) ?? "http://localhost:5000/");
        _serverTask = _server.StartAsync();

#if !DEBUG
        RegisterHotReloadAction?.Invoke(this);
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
        _serverTask?.Dispose();
        _server?.Stop();
        harmony.UnpatchAll(HarmonyId);
    }

    public static void OnHotReload(ResoniteMod modInstance)
    {
        Init(modInstance);
    }
#endif
}
