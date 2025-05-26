using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using ModelContextProtocol.Server;
using ResoniteModLoader;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace FluxMcp.Tools;

/// <summary>
/// Provides MCP tools for creating and deleting ProtoFlux nodes.
/// </summary>
[McpServerToolType]
public static class NodeCreationTools
{
    /// <summary>
    /// Creates a new ProtoFlux node with the specified type and position.
    /// </summary>
    /// <param name="type">The type of node to create. For generic types, use specific types like &lt;float&gt;, &lt;int&gt;, etc.</param>
    /// <param name="position">The position to place the node (Right, Up, Forward).</param>
    /// <returns>A task representing the asynchronous operation that returns the created node or null if creation failed.</returns>
    [McpServerTool(Name = "createNode"), Description("Creates a new node with the specified name and type. For generic types, use specific types like <float>, <int>, <bool>, <float3>, <color> instead of <T>. Use searchNodeType to find valid node types. Dimension of position: (Right, Up, Forward).")]
    public static async Task<object?> CreateNode(string type, float3 position)
    {
        var result = await NodeToolHelpers.HandleAsync(async () =>
        {
            if (string.IsNullOrEmpty(type))
            {
                ResoniteMod.Warn("Type cannot be null or empty.");
                throw new ArgumentException("Type cannot be null or empty.", nameof(type));
            }

            ValidateGenericTypeFormat(type);

            const string bindingPrefix = "[ProtoFluxBindings]";
            if (!type.StartsWith(bindingPrefix, StringComparison.Ordinal))
            {
                type = bindingPrefix + type;
                ResoniteMod.DebugFunc(() => $"Added binding prefix to type: {type}");
            }
            const string defaultNamespace = "FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.";
            if (!type.StartsWith(bindingPrefix + defaultNamespace, StringComparison.Ordinal))
            {
                var innerType = type.Substring(bindingPrefix.Length);
                type = bindingPrefix + defaultNamespace + innerType;
                ResoniteMod.DebugFunc(() => $"Applied fallback namespace to type: {type}");
            }
            var decodedType = NodeToolHelpers.Types.DecodeType(type);
            ResoniteMod.DebugFunc(() => $"Creating Node {type} -> {decodedType}");
            if (decodedType == null)
            {
                var suggestion = FindClosestNodeType(type);
                if (suggestion != null && type.Contains('<') && type.Contains('>') && suggestion.Contains('<') && suggestion.Contains('>'))
                {
                    var typeGeneric = type.Substring(type.IndexOf('<'), type.LastIndexOf('>') - type.IndexOf('<') + 1);
                    var sugPrefix = suggestion.Substring(0, suggestion.IndexOf('<'));
                    suggestion = sugPrefix + typeGeneric;
                }
                var message = suggestion is null
                    ? $"Invalid type: {type}. Use searchNodeType or listNodeTypes to find valid node types."
                    : $"Invalid type: {type}. Did you mean {suggestion}? Use searchNodeType to find similar node types.";
                throw new ArgumentException(message);
            }

            return await CreateNodeInternal(decodedType, position).ConfigureAwait(false);
        }).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// Deletes the specified ProtoFlux node.
    /// </summary>
    /// <param name="nodeRefId">The reference ID of the node to delete.</param>
    /// <returns>A task representing the asynchronous operation that returns a confirmation message or null if deletion failed.</returns>
    [McpServerTool(Name = "deleteNode"), Description("Deletes the specified node.")]
    public static async Task<object?> DeleteNode(string nodeRefId)
    {
        var result = await NodeToolHelpers.HandleAsync(() => NodeToolHelpers.UpdateAction(NodeToolHelpers.WorkspaceSlot, () =>
            {
                NodeLookupTools.FindNodeInternal(nodeRefId).Slot.Destroy();
                return (object?)"done";
            }
        )).ConfigureAwait(false);
        return result;
    }

    private static async Task<ProtoFluxNode> CreateNodeInternal(Type type, float3 position)
    {
        try
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            ResoniteMod.Debug("Creating slot");
            var slot = await GenerateSlotNode(type, NodeToolHelpers.WorkspaceSlot, position).ConfigureAwait(false);

            var node = await NodeToolHelpers.UpdateAction(slot, () =>
            {
                ResoniteMod.DebugFunc(() => $"Attaching {type}");
                var component = slot.AttachComponent(type);
                ResoniteMod.DebugFunc(() => $"Attached {component}");
                var node = component as ProtoFluxNode;
                slot.UnpackNodes();
                return node;
            }).ConfigureAwait(false);

            if (node == null)
            {
                ResoniteMod.Warn("Failed to attach Node");
                throw new InvalidOperationException("Failed to create a ProtoFluxNode.");
            }
            ResoniteMod.DebugFunc(() => $"Attached {node}");

            return node;
        }
        catch (Exception ex)
        {
            ResoniteMod.Warn(ex.ToString());
            throw;
        }
    }

