# Development Notes

- Keep source code and comments in English.
- Remove unnecessary comments when editing files.
- Manage the lifecycle of servers carefully:
- Stop the SSE server when the engine shuts down and clear static references.
- Use Conventional Commit messages with an emoji.
- Always run `dotnet test FluxMcp.sln` before committing.
- Build and test commands run offline once packages are restored. Restoring new packages requires network access.
- Avoid splitting property values containing paths with spaces across lines, as it can cause path resolution failures.
- Use `.editorconfig` for configuring compiler warnings and code style settings across the entire solution.

## Build Configurations

FluxMcp uses separate build configurations for different environments:

- **Debug/Release**: For local development with real Resonite assemblies installed
- **StubDebug/StubRelease**: For CI environments without real Resonite assemblies
  
The solution automatically selects the appropriate assembly references based on the configuration:
- Debug/Release configurations reference real Resonite assemblies when available
- StubDebug/StubRelease configurations always use ResoniteStubs project for stub implementations

This eliminates the need for complex conditional logic and makes builds predictable across different environments.

## MCP Tool Documentation

- **MCP Schema**: Only the `Description` attribute content is reflected in the MCP tool schema that AI agents see.
- **XML Documentation**: XML doc comments (`/// <summary>`) are for C# IntelliSense and developer documentation only.
- **Best Practice**: Use both - `Description` attribute for AI agents, XML docs for developers.


## Planned .NET Upgrade

FluxMcp currently targets .NET Framework 4.7.2. A move to .NET 9 is planned once the runtime stabilizes. Dependencies and build steps will change accordingly, so keep an eye on this repository for updated instructions.
