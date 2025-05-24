using Microsoft.Extensions.AI;

namespace FluxMcp;

public class CallToolResult
{
    public bool IsError { get; private set; }
    public AIContent Content { get; private set; }

    public CallToolResult(bool isError, AIContent content)
    {
        IsError = isError;
        Content = content;
    }

    public static CallToolResult Success(AIContent content) => new CallToolResult(false, content);
    public static CallToolResult Error(AIContent content) => new CallToolResult(true, content);
}
