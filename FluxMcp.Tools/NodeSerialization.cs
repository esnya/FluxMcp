using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FluxMcp;

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
            NodeToolHelpers.EncodeType(node.GetType()),
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
        if (element is null) throw new ArgumentNullException(nameof(element));

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
