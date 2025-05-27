using System;
using ResoniteModLoader;
using Microsoft.Extensions.Logging;

namespace FluxMcp;

/// <summary>
/// Logger implementation that bridges NetfxMcp logging interface to Resonite mod logging.
/// </summary>
public class ResoniteLogger : ILogger
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        switch (logLevel)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
                if (!ResoniteMod.IsDebugEnabled()) return;
                if (exception is null)
                {
                    ResoniteMod.Debug(formatter(state, exception));
                }
                else
                {
                    ResoniteMod.Debug($"{formatter(state, exception)}: {exception.Message}");
                }
                break;
            case LogLevel.Information:
                ResoniteMod.Msg(formatter(state, exception));
                break;
            default:
                ResoniteMod.Warn(formatter(state, exception));
                break;
        }
    }
}

