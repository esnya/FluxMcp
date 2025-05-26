using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.Actions;
using ModelContextProtocol.Server;
using ResoniteModLoader;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace FluxMcp;

[McpServerToolType]
public static class NodeCreationTools
{
    [McpServerTool(Name = "createNode"), Description("Creates a new node with the specified name and type. Dimension of postition: (Right, Up, Forward).")]
    public static Task<object?> CreateNode(string type, float3 position)
    {
        return NodeToolHelpers.HandleAsync(async () =>
        {
            if (string.IsNullOrEmpty(type))
            {
                ResoniteMod.Warn("Type cannot be null or empty.");
                throw new ArgumentException("Type cannot be null or empty.", nameof(type));
            }

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
                throw new ArgumentException("Use specific generic type format (i.e. [ProtoFluxBindings]FrooxEngine....NodeType<float3>) instead of <T>", nameof(type));
            }

            if (type.Contains("<T1, T2>"))
            {
                ResoniteMod.Warn($"Invalid generic type format {type}.");
                throw new ArgumentException("Use specific generic type format (i.e. [ProtoFluxBindings]FrooxEngine....NodeType<float3, float3>) instead of <T1, T2>", nameof(type));
            }

            if (type.Contains("`") )
            {
                ResoniteMod.Warn($"Invalid generic type format {type}.");
                throw new ArgumentException("Invalid generic type format. Use '<T>' instead (i.e. [ProtoFluxBindings]FrooxEngine....NodeType<float3>)", nameof(type));
            }
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
                    ? $"Invalid type: {type}"
                    : $"Invalid type: {type}. Did you mean {suggestion}?";
                throw new ArgumentException(message);
            }

            return NodeInfo.Encode(await CreateNodeInternal(decodedType, position).ConfigureAwait(false));
        });
    }

    [McpServerTool(Name = "deleteNode"), Description("Deletes the specified node.")]
    public static Task<object?> DeleteNode(string nodeRefId)
    {
        return NodeToolHelpers.HandleAsync(() => NodeToolHelpers.UpdateAction(NodeToolHelpers.WorkspaceSlot, () =>
            {
                NodeLookupTools.FindNodeInternal(nodeRefId).Slot.Destroy();
                return (object?)"done";
            }
        ));
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
}
