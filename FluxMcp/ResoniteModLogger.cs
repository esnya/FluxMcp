using System;
using ResoniteModLoader;
using NetfxMcp;

namespace FluxMcp;

public class ResoniteLogger : INetfxMcpLogger
{
    public void Debug(string message) => ResoniteMod.Debug(message);
    public void Warn(string message) => ResoniteMod.Warn(message);
    public void Msg(string message) => ResoniteMod.Msg(message);
    public void DebugFunc(Func<string> messageFunc) => ResoniteMod.DebugFunc(messageFunc);
}

