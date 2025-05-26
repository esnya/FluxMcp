using FrooxEngine.ProtoFlux;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Elements.Core;

namespace FluxMcp.Tools;

[McpServerToolType]
public static class NodeValueTools
{
    [McpServerTool(Name = "getInputNodeValue"), Description("Gets the current value of an input node. Use this to read values from input nodes like ValueInput<T>.")]
    public static object? GetInputNodeValue(string nodeRefId)
    {
        return NodeToolHelpers.Handle(() =>
        {
            var node = NodeLookupTools.FindNodeInternal(nodeRefId);
            if (node is not IInput inputNode)
            {
                throw new InvalidOperationException($"Node {nodeRefId} is not an input node.");
            }
            return inputNode.BoxedValue;
        });
    }

    [McpServerTool(Name = "setInputNodeValue"), Description("Sets the value of an input node. Automatically handles type conversion for basic types like float, int, bool, and vectors. Use this to configure input nodes with specific values.")]
    public static async Task<object?> SetInputNodeValue(string nodeRefId, JsonElement value)
    {
        return await NodeToolHelpers.HandleAsync(async () =>
        {
            return await NodeToolHelpers.UpdateAction(NodeToolHelpers.WorkspaceSlot, () =>
            {
                var node = NodeLookupTools.FindNodeInternal(nodeRefId);
                if (node is not IInput inputNode)
                {
                    throw new InvalidOperationException($"Node {nodeRefId} is not an input node.");
                }
                var targetType = inputNode.InputType();

                try
                {
                    if (targetType == typeof(colorX) && !value.TryGetProperty("profile", out var _))
                    {
                        inputNode.BoxedValue = (colorX)value.Deserialize<color>(NodeToolHelpers.JsonOptions);
                    }
                    else
                    {
                        inputNode.BoxedValue = value.Deserialize(targetType, NodeToolHelpers.JsonOptions)!;
                    }
                }
                catch (InvalidCastException)
                {
                    throw new InvalidOperationException($"Cannot set input value of {value} into {targetType}");
                }

                return inputNode.BoxedValue;
            }).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
