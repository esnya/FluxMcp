using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using ResoniteModLoader;
using System;
using System.Reflection;

namespace NetfxMcp;

public static class McpServerBuilder
{
    public static IMcpServer Build(INetfxMcpLogger logger, ITransport transport, Assembly toolsAssembly)
    {
        logger.Debug("Starting to build MCP Server");

        var toolCollection = new McpServerPrimitiveCollection<McpServerTool>();
        foreach (var type in toolsAssembly.GetTypes())
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

                logger.Debug($"Adding tool from method {method.Name} in type {type.FullName}");
                if (!method.IsStatic)
                {
                    logger.Warn($"Skipping non-static method {method.Name} in type {type.FullName}");
                    continue;
                }

                try
                {
                    var tool = McpServerTool.Create(method);
                    toolCollection.Add(tool);
                }
                catch (Exception ex)
                {
                    logger.Warn($"Error creating tool for method {method.Name} in type {type.FullName}: {ex.Message}");
                    throw;
                }
            }
        }

        logger.Debug("Finished collecting tools for MCP Server");

        var options = new McpServerOptions
        {
            Capabilities = new()
            {
                Tools = new()
                {
                    ToolCollection = toolCollection
                }
            },
        };

        logger.Debug("Creating MCP Server with collected options");
        return McpServerFactory.Create(transport, options);
    }
}

