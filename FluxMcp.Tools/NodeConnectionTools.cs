using FrooxEngine.ProtoFlux;
using ModelContextProtocol.Server;
using ResoniteModLoader;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace FluxMcp.Tools;

/// <summary>
/// Provides MCP tools for connecting ProtoFlux nodes.
/// </summary>
[McpServerToolType]
public static class NodeConnectionTools
{
    /// <summary>
    /// Defines the types of connections available for ProtoFlux nodes.
    /// </summary>
    public enum ConnectionType
    {
        /// <summary>Input connection.</summary>
        Input,
        /// <summary>Output connection.</summary>
        Output,
        /// <summary>Impulse connection.</summary>
        Impulse,
        /// <summary>Operation connection.</summary>
        Operation,
        /// <summary>Reference connection.</summary>
        Reference,
        /// <summary>Input list connection.</summary>
        InputList,
        /// <summary>Output list connection.</summary>
        OutputList,
        /// <summary>Impulse list connection.</summary>
        ImpulseList,
        /// <summary>Operation list connection.</summary>
        OperationList,
        /// <summary>Global reference connection.</summary>
        GlobalRef,
        /// <summary>Global reference list connection.</summary>
        GlobalRefList
    }

    /// <summary>
    /// Attempts to connect a node's connection by type and index to a target node.
    /// </summary>
    /// <param name="nodeRefId">The reference ID of the source node.</param>
    /// <param name="connectionType">The type of connection (Input, Output, Impulse, etc.).</param>
    /// <param name="index">The connection index on the source node.</param>
    /// <param name="targetNodeRefId">The reference ID of the target node.</param>
    /// <param name="targetIndex">The connection index on the target node (default: 0).</param>
    /// <returns>A task representing the asynchronous operation result.</returns>
    [McpServerTool(Name = "tryConnect"), Description("Attempts to connect a node's connection by type and index to a target node. connectionType values: Input, Output, Impulse, Operation, Reference, InputList, OutputList, ImpulseList, OperationList, GlobalRef, GlobalRefList")]
    public static Task<object> TryConnect(string nodeRefId, string connectionType, int index, string targetNodeRefId, int targetIndex = 0)
    {
        return NodeToolHelpers.HandleAsync(async () =>
            await NodeToolHelpers.UpdateAction(NodeToolHelpers.WorkspaceSlot, () =>
            {
                var node = NodeLookupTools.FindNodeInternal(nodeRefId);
                var target = NodeLookupTools.FindNodeInternal(targetNodeRefId);

                if (!Enum.TryParse<ConnectionType>(connectionType, out var parsedConnectionType))
                {
                    throw new ArgumentException($"Invalid connection type: {connectionType}. Valid values: {string.Join(", ", Enum.GetNames(typeof(ConnectionType)))}", nameof(connectionType));
                }

                try
                {
                    return TryConnectInternal(node, parsedConnectionType, index, target, targetIndex);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    ResoniteMod.Warn(ex.ToString());
                    return TryConnectInternal(target, parsedConnectionType, targetIndex, node, index);
                }
            }).ConfigureAwait(false)
        );
    }

    /// <summary>
    /// Gets specified node connection element or list by type and index.
    /// </summary>
    /// <param name="nodeRefId">The reference ID of the node.</param>
    /// <param name="connectionType">The type of connection to retrieve.</param>
    /// <param name="index">The connection index.</param>
    /// <returns>The connection element or list.</returns>
    [McpServerTool(Name = "getNodeConnection"), Description("Gets specified node connection element or list by type and index. connectionType values: Input, Output, Impulse, Operation, Reference, InputList, OutputList, ImpulseList, OperationList, GlobalRef, GlobalRefList")]
    public static object GetNodeConnection(string nodeRefId, string connectionType, int index)
    {
        return NodeToolHelpers.Handle<object>(() =>
        {
            var node = NodeLookupTools.FindNodeInternal(nodeRefId);

            if (!Enum.TryParse<ConnectionType>(connectionType, out var parsedConnectionType))
            {
                throw new ArgumentException($"Invalid connection type: {connectionType}. Valid values: {string.Join(", ", Enum.GetNames(typeof(ConnectionType)))}", nameof(connectionType));
            }

            return parsedConnectionType switch
            {
                ConnectionType.Input => node.GetInput(index),
                ConnectionType.Output => node.GetOutput(index),
                ConnectionType.Impulse => node.GetImpulse(index),
                ConnectionType.Operation => node.GetOperation(index),
                ConnectionType.Reference => node.GetReference(index),
                ConnectionType.InputList => node.GetInputList(index),
                ConnectionType.OutputList => node.GetOutputList(index),
                ConnectionType.ImpulseList => node.GetImpulseList(index),
                ConnectionType.OperationList => node.GetOperationList(index),
                ConnectionType.GlobalRef => node.GetGlobalRef(index),
                ConnectionType.GlobalRefList => node.GetGlobalRefList(index),
                _ => throw new ArgumentException($"Unknown connection type: {parsedConnectionType}", nameof(connectionType))
            };
        });
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

}
