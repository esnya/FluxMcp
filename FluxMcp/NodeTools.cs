using System;
using System.Collections.Generic;
using System.ComponentModel;
using ModelContextProtocol.Server;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using System.Linq;

namespace FluxMcp
{
    [McpServerToolType]
    public static class NodeTools
    {
        private static World FocusedWorld => Engine.Current.WorldManager.FocusedWorld;
        private static TypeManager Types => FocusedWorld.Types;
        private static Slot LocalUserSpace => FocusedWorld.LocalUserSpace;
        private static Slot WorkspaceSlot => FocusedWorld.RootSlot.GetChildrenWithTag("__FLUXMCP_WORKSPACE__").Append(LocalUserSpace).First();


        [McpServerTool(Name = "createNode"), Description("Creates a new node with the specified name and type.")]
        public static NodeInfo CreateNode(string type)
        {
            return NodeInfo.Encode(CreateNodeInternal(Types.DecodeType(type)));
        }

        private static ProtoFluxNode CreateNodeInternal(Type type)
        {
            if (type is null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (typeof(ProtoFluxNode).IsAssignableFrom(type))
            {
                throw new InvalidOperationException("The type must not be ProtoFluxNode or its subclass.");
            }

            if (GenerateSlotNode(type, WorkspaceSlot).AttachComponent(type) is not ProtoFluxNode node)
            {
                throw new InvalidOperationException("Failed to create a ProtoFluxNode.");
            }

            return node;
        }

        private static Slot GenerateSlotNode(Type type, Slot parentSlot)
        {
            Slot slot = parentSlot.AddSlot(type.Name);
            slot.PositionInFrontOfUser();
            slot.GlobalScale = parentSlot.GlobalScale;
            return slot;
        }

        [McpServerTool(Name = "findNode"), Description("Finds a node by its reference ID.")]
        public static NodeInfo FindNode(string reference)
        {
            return NodeInfo.Encode(FindNodeInternal(reference));
        }

        private static ProtoFluxNode FindNodeInternal(string reference)
        {
            if (!RefID.TryParse(reference, out var refID))
            {
                throw new ArgumentException("Invalid RefID format.", nameof(reference));
            }

            var obj = FocusedWorld.ReferenceController.GetObjectOrNull(refID);
            return obj as ProtoFluxNode ?? throw new InvalidOperationException($"{reference} does not exist or is not a ProtoFluxNode");
        }

        [McpServerTool(Name = "searchNodeType"), Description("Searches node types in a category.")]
        public static IEnumerable<string> SearchNodeType(string category, int maxItems, int skip = 0)
        {
            return WorkerInitializer.ComponentLibrary.GetSubcategory(category).Elements.Skip(skip).Take(maxItems).Select(Types.EncodeType);
        }

        [McpServerTool(Name = "deleteNode"), Description("Deletes the specified node.")]
        public static void DeleteNode(string nodeRefId)
        {
            FindNodeInternal(nodeRefId).Slot.Destroy();
        }

        [McpServerTool(Name = "tryConnectInput"), Description("Attempts to connect an input to an output.")]
        public static bool TryConnectInput(string nodeRefId, int inputIndex, string outputNodeRefId, int outputIndex)
        {
            var node = FindNodeInternal(nodeRefId);
            var outputNode = FindNodeInternal(outputNodeRefId);

            var input = node.GetInput(inputIndex);
            var output = outputNode.GetOutput(outputIndex);

            return node.TryConnectInput(input, output, allowExplicitCast: true, undoable: true);
        }

        [McpServerTool(Name = "tryConnectImpulse"), Description("Attempts to connect an impulse to an operation.")]
        public static bool TryConnectImpulse(string nodeRefId, int impulseIndex, string operationNodeRefId, int operationIndex)
        {
            var node = FindNodeInternal(nodeRefId);
            var operationNode = FindNodeInternal(operationNodeRefId);

            var impulse = node.GetImpulse(impulseIndex);
            var operation = operationNode.GetOperation(operationIndex);

            return node.TryConnectImpulse(impulse, operation, undoable: true);
        }

        [McpServerTool(Name = "tryConnectReference"), Description("Attempts to connect a reference to another node.")]
        public static bool TryConnectReference(string nodeRefId, int referenceIndex, string targetNodeRefId)
        {
            var node = FindNodeInternal(nodeRefId);
            var targetNode = FindNodeInternal(targetNodeRefId);

            var reference = node.GetReference(referenceIndex);

            return node.TryConnectReference(reference, targetNode, undoable: true);
        }

        private static PackedElement PackNodeElement(IWorldElement element)
        {
            return new PackedElement(element);
        }

        private static PackedElementList PackNodeElementList(ISyncList list)
        {
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
                node.World.Types.EncodeType(node.GetType()),
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
