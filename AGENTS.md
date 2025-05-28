# Development Notes

- Code and comments in English
- Remove unnecessary comments
- Manage server lifecycle (stop SSE server on shutdown; clear static references)
- Use Conventional Commits with emojis (standard gitmoji or custom)
  - Format: `category(optional scope): EMOJI description in EN`
- Run `dotnet test FluxMcp.sln` before committing
- Restore packages once; avoid splitting path-containing properties
- Configure style and warnings via `.editorconfig`
- Centralize reusable logic; avoid premature abstraction

## Build Configurations

- Debug/Release: real Resonite assemblies
- StubDebug/StubRelease: ResoniteStubs for CI
- References auto-selected by configuration

## NuGet Dependency Conflict Resolution

1. Prioritize Resonite assemblies over NuGet
2. Exclude conflicting NuGet packages (`ExcludeAssets="all" PrivateAssets="all"`)
3. Reference Resonite's Managed assemblies directly
4. Use NuGet only for assemblies absent in Resonite

## Documentation Practices

- MCP Schema: AI-visible via `Description` attribute
- Developer docs: use XML comments (`/// <summary>`)
- Combine both for clarity

## Planned Upgrade

- Currently .NET Framework 4.7.2; migrate to .NET 9 when stable
