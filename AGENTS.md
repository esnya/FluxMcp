# Development Notes

- Keep source code and comments in English.
- Remove unnecessary comments when editing files.
- Manage the lifecycle of servers carefully:
- Stop the SSE server when the engine shuts down and clear static references.
- Use Conventional Commit messages with an emoji.
- Always run `dotnet test NetfxMcp.Tests/NetfxMcp.Tests.csproj` before committing.
- Build and test commands run offline once packages are restored. Restoring new packages requires network access.
- Avoid splitting property values containing paths with spaces across lines, as it can cause path resolution failures.

## MCP Tool Documentation

- **MCP Schema**: Only the `Description` attribute content is reflected in the MCP tool schema that AI agents see.
- **XML Documentation**: XML doc comments (`/// <summary>`) are for C# IntelliSense and developer documentation only.
- **Best Practice**: Use both - `Description` attribute for AI agents, XML docs for developers.
