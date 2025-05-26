using System;
namespace NetfxMcp;

public interface INetfxMcpLogger
{
    void Debug(string message);
    void Warn(string message);
    void Msg(string message);
    void DebugFunc(Func<string> messageFunc);
}

