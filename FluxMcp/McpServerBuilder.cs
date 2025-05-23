using ModelContextProtocol.Server;
using System;
using System.IO.Pipelines;
using System.Reflection;

namespace FluxMcp;

internal static class McpServerBuilder
{
    public static IMcpServer Build(PipeReader reader, PipeWriter writer)
    {
        var transport = new StreamServerTransport(reader.AsStream(), writer.AsStream());

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

                var tool = method.IsStatic
                    ? McpServerTool.Create(method)
                    : McpServerTool.Create(method, _ => Activator.CreateInstance(type)!);

                toolCollection.Add(tool);
            }
        }

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

        return McpServerFactory.Create(transport, options);
    }
}

