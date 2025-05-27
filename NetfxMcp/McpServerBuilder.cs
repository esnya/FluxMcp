using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;

namespace NetfxMcp;

/// <summary>
/// Provides functionality to build MCP servers with tool discovery and registration.
/// </summary>
public static class McpServerBuilder
{
    /// <summary>
    /// Builds an MCP server with tools discovered from the specified assembly.
    /// </summary>
    /// <param name="logger">The logger instance to use for logging.</param>
    /// <param name="transport">The transport layer for communication.</param>
    /// <param name="toolsAssembly">The assembly to scan for MCP tools.</param>
    /// <returns>A configured MCP server instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public static IMcpServer Build(ILogger logger, ITransport transport, Assembly toolsAssembly)
    {
        if (logger is null)
        {
            throw new ArgumentNullException(nameof(logger));
        }
        if (transport is null)
        {
            throw new ArgumentNullException(nameof(transport));
        }
        if (toolsAssembly is null)
        {
            throw new ArgumentNullException(nameof(toolsAssembly));
        }

        logger.LogDebug("Starting to build MCP Server");

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

                logger.LogDebug("Adding tool from method {Method} in type {Type}", method.Name, type.FullName);
                if (!method.IsStatic)
                {
                    logger.LogWarning("Skipping non-static method {Method} in type {Type}", method.Name, type.FullName);
                    continue;
                }

                try
                {
                    var tool = McpServerTool.Create(method);
                    toolCollection.Add(tool);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Error creating tool for method {Method} in type {Type}", method.Name, type.FullName);
                    throw;
                }
            }
        }

        logger.LogDebug("Finished collecting tools for MCP Server");

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

        logger.LogDebug("Creating MCP Server with collected options");
        return McpServerFactory.Create(transport, options);
    }
}

