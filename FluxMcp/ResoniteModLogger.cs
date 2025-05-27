using System;
using ResoniteModLoader;
using Microsoft.Extensions.Logging;

namespace FluxMcp;

/// <summary>
/// Logger implementation that bridges NetfxMcp logging interface to Resonite mod logging.
/// </summary>
public class ResoniteLogger : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        switch (logLevel)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
                if (exception is null)
                {
                    ResoniteMod.Debug(message);
                }
                else
                {
                    ResoniteMod.Debug($"{message}: {exception.Message}");
                }
                break;
            case LogLevel.Information:
                ResoniteMod.Msg(message);
                break;
            default:
                ResoniteMod.Warn(message);
                break;
        }
    }
}

