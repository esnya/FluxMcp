using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Reflection;
using ResoniteModLoader;

namespace FluxMcp;

internal static class LocalMcpServerBuilder
{
    public static IMcpServer Build(ITransport transport)
    {
        ResoniteMod.Debug("Starting to build MCP Server");

        var toolCollection = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var type in typeof(NodeTools).Assembly.GetTypes())
        {
            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() is null)
            {
                continue;
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                if (method.GetCustomAttribute<McpServerToolAttribute>() is null)
                {
                    continue;
                }

                ResoniteMod.Debug($"Adding tool from method {method.Name} in type {type.FullName}");
                if (!method.IsStatic)
                {
                    ResoniteMod.Warn($"Skipping non-static method {method.Name} in type {type.FullName}");
                    continue;
                }

                try
                {
                    var tool = McpServerTool.Create(method);
                    toolCollection.Add(tool);
                }
                catch (Exception ex)
                {
                    ResoniteMod.Warn($"Error creating tool for method {method.Name} in type {type.FullName}: {ex.Message}");
                    throw;
                }
            }
        }

        ResoniteMod.Debug("Finished collecting tools for MCP Server");

        var options = new McpServerOptions
        {
            Capabilities = new()
            {
                Tools = new()
                {
                    ToolCollection = toolCollection
                }
            }
        };

        ResoniteMod.Debug("Creating MCP Server with collected options");
        return McpServerFactory.Create(transport, options);
    }
}

