using System;
using ResoniteModLoader;
using NetfxMcp;

namespace FluxMcp;

/// <summary>
/// Logger implementation that bridges NetfxMcp logging interface to Resonite mod logging.
/// </summary>
public class ResoniteLogger : INetfxMcpLogger
{
    /// <inheritdoc />
    public void Debug(string message) => ResoniteMod.Debug(message);
    /// <inheritdoc />
    public void Warn(string message) => ResoniteMod.Warn(message);
    /// <inheritdoc />
    public void Msg(string message) => ResoniteMod.Msg(message);
    /// <inheritdoc />
    public void DebugFunc(Func<string> messageFunc) => ResoniteMod.DebugFunc(messageFunc);
}