    private static async Task<Slot> GenerateSlotNode(Type type, Slot parentSlot, float3 position)
    {
        try
        {
            Slot? slot = null;
            ResoniteMod.Debug("Waiting for world update");

            parentSlot.RunSynchronously(() =>
            {
                ResoniteMod.Debug("Creating slot");
                slot = parentSlot.AddSlot(type.Name);
                if (slot == slot.World.LocalUserSpace)
                {
                    slot.PositionInFrontOfUser();
                }
                else
                {
                    slot.LocalPosition = position;
                }
                slot.GlobalScale = parentSlot.GlobalScale;
                slot.Tag = null!;
                ResoniteMod.DebugFunc(() => $"Slot created {slot}");
            });

            while (slot is null)
            {
                ResoniteMod.Debug("Waiting slot creation");
                await Task.Delay(100).ConfigureAwait(false);
            }

            return slot;
        }
        catch (Exception ex)
        {
            ResoniteMod.Warn(ex.ToString());
            throw;
        }
    }

    private static System.Collections.Generic.IEnumerable<string> GatherAllNodeTypes(CategoryNode<Type> category)
    {
        return category.Elements.Select(NodeToolHelpers.EncodeType)
            .Concat(category.Subcategories.SelectMany(GatherAllNodeTypes));
    }

    private static string? FindClosestNodeType(string search)
    {
        var category = WorkerInitializer.ComponentLibrary.GetSubcategory("ProtoFlux/Runtimes/Execution/Nodes");
        var searchUpper = NodeToolHelpers.CleanTypeName(search).ToUpperInvariant();
        return GatherAllNodeTypes(category)
            .Select(name => new
            {
                Name = name,
                Distance = NodeToolHelpers.LevenshteinDistance(NodeToolHelpers.CleanTypeName(name).ToUpperInvariant().AsSpan(), searchUpper.AsSpan()),
            })
            .OrderBy(x => x.Distance)
            .Select(x => x.Name)
            .FirstOrDefault();
    }

    private static void ValidateGenericTypeFormat(string type)
    {
        if (type.Contains("<>"))
        {
            ResoniteMod.Warn($"Invalid generic type format {type}. Do not use '<>', use '<T>' instead.");
            throw new ArgumentException("Invalid generic type format. Do not use '<>', use '<T>' instead.", nameof(type));
        }
        if (type.Contains("<,>"))
        {
            ResoniteMod.Warn($"Invalid generic type format {type}. Do not use '<,>', use '<T1, T2>' instead.");
            throw new ArgumentException("Invalid generic type format. Do not use '<,>', use '<T1, T2>' instead.", nameof(type));
        }
        if (type.Contains("<T>"))
        {
            ResoniteMod.Warn($"Invalid generic type format {type}.");
            var guidance = GetCommonTypesGuidance();
            throw new ArgumentException($"Generic types require specific type parameters instead of <T>.\n\n{guidance}", nameof(type));
        }
        if (type.Contains("<T1, T2>"))
        {
            ResoniteMod.Warn($"Invalid generic type format {type}.");
            var guidance = GetCommonTypesGuidance();
            throw new ArgumentException($"Generic types require specific type parameters instead of <T1, T2>.\n\n{guidance}", nameof(type));
        }
        if (type.Contains("`"))
        {
            ResoniteMod.Warn($"Invalid generic type format {type}.");
            throw new ArgumentException("Invalid generic type format. Use '<T>' instead (i.e. [ProtoFluxBindings]FrooxEngine....NodeType<float3>)", nameof(type));
        }
    }

    /// <summary>
    /// Gets commonly used ProtoFlux types for AI guidance
    /// </summary>
    /// <returns>Guidance string listing common categories and types</returns>
    internal static string GetCommonTypesGuidance()
    {
        return @"Common ProtoFlux types for generic nodes:
• Basic types: float, int, bool, string
• Vectors: float2, float3, float4, int2, int3, int4
• Colors: color, colorX
• Math: double, uint, byte, short
• Complex: Slot, IWorldElement, User

Examples:
• ValueInput<float> - Input node for float values
• ValueInput<bool> - Input node for boolean values
• Adder<float, float> - Add two float values
• Multiplier<float3> - Multiply float3 vectors

Use searchNodeType to find specific nodes or listNodeTypes to browse categories.";
    }
}
