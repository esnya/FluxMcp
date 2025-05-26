# Development Notes

- Keep source code and comments in English.
- Remove unnecessary comments when editing files.
- Manage the lifecycle of servers carefully:
  - `McpSseServerHost` should keep a reference to the created `IHost` and dispose it when stopping.
  - `SimpleHttpServer` must close its `HttpListener` during shutdown.
- Stop the SSE server when the engine shuts down and clear static references.
- Use Conventional Commit messages with an emoji.
- Always run `dotnet test NetfxMcp.Tests/NetfxMcp.Tests.csproj` before committing.
- Currently package retrieval fails, so building and running tests is not possible.
