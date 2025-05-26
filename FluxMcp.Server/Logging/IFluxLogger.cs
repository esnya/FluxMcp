using System;
namespace FluxMcp;

public interface IFluxLogger
{
    void Debug(string message);
    void Warn(string message);
    void Msg(string message);
    void DebugFunc(Func<string> messageFunc);
}

