using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
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

    [AutoRegisterConfigKey]
    private static readonly ModConfigurationKey<string> _bindAddressKey = new ModConfigurationKey<string>("Bind adderess", computeDefault:  () => "127.0.0.1");

    [AutoRegisterConfigKey]
    private static readonly ModConfigurationKey<int> _portKey = new ModConfigurationKey<int>("Listen port", computeDefault: () => 5000);
    

    public Action<ResoniteMod>? RegisterHotReloadAction { get; set; } = mod =>
    {
#if DEBUG
        HotReloader.RegisterForHotReload(mod);
#endif
    };

    private static TcpTransport? _tcpServer;

    public override void OnEngineInit()
    {
        Init(this);
#if DEBUG
        RegisterHotReloadAction?.Invoke(this);
#endif
    }

    private static void Init(ResoniteMod modInstance)
    {
        _config = modInstance?.GetConfiguration();
        Debug("Starting TCP server...");
        _tcpServer = new TcpTransport(_config?.GetValue(_bindAddressKey) ?? "127.0.0.1", _config?.GetValue(_portKey) ?? 5000);
        Task.Run(() => _tcpServer.StartAsync(default));
    }

#if DEBUG
    public static void BeforeHotReload()
    {
        _tcpServer?.Stop();
    }

    public static void OnHotReload(ResoniteMod modInstance)
    {
        Init(modInstance);
    }
#endif
}
