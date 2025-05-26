using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.Actions;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;
using ResoniteModLoader;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FluxMcp
{
    [McpServerToolType]
    public static class NodeTools
    {
        private static World FocusedWorld => Engine.Current.WorldManager.FocusedWorld;
        private static TypeManager Types => FocusedWorld.Types;
        private static Slot LocalUserSpace => FocusedWorld.LocalUserSpace;
        private static Slot WorkspaceSlot => FocusedWorld.RootSlot.GetChildrenWithTag("__FLUXMCP_WORKSPACE__").Append(LocalUserSpace).First();

        private static async Task<T> UpdateAction<T>(Slot slot, Func<T> action)
        {
            T result = default!;
            var done = false;
            Exception? error = null;

            slot.RunSynchronously(() =>
            {
                try
                {
                    result = action();
                }
                catch (Exception ex)
                {
                    ResoniteMod.Warn(ex);
                    error = ex;
                }
                finally
                {
                    done = true;
                }
            });

            while (!done && error == null)
            {
                ResoniteMod.Debug("Waiting slot creation");
                await Task.Delay(100).ConfigureAwait(false);
            }

            if (error != null)
            {
                throw error;
            }

            return result;
        }


        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Error should be sent to client")]
        private static object? Handle<T>(Func<T> func)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                return new ErrorContent(ex.Message);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Error should be sent to client")]
        private static async Task<object?> HandleAsync<T>(Func<Task<T>> func)
        {
            try
            {
                return await func().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new ErrorContent(ex.Message);
            }
        }

        internal static string EncodeType(Type type)
        {
            return Types.EncodeType(type).Replace("<>", "<T>").Replace("<,>", "<T1,T2>");
        }

        private static int LevenshteinDistance(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
        {
            var d = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= a.Length; i++)
            {
                d[i, 0] = i;
            }

            for (int j = 0; j <= b.Length; j++)
            {
                d[0, j] = j;
            }

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[a.Length, b.Length];
        }

        private static IEnumerable<string> GatherAllNodeTypes(CategoryNode<Type> category)
        {
            return category.Elements.Select(EncodeType)
                .Concat(category.Subcategories.SelectMany(GatherAllNodeTypes));
        }

        private static string? FindClosestNodeType(string search)
        {
            var category = WorkerInitializer.ComponentLibrary.GetSubcategory("ProtoFlux/Runtimes/Execution/Nodes");
            var searchUpper = CleanTypeName(search).ToUpperInvariant();
            return GatherAllNodeTypes(category)
                .Select(name => new
                {
                    Name = name,
                    Distance = LevenshteinDistance(CleanTypeName(name).ToUpperInvariant().AsSpan(), searchUpper.AsSpan()),
                })
                .OrderBy(x => x.Distance)
                .Select(x => x.Name)
                .FirstOrDefault();
        }

        [McpServerTool(Name = "createNode"), Description("Creates a new node with the specified name and type. Dimension of postition: (Right, Up, Forward).")]
        public static Task<object?> CreateNode(string type, float3 position)
        {
            return HandleAsync(async () =>
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

                if (type.Contains("`"))
                {
                    ResoniteMod.Warn($"Invalid generic type format {type}.");
                    throw new ArgumentException("Invalid generic type format. Use '<T>' instead (i.e. [ProtoFluxBindings]FrooxEngine....NodeType<float3>)", nameof(type));
                }
                const string bindingPrefix = "[ProtoFluxBindings]";
                // Ensure prefix exists
                if (!type.StartsWith(bindingPrefix, StringComparison.Ordinal))
                {
                    type = bindingPrefix + type;
                    ResoniteMod.DebugFunc(() => $"Added binding prefix to type: {type}");
                }
                // Fallback: add default namespace if missing
                const string defaultNamespace = "FrooxEngine.ProtoFlux.Runtimes.Execution.Nodes.";
                if (!type.StartsWith(bindingPrefix + defaultNamespace, StringComparison.Ordinal))
                {
                    var innerType = type.Substring(bindingPrefix.Length);
                    type = bindingPrefix + defaultNamespace + innerType;
                    ResoniteMod.DebugFunc(() => $"Applied fallback namespace to type: {type}");
                }
                var decodedType = Types.DecodeType(type);
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

        private static async Task<ProtoFluxNode> CreateNodeInternal(Type type, float3 position)
        {
            try
            {
                if (type is null)
                {
                    throw new ArgumentNullException(nameof(type));
                }

                //if (typeof(ProtoFluxNode).IsAssignableFrom(type))
                //{
                //    throw new InvalidOperationException("The type must not be ProtoFluxNode or its subclass.");
                //}

                ResoniteMod.Debug("Creating slot");
                var slot = await GenerateSlotNode(type, WorkspaceSlot, position).ConfigureAwait(false);

                var node = await UpdateAction(slot, () =>
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

        [McpServerTool(Name = "findNode"), Description("Finds a node by its reference ID.")]
        public static object? FindNode(string reference)
        {
            return Handle(() => NodeInfo.Encode(FindNodeInternal(reference)));
        }

        private static ProtoFluxNode FindNodeInternal(string reference)
        {
            try
            {
                ResoniteMod.DebugFunc(() => $"Finding node {reference}");
                if (!RefID.TryParse(reference, out var refID))
                {
                    throw new ArgumentException("Invalid RefID format.", nameof(reference));
                }


                var obj = FocusedWorld.ReferenceController.GetObjectOrNull(refID);
                ResoniteMod.DebugFunc(() => $"Found {obj} ({obj?.GetType()})");
                return obj as ProtoFluxNode ?? throw new InvalidOperationException($"{reference} does not exist or is not a ProtoFluxNode");
            }
            catch (Exception ex)
            {
                ResoniteMod.Warn(ex.ToString());
                throw;
            }
        }

        private static CategoryNode<Type> GetProtoFluxNodeCategory(string category = "")
        {
            var fullCategory = "ProtoFlux/Runtimes/Execution/Nodes/" + category;
            ResoniteMod.DebugFunc(() => $"Getting ProtoFlux Node Category {fullCategory}");
            return WorkerInitializer.ComponentLibrary.GetSubcategory(fullCategory);
        }

        private static IEnumerable<string> GatherSubcategories(CategoryNode<Type> category, string prefix = "")
        {
            ResoniteMod.DebugFunc(() => $"Gathering subcategories for {category.Name} with prefix {prefix}");
            var subcategories = category.Subcategories?.ToList();
            if (category.ElementCount == 0 && (subcategories == null || subcategories.Count == 0))
            {
                // No elements in this category, return empty
                ResoniteMod.DebugFunc(() => $"No elements in category {category.Name}");
                return Enumerable.Empty<string>();
            }

            return subcategories?.SelectMany(sub =>
                {
                    var subPrefix = prefix + sub.Name + '/';
                    return GatherSubcategories(sub, subPrefix).Prepend(prefix + sub.Name);
                }) ?? Enumerable.Repeat(prefix + category.Name, 1);
        }

        [McpServerTool(Name = "getCategories"), Description("Get all ProtoFlux node categories.")]
        public static object? GetCategories()
        {
            return Handle(() =>
            {
                ResoniteMod.Debug("Gathering ProtoFlux node categories");
                var root = GetProtoFluxNodeCategory();
                return GatherSubcategories(root);
            });
        }

        [McpServerTool(Name = "listNodeTypes"), Description("List ProtoFlux nodes in cattegory (i.e. Actions, Actions/IndirectActions, ...)")]
        public static object? ListNodeTypesInCategory(string category, int maxItems, int skip = 0)
        {
            return Handle(() =>
            {
                var categoryNode = GetProtoFluxNodeCategory(category);
                return new ListResult<string>(
                    categoryNode.Elements.Select(EncodeType).Skip(skip).Take(maxItems),
                    categoryNode.ElementCount,
                    skip
                );
            });
        }

        private static IEnumerable<string> SearchNodeTypeInternal(CategoryNode<Type> category, string search, int maxItems, int skip = 0)
        {
            var results = new List<(string Name, int Distance)>();
            // normalize search: remove brackets/generics, namespace, whitespace, and uppercase
            var cleanedSearch = CleanTypeName(search).Replace(" ", string.Empty).ToUpperInvariant();

            void Gather(CategoryNode<Type> node)
            {
                foreach (var name in node.Elements.Select(EncodeType))
                {
                    var cleanedName = CleanTypeName(name).Replace(" ", string.Empty).ToUpperInvariant();
                    var distance = cleanedName.Contains(cleanedSearch)
                        ? 0
                        : LevenshteinDistance(cleanedName.AsSpan(), cleanedSearch.AsSpan());
                    results.Add((name, distance));
                }

                foreach (var sub in node.Subcategories)
                {
                    Gather(sub);
                }
            }

            Gather(category);

            return results
                .OrderBy(r => r.Distance)
                .ThenBy(r => r.Name.Length)
                .Skip(skip)
                .Take(maxItems)
                .Select(r => r.Name);
        }

        [McpServerTool(Name = "searchNodeType"), Description("Search node in all category.")]
        public static object? SearchNodeType(string search, int maxItems, int skip = 0)
        {
            return Handle(() =>
            {
                var category = WorkerInitializer.ComponentLibrary.GetSubcategory("ProtoFlux/Runtimes/Execution/Nodes");
                return SearchNodeTypeInternal(category, search, maxItems, skip);
            });
        }

        [McpServerTool(Name = "deleteNode"), Description("Deletes the specified node.")]
        public static Task<object?> DeleteNode(string nodeRefId)
        {
            return HandleAsync(() => UpdateAction(WorkspaceSlot, () =>
                {
                    FindNodeInternal(nodeRefId).Slot.Destroy();
                    return (object?)"done";
                })
            );
        }

        public enum ConnectionType
        {
            Input,
            Output,
            Impulse,
            Operation,
            Reference,
            InputList,
            OutputList,
            ImpulseList,
            OperationList,
            GlobalRef,
            GlobalRefList
        }

        private static bool TryConnectInternal(ProtoFluxNode node, ConnectionType connectionType, int index, ProtoFluxNode target, int targetIndex)
        {
            return connectionType switch
            {
                ConnectionType.Input => node.TryConnectInput(node.GetInput(index), target.GetOutput(targetIndex), allowExplicitCast: false, undoable: true),
                ConnectionType.Output => target.TryConnectInput(target.GetInput(targetIndex), node.GetOutput(index), allowExplicitCast: false, undoable: true),
                ConnectionType.InputList => node.TryConnectInput(node.GetInput(index), target.GetOutput(targetIndex), allowExplicitCast: false, undoable: true),
                ConnectionType.OutputList => target.TryConnectInput(target.GetInput(targetIndex), node.GetOutput(index), allowExplicitCast: false, undoable: true),
                ConnectionType.Impulse => node.TryConnectImpulse(node.GetImpulse(index), target.GetOperation(targetIndex), undoable: true),
                ConnectionType.ImpulseList => node.TryConnectImpulse(node.GetImpulse(index), target.GetOperation(targetIndex), undoable: true),
                ConnectionType.Reference => node.TryConnectReference(node.GetReference(index), target, undoable: true),
                _ => throw new ArgumentException($"Unsupported connect type: {connectionType}", nameof(connectionType))
            };
        }
        // Consolidated connection attempt for inputs, impulses, and references
        [McpServerTool(Name = "tryConnect"), Description("Attempts to connect a node's connection by type and index to a target node.")]
        public static Task<object?> TryConnect(string nodeRefId, ConnectionType connectionType, int index, string targetNodeRefId, int targetIndex = 0)
        {
            return HandleAsync(async () =>
                await UpdateAction(WorkspaceSlot, async () =>
                {
                    var node = FindNodeInternal(nodeRefId);
                    var target = FindNodeInternal(targetNodeRefId);

                    try
                    {
                        return TryConnectInternal(node, connectionType, index, target, targetIndex);
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        ResoniteMod.Warn(ex.ToString());
                        return TryConnectInternal(target, connectionType, targetIndex, node, index);
                    }
                }).ConfigureAwait(false)
            );
        }

        private static PackedElement PackNodeElement(IWorldElement element)
        {
            ResoniteMod.DebugFunc(() => $"Packing Node Element {element}");
            return new PackedElement(element);
        }

        private static PackedElementList PackNodeElementList(ISyncList list)
        {
            ResoniteMod.DebugFunc(() => $"Packing Node Element List {list}");
            return new PackedElementList(list);
        }

        [McpServerTool(Name = "getNodeConnection"), Description("Gets specified node connection element or list by type and index.")]
        public static object? GetNodeConnection(string nodeRefId, ConnectionType connectionType, int index)
        {
            return Handle<object>(() =>
            {
                var node = FindNodeInternal(nodeRefId);
                return connectionType switch
                {
                    ConnectionType.Input => PackNodeElement(node.GetInput(index)),
                    ConnectionType.Output => PackNodeElement(node.GetOutput(index)),
                    ConnectionType.Impulse => PackNodeElement(node.GetImpulse(index)),
                    ConnectionType.Operation => PackNodeElement(node.GetOperation(index)),
                    ConnectionType.Reference => PackNodeElement(node.GetReference(index)),
                    ConnectionType.InputList => PackNodeElementList(node.GetInputList(index)),
                    ConnectionType.OutputList => PackNodeElementList(node.GetOutputList(index)),
                    ConnectionType.ImpulseList => PackNodeElementList(node.GetImpulseList(index)),
                    ConnectionType.OperationList => PackNodeElementList(node.GetOperationList(index)),
                    ConnectionType.GlobalRef => PackNodeElement(node.GetGlobalRef(index)),
                    ConnectionType.GlobalRefList => PackNodeElementList(node.GetGlobalRefList(index)),
                    _ => throw new ArgumentException($"Unknown connection type: {connectionType}", nameof(connectionType))
                };
            });
        }

        [McpServerTool(Name = "getWorldElement"), Description("Gets information about an element by its RefID.")]
        public static object? GetWorldElement(string refId)
        {
            return Handle(() =>
            {
                if (!RefID.TryParse(refId, out var parsedRefId))
                {
                    throw new ArgumentException("Invalid RefID format.", nameof(refId));
                }

                var element = FocusedWorld.ReferenceController.GetObjectOrNull(parsedRefId) ?? throw new InvalidOperationException($"No element found with RefID: {refId}");
                if (element is ProtoFluxNode node)
                {
                    return NodeInfo.Encode(node);
                }

                return (object?)element;
            });
        }

        [McpServerTool(Name = "getInputNodeValue"), Description("Gets the value of input node by index")]
        public static object? GetInputNodeValue(string nodeRefId, int inputIndex)
        {
            return Handle(() =>
            {
                var node = FindNodeInternal(nodeRefId);
                if (node is not IInput inputNode)
                {
                    throw new InvalidOperationException($"Node {nodeRefId} is not an input node.");
                }
                return inputNode.BoxedValue;
            });
        }

        [McpServerTool(Name = "setInputNodeValue"), Description("Sets the value of input node")]
        public static async Task<object?> SetInputNodeValue(string nodeRefId, JsonElement value)
        {
            return await HandleAsync(async () =>
            {
                return await UpdateAction(WorkspaceSlot, () =>
                {
                    var node = FindNodeInternal(nodeRefId);
                    if (node is not IInput inputNode)
                    {
                        throw new InvalidOperationException($"Node {nodeRefId} is not an input node.");
                    }
                    var targetType = inputNode.InputType();

                    try
                    {
                        if (targetType == typeof(colorX) && !value.TryGetProperty("profile", out var _))
                        {
                            inputNode.BoxedValue = (colorX)value.Deserialize<color>(_jsonOptions);
                        }
                        else
                        {
                            inputNode.BoxedValue = value.Deserialize(targetType, _jsonOptions)!;
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
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        // Consolidated setter; removed all typed Set*InputValue overloads and helper methods.

        // Removes leading '[...]' and trailing '<...>' from type names
        private static string CleanTypeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (name.StartsWith("[", StringComparison.Ordinal))
            {
                var endBracket = name.IndexOf(']');
                if (endBracket >= 0)
                {
                    name = name.Substring(endBracket + 1);
                }
            }
            var lt = name.LastIndexOf('<');
            var gt = name.LastIndexOf('>');
            if (lt >= 0 && gt == name.Length - 1)
            {
                name = name.Substring(0, lt);
            }
            // remove namespace prefix (up to last '.')
            var lastDot = name.LastIndexOf('.');
            if (lastDot >= 0)
            {
                name = name.Substring(lastDot + 1);
            }
            return name;
        }
    }

    public class NodeInfo
    {
        public string RefId { get; set; }
        public string NodeType { get; set; }
        public string NodeName { get; set; }
        public int NodeInputCount { get; set; }
        public int NodeInputListCount { get; set; }
        public int NodeOutputCount { get; set; }
        public int NodeOutputListCount { get; set; }
        public int NodeImpulseCount { get; set; }
        public int NodeImpulseListCount { get; set; }
        public int NodeOperationCount { get; set; }
        public int NodeOperationListCount { get; set; }
        public int NodeReferenceCount { get; set; }
        public int NodeGlobalRefCount { get; set; }
        public int NodeGlobalRefListCount { get; set; }

        public NodeInfo(string refId, string nodeType, string nodeName, int nodeInputCount, int nodeInputListCount,
                        int nodeOutputCount, int nodeOutputListCount, int nodeImpulseCount, int nodeImpulseListCount,
                        int nodeOperationCount, int nodeOperationListCount, int nodeReferenceCount,
                        int nodeGlobalRefCount, int nodeGlobalRefListCount)
        {
            RefId = refId;
            NodeType = nodeType;
            NodeName = nodeName;
            NodeInputCount = nodeInputCount;
            NodeInputListCount = nodeInputListCount;
            NodeOutputCount = nodeOutputCount;
            NodeOutputListCount = nodeOutputListCount;
            NodeImpulseCount = nodeImpulseCount;
            NodeImpulseListCount = nodeImpulseListCount;
            NodeOperationCount = nodeOperationCount;
            NodeOperationListCount = nodeOperationListCount;
            NodeReferenceCount = nodeReferenceCount;
            NodeGlobalRefCount = nodeGlobalRefCount;
            NodeGlobalRefListCount = nodeGlobalRefListCount;
        }

        public static NodeInfo Encode(ProtoFluxNode node)
        {
            if (node is null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            return new NodeInfo(
                node.ReferenceID.ToString(),
                NodeTools.EncodeType(node.GetType()),
                node.Name,
                node.NodeInputCount,
                node.NodeInputListCount,
                node.NodeOutputCount,
                node.NodeOutputListCount,
                node.NodeImpulseCount,
                node.NodeImpulseListCount,
                node.NodeOperationCount,
                node.NodeOperationListCount,
                node.NodeReferenceCount,
                node.NodeGlobalRefCount,
                node.NodeGlobalRefListCount
            );
        }
    }

    public class PackedElement
    {
        public string RefId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string? ParentRefId { get; set; }

        public PackedElement(IWorldElement element)
        {
            if (element is null) throw new ArgumentNullException(nameof(element)); // Simplified null check for IDE0270

            RefId = element.ReferenceID.ToString();
            Name = element.Name;
            Type = element.GetType().Name;
            ParentRefId = element.Parent?.ReferenceID.ToString();
        }
    }

    public class PackedElementList
    {
        public string RefId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public IReadOnlyCollection<PackedElement> Elements { get; }

        public PackedElementList(ISyncList list)
        {
            if (list == null) throw new ArgumentNullException(nameof(list));

            RefId = list.ReferenceID.ToString();
            Name = list.Name;
            Type = list.GetType().Name;
            Elements = list.Elements.Cast<IWorldElement>().Select(e => new PackedElement(e)).ToList().AsReadOnly();
        }
    }

    public class ListResult<T>
    {
        public IEnumerable<T> Items { get; }
        public int TotalCount { get; }
        public int Skip { get; }

        public ListResult(IEnumerable<T> items, int totalCount, int skip = 0)
        {
            Items = items ?? throw new ArgumentNullException(nameof(items));
            TotalCount = totalCount;
            Skip = skip;
        }
    }
}
