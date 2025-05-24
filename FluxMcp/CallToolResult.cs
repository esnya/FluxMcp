using Microsoft.Extensions.AI;

namespace FluxMcp;

public readonly record struct CallToolResult(bool IsError, AIContent Content)
{
    public static CallToolResult Success(AIContent content) => new(false, content);
    public static CallToolResult Error(AIContent content) => new(true, content);
}
