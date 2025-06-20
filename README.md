# FluxMcp
A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader) mod for [Resonite](https://resonite.com/) that adds an MCP (Model Context Protocol) server to handle ProtoFlux for AI integration. 

## Installation

1. Install the [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
1. Download the latest release from the [Releases page](https://github.com/esnya/FluxMcp/releases).
1. Place `FluxMcp.dll` into your `rml_mods` folder. This folder should be located at `C:\Program Files (x86)\Steam\steamapps\common\Resonite\rml_mods` for a standard installation. You can create it if it's missing, or if you start the game once with the ResoniteModLoader installed it will create this folder for you.
1. Extract `rml_libs.zip` and place the `rml_libs` folder in your Resonite installation directory (typically `C:\Program Files (x86)\Steam\steamapps\common\Resonite`).
1. Launch the game. If you want to check that the mod is working you can check your Resonite logs.

## Configuration

`Enabled` determines whether the TCP server is started. This value defaults to `true` and can be toggled at runtime.
`Bind address` and `Listen port` control where the TCP server listens. Changes to these settings restart the server automatically.


## Development Requirements

For development, you will need the [ResoniteHotReloadLib](https://github.com/Nytra/ResoniteHotReloadLib) to be able to hot reload your mod with DEBUG build.
