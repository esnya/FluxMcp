using System;

namespace NetfxMcp;

/// <summary>
/// Defines a logger interface for NetfxMcp framework.
/// </summary>
public interface INetfxMcpLogger
{
    /// <summary>
    /// Logs a debug message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Debug(string message);
    /// <summary>
    /// Logs a warning message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Warn(string message);
    /// <summary>
    /// Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void Msg(string message);
    /// <summary>
    /// Logs a debug message using a deferred function call.
    /// </summary>
    /// <param name="messageFunc">A function that returns the message to log.</param>
    void DebugFunc(Func<string> messageFunc);
}

