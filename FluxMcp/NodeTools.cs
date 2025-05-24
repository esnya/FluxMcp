using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ResoniteModLoader;
using System.Threading.Tasks;
using System.Threading;

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
                    var error = ex;
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

        internal static string EncodeType(Type type)
        {
            return Types.EncodeType(type).Replace("<>", "<T>").Replace("<,>", "<T1,T2>");
        }

        [McpServerTool(Name = "createNode"), Description("Creates a new node with the specified name and type. Dimension of postition: (Right, Up, Forward).")]
        public static async Task<NodeInfo> CreateNode(string type, float3 position)
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

            if (!type.StartsWith("[", StringComparison.Ordinal))
            {
                ResoniteMod.Warn($"Type {type} does not start with '['. It should be a valid type name.");
                throw new ArgumentException("Type must start with '[' to be a valid type name.", nameof(type));
            }

            var decodedType = Types.DecodeType(type);
            ResoniteMod.DebugFunc(() => $"Creating Node {type} -> {decodedType}");
            if (decodedType == null)
            {
                throw new ArgumentException($"Invalid type: {type}");
            }
            return NodeInfo.Encode(await CreateNodeInternal(Types.DecodeType(type), position).ConfigureAwait(false));
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
                    ResoniteMod.Debug("Ceating slot");
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
        public static NodeInfo FindNode(string reference)
        {
            return NodeInfo.Encode(FindNodeInternal(reference));
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

        [McpServerTool(Name = "listSubCategories"), Description("Search sub categories ('/' separeted)")]
        public static IEnumerable<string> ListSubCategories(int maxItems, string category = "", int skip = 0)
        {
            return WorkerInitializer.ComponentLibrary.GetSubcategory("ProtoFlux/Runtimes/Execution/Nodes/" + category).Subcategories.Skip(skip).Take(maxItems).Select(x => (category + '/' + x.Name).Replace("//", "/"));
        }

        [McpServerTool(Name = "listNodeTypes"), Description("List ProtoFlux nodes in cattegory (i.e. Actions, Actions/IndirectActions, ...)")]
        public static IEnumerable<string> ListNodeType(string category, int maxItems, int skip = 0)
        {
            return WorkerInitializer.ComponentLibrary.GetSubcategory("ProtoFlux/Runtimes/Execution/Nodes/" + category).Elements.Skip(skip).Take(maxItems).Select(EncodeType);
        }

        private static IEnumerable<string> SearchNodeInternal(CategoryNode<Type> category, string search, int maxItems, int skip = 0)
        {
            return category.Elements.Select(EncodeType).Where(t => t.Contains(search))
                .Concat(
                    category.Subcategories.SelectMany(sub => SearchNodeInternal(sub, search, maxItems, skip))
                ).Skip(skip).Take(maxItems);
        }

        [McpServerTool(Name = "searchNode"), Description("Search node in all category.")]
        public static IEnumerable<string> SearchNode(string search, int maxItems, int skip = 0)
        {
            var category = WorkerInitializer.ComponentLibrary.GetSubcategory("ProtoFlux/Runtimes/Execution/Nodes");
            return SearchNodeInternal(category, search, maxItems, skip);
        }

        [McpServerTool(Name = "deleteNode"), Description("Deletes the specified node.")]
        public static void DeleteNode(string nodeRefId)
        {
            FindNodeInternal(nodeRefId).Slot.Destroy();
        }

        [McpServerTool(Name = "tryConnectInput"), Description("Attempts to connect an input to an output.")]
        public static async Task<bool> TryConnectInput(string nodeRefId, int inputIndex, string outputNodeRefId, int outputIndex)
        {
            return await UpdateAction(WorkspaceSlot, () =>
            {
                ResoniteMod.DebugFunc(() => $"Connecting Input: {nodeRefId}({inputIndex} <- {outputNodeRefId}({outputIndex})");
                var node = FindNodeInternal(nodeRefId);
                ResoniteMod.DebugFunc(() => $"Node: {node}");
                var outputNode = FindNodeInternal(outputNodeRefId);
                ResoniteMod.DebugFunc(() => $"Node: {outputNode}");

                var input = node.GetInput(inputIndex);
                ResoniteMod.DebugFunc(() => $"input: {input}");
                var output = outputNode.GetOutput(outputIndex);
                ResoniteMod.DebugFunc(() => $"output: {output}");

                return node.TryConnectInput(input, output, allowExplicitCast: true, undoable: true);
            }).ConfigureAwait(false);
        }

        [McpServerTool(Name = "tryConnectImpulse"), Description("Attempts to connect an impulse to an operation.")]
        public static async Task<bool> TryConnectImpulse(string nodeRefId, int impulseIndex, string operationNodeRefId, int operationIndex)
        {
            return await UpdateAction(WorkspaceSlot, () =>
            {
                ResoniteMod.DebugFunc(() => $"Connecting Impulse: {nodeRefId}({impulseIndex} <- {operationNodeRefId}({operationIndex})");
                var node = FindNodeInternal(nodeRefId);
                ResoniteMod.DebugFunc(() => $"Node: {node}");

                var operationNode = FindNodeInternal(operationNodeRefId);
                ResoniteMod.DebugFunc(() => $"Node: {operationNode}");

                var impulse = node.GetImpulse(impulseIndex);
                ResoniteMod.DebugFunc(() => $"impulse: {impulse}");
                var operation = operationNode.GetOperation(operationIndex);
                ResoniteMod.DebugFunc(() => $"operation: {operation}");

                return node.TryConnectImpulse(impulse, operation, undoable: true);
            }).ConfigureAwait(false);
        }

        [McpServerTool(Name = "tryConnectReference"), Description("Attempts to connect a reference to another node.")]
        public static async Task<bool> TryConnectReference(string nodeRefId, int referenceIndex, string targetNodeRefId)
        {
            return await UpdateAction(WorkspaceSlot, () =>
            {
                ResoniteMod.DebugFunc(() => $"Connecting Reference: {nodeRefId}({referenceIndex} <- {targetNodeRefId}");
                var node = FindNodeInternal(nodeRefId);
                ResoniteMod.DebugFunc(() => $"node: {node}");

                var targetNode = FindNodeInternal(targetNodeRefId);
                ResoniteMod.DebugFunc(() => $"targetNode: {targetNode}");

                var reference = node.GetReference(referenceIndex);
                ResoniteMod.DebugFunc(() => $"reference: {reference}");


                return node.TryConnectReference(reference, targetNode, undoable: true);
            }).ConfigureAwait(false);
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

        [McpServerTool(Name = "getNodeOutput"), Description("Gets the output of a node by index.")]
        public static object GetNodeOutput(string nodeRefId, int outputIndex)
        {
            var node = FindNodeInternal(nodeRefId);
            return PackNodeElement(node.GetOutput(outputIndex));
        }

        [McpServerTool(Name = "getNodeInput"), Description("Gets the input of a node by index.")]
        public static object GetNodeInput(string nodeRefId, int inputIndex)
        {
            var node = FindNodeInternal(nodeRefId);
            return PackNodeElement(node.GetInput(inputIndex));
        }

        [McpServerTool(Name = "getNodeImpulse"), Description("Gets the impulse of a node by index.")]
        public static object GetNodeImpulse(string nodeRefId, int impulseIndex)
        {
            var node = FindNodeInternal(nodeRefId);
            return PackNodeElement(node.GetImpulse(impulseIndex));
        }

        [McpServerTool(Name = "getNodeOperation"), Description("Gets the operation of a node by index.")]
        public static object GetNodeOperation(string nodeRefId, int operationIndex)
        {
            var node = FindNodeInternal(nodeRefId);
            return PackNodeElement(node.GetOperation(operationIndex));
        }

        [McpServerTool(Name = "getNodeReference"), Description("Gets the reference of a node by index.")]
        public static object GetNodeReference(string nodeRefId, int referenceIndex)
        {
            var node = FindNodeInternal(nodeRefId);
            return PackNodeElement(node.GetReference(referenceIndex));
        }

        [McpServerTool(Name = "getNodeInputList"), Description("Gets the input list of a node by index.")]
        public static object GetNodeInputList(string nodeRefId, int inputListIndex)
        {
            var node = FindNodeInternal(nodeRefId);
            return PackNodeElementList(node.GetInputList(inputListIndex));
        }

        [McpServerTool(Name = "getNodeOutputList"), Description("Gets the output list of a node by index.")]
        public static object GetNodeOutputList(string nodeRefId, int outputListIndex)
        {
            var node = FindNodeInternal(nodeRefId);
            return PackNodeElementList(node.GetOutputList(outputListIndex));
        }

        [McpServerTool(Name = "getNodeImpulseList"), Description("Gets the impulse list of a node by index.")]
        public static object GetNodeImpulseList(string nodeRefId, int impulseListIndex)
        {
            var node = FindNodeInternal(nodeRefId);
            return PackNodeElementList(node.GetImpulseList(impulseListIndex));
        }

        [McpServerTool(Name = "getNodeOperationList"), Description("Gets the operation list of a node by index.")]
        public static object GetNodeOperationList(string nodeRefId, int operationListIndex)
        {
            var node = FindNodeInternal(nodeRefId);
            return PackNodeElementList(node.GetOperationList(operationListIndex));
        }

        [McpServerTool(Name = "getNodeGlobalRef"), Description("Gets the global reference of a node by index.")]
        public static object GetNodeGlobalRef(string nodeRefId, int globalRefIndex)
        {
            var node = FindNodeInternal(nodeRefId);
            return PackNodeElement(node.GetGlobalRef(globalRefIndex));
        }

        [McpServerTool(Name = "getNodeGlobalRefList"), Description("Gets the global reference list of a node by index.")]
        public static object GetNodeGlobalRefList(string nodeRefId, int globalRefListIndex)
        {
            var node = FindNodeInternal(nodeRefId);
            return PackNodeElementList(node.GetGlobalRefList(globalRefListIndex));
        }

        [McpServerTool(Name = "getWorldElement"), Description("Gets information about an element by its RefID.")]
        public static PackedElement GetWorldElement(string refId)
        {
            if (!RefID.TryParse(refId, out var parsedRefId))
            {
                throw new ArgumentException("Invalid RefID format.", nameof(refId));
            }

            var element = FocusedWorld.ReferenceController.GetObjectOrNull(parsedRefId) ?? throw new InvalidOperationException($"No element found with RefID: {refId}");
            return new PackedElement(element);
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
}
