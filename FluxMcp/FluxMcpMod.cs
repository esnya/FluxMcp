using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
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

    private static ModConfiguration? _config;
    private static readonly Harmony _harmony = new(HarmonyId); // Made read-only to resolve IDE0044


    private static readonly ModConfigurationKey<string> _hostUrlKey = new ModConfigurationKey<string>("Host Binding URL");
    

    public static FluxMcpMod? Instance { get; private set; } // Resolved CA2211 by making Instance private set

    // Delegate for HotReloader registration
    public Action<ResoniteMod>? RegisterHotReloadAction { get; set; } = mod =>
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

        _server = new McpSseServerHost(new Uri(_config?.GetValue(_hostUrlKey) ?? "http://localhost:5000/", UriKind.Absolute)); // Ensured proper Uri conversion
        _serverTask = _server.StartAsync();
        _serverTask.GetAwaiter().GetResult();

#if DEBUG
        RegisterHotReloadAction?.Invoke(this);
#endif
    }

    public static void Shotdown() // Marked as static to resolve CA1822
    {
        _server?.Stop();
        _server = null;
        _serverTask = null;
        Instance = null;
    }

    private static void Init(ResoniteMod modInstance)
    {
        _harmony.PatchAll();
        _config = modInstance?.GetConfiguration();
    }

#if DEBUG
    public static void BeforeHotReload()
    {
        _server?.Stop();
        _harmony.UnpatchAll(HarmonyId);
    }

    public static void OnHotReload(ResoniteMod modInstance)
    {
        Init(modInstance);
    }
#endif
}
