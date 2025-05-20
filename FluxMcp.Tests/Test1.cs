using FluxMcp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using FrooxEngine;
using System.Reflection;
using Elements.Core;
using ModelContextProtocol.Client;
using ResoniteModLoader;
using ResoniteHotReloadLib;

namespace FluxMcp.Tests;

[TestClass]
public sealed class McpServerTests
{

    [TestMethod]
    public async Task McpServer_ShouldRespondToClient_WithSseClientTransport()
    {
        // Arrange
        var mod = new FluxMcpMod();
        FluxMcpMod.RegisterHotReloadAction = null;

        // Intercept and set FinishedLoading for testing purposes
        typeof(ResoniteModBase).GetProperty("FinishedLoading", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(mod, true);

        // Initialize the mod
        mod.OnEngineInit();

        // Set up SseClientTransport
        var clientTransport = new SseClientTransport(new SseClientTransportOptions
        {
            Name = "Everything",
            Endpoint = new Uri("http://localhost:5000/mcp")
        });

        // Act
        var client = await McpClientFactory.CreateAsync(clientTransport);

        // Assert server tools
        var tools = await client.ListToolsAsync();
        Console.WriteLine("Available tools: " + string.Join(", ", tools.Select(t => t.Name)));
        Assert.IsNotNull(tools, "The server should return a list of tools.");
        Assert.IsTrue(tools.Any(), "The server should have at least one tool available.");

        // Act and Assert tool execution
        var result = await client.CallToolAsync(
            "getNodeReference", // Changed tool name from 'echo' to 'getNodeReference'
            new Dictionary<string, object?> { ["nodeId"] = "exampleNodeId" },
            cancellationToken: CancellationToken.None);

        var referenceResult = result.Content.FirstOrDefault(c => c.Type == "text")?.Text;
        Assert.IsNotNull(referenceResult, "The getNodeReference tool should return a valid reference.");
    }
}
