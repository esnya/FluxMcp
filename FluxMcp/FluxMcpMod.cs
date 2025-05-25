using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using ResoniteModLoader;
using System.Threading;


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
    private static readonly ModConfigurationKey<bool> _enabledKey = new ModConfigurationKey<bool>(
        "Enabled",
        computeDefault: () => true);

    [AutoRegisterConfigKey]
    private static readonly ModConfigurationKey<string> _bindAddressKey = new ModConfigurationKey<string>("Bind address", computeDefault: () => "127.0.0.1");

    [AutoRegisterConfigKey]
    private static readonly ModConfigurationKey<int> _portKey = new ModConfigurationKey<int>("Listen port", computeDefault: () => 5000);

    private static CancellationTokenSource? _cts;
    private static Task? _serverTask;
    public static Action<ResoniteMod>? RegisterHotReloadAction { get; set; } = mod =>
    {
#if DEBUG
        HotReloader.RegisterForHotReload(mod);
#endif
    };

    public static bool IsServerRunning => _httpServer?.IsRunning ?? false;


    private static McpHttpStreamingServer? _httpServer;

    private static void RestartServer()
    {
        StopHttpServer();

        if (_config?.GetValue(_enabledKey) != false)
        {
            StartHttpServer();
        }
    }

    private static void StartHttpServer()
    {
        if (_httpServer != null)
        {
            return;
        }

        Debug("Creating HTTP streaming server...");
        var bindAddress = _config?.GetValue(_bindAddressKey) ?? "127.0.0.1";
        var port = _config?.GetValue(_portKey) ?? 5000;

        _httpServer = new McpHttpStreamingServer(transport => LocalMcpServerBuilder.Build(transport), $"http://{bindAddress}:{port}/");

        Debug("Starting HTTP streaming server...");
        _cts = new CancellationTokenSource();
        _serverTask = Task.Run(() => _httpServer.StartAsync(_cts.Token));
    }

    private static void StopHttpServer()
    {
        if (_httpServer == null)
        {
            return;
        }

        try
        {
            _httpServer.Stop();
            _cts?.Cancel();
            _serverTask?.GetAwaiter().GetResult();
            _cts?.Dispose();
            _httpServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        finally
        {
            _cts = null;
            _serverTask = null;
            _httpServer = null;
        }
    }

    public override void OnEngineInit()
    {
        Init(this);
    }

    private static void Init(ResoniteMod modInstance)
    {
#if DEBUG
        RegisterHotReloadAction?.Invoke(modInstance);
#endif

        _config = modInstance?.GetConfiguration();
        Debug($"Config initialized: {_config != null}");

        _enabledKey.OnChanged += value =>
        {
            if (value is bool enabled)
            {
                if (enabled)
                {
                    StartHttpServer();
                }
                else
                {
                    StopHttpServer();
                }
            }
        };

        _bindAddressKey.OnChanged += _ => RestartServer();
        _portKey.OnChanged += _ => RestartServer();

        if (_config?.GetValue(_enabledKey) != false)
        {
            StartHttpServer();
        }
    }

#if DEBUG
    public static void BeforeHotReload()
    {
        StopHttpServer();
    }

    public static void OnHotReload(ResoniteMod modInstance)
    {
        Init(modInstance);
    }
#endif
}
