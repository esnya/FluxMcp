using System;
using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace MonkeyLoader.ModTemplate
{
    [McpServerToolType]
    public static class NodeTools
    {
        [McpServerTool(Name = "createNode"), Description("Creates a new node with the specified name and type.")]
        public static Guid CreateNode(NodeManager manager, string name, string type)
        {
            var node = manager.CreateNode(name, type);
            return node.Id;
        }

        [McpServerTool(Name = "findNodes"), Description("Finds nodes whose names contain the specified substring.")]
        public static IEnumerable<NodeInfo> FindNodes(NodeManager manager, string name)
        {
            foreach (var node in manager.FindNodes(name))
                yield return new NodeInfo(node.Id, node.Name, node.Type);
        }

        [McpServerTool(Name = "getOutputDisplay"), Description("Gets the output display for the specified node.")]
        public static string GetOutputDisplay(NodeManager manager, Guid nodeId) =>
            manager.GetOutputDisplay(nodeId) ?? string.Empty;

        [McpServerTool(Name = "getInputFields"), Description("Gets the input fields for the specified node.")]
        public static IReadOnlyList<string> GetInputFields(NodeManager manager, Guid nodeId) =>
            manager.GetInputFields(nodeId);

        [McpServerTool(Name = "getOutputFields"), Description("Gets the output fields for the specified node.")]
        public static IReadOnlyList<string> GetOutputFields(NodeManager manager, Guid nodeId) =>
            manager.GetOutputFields(nodeId);

        [McpServerTool(Name = "connectInput"), Description("Connects an input field of a node to a target node.")]
        public static void ConnectInput(NodeManager manager, Guid nodeId, string inputField, Guid targetNodeId) =>
            manager.ConnectInput(nodeId, inputField, targetNodeId);

        [McpServerTool(Name = "connectOutput"), Description("Connects an output field of a node to a target node.")]
        public static void ConnectOutput(NodeManager manager, Guid nodeId, string outputField, Guid targetNodeId) =>
            manager.ConnectOutput(nodeId, outputField, targetNodeId);

        [McpServerTool(Name = "getConnection"), Description("Gets the current connection for the specified node field.")]
        public static Guid? GetConnection(NodeManager manager, Guid nodeId, string field) =>
            manager.GetCurrentConnection(nodeId, field);

        [McpServerTool(Name = "triggerCall"), Description("Increments the call count for the specified node.")]
        public static void TriggerCall(NodeManager manager, Guid nodeId) =>
            manager.TriggerCall(nodeId);

        [McpServerTool(Name = "triggerDynamicImpulse"), Description("Increments the impulse count for the specified node.")]
        public static void TriggerDynamicImpulse(NodeManager manager, Guid nodeId) =>
            manager.TriggerDynamicImpulse(nodeId);
    }

    public record NodeInfo(Guid id, string name, string type);
}
