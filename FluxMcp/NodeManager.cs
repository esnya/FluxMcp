using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace FluxMcp
{
    public class NodeManager
    {
        private readonly ConcurrentDictionary<Guid, ProtoFluxNode> _nodes = new();

        public ProtoFluxNode CreateNode(string name, string type)
        {
            var node = new ProtoFluxNode(Guid.NewGuid(), name, type);
            _nodes[node.Id] = node;
            return node;
        }

        public IEnumerable<ProtoFluxNode> FindNodes(string name)
        {
            foreach (var node in _nodes.Values)
            {
                if (node.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
                    yield return node;
            }
        }

        public string? GetOutputDisplay(Guid nodeId) =>
            _nodes.TryGetValue(nodeId, out var node) ? node.OutputDisplay : null;

        public IReadOnlyList<string> GetInputFields(Guid nodeId) =>
            _nodes.TryGetValue(nodeId, out var node) ? node.InputFields : Array.Empty<string>();

        public IReadOnlyList<string> GetOutputFields(Guid nodeId) =>
            _nodes.TryGetValue(nodeId, out var node) ? node.OutputFields : Array.Empty<string>();

        public void ConnectInput(Guid nodeId, string inputField, Guid targetNodeId)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
                node.InputConnections[inputField] = targetNodeId;
        }

        public void ConnectOutput(Guid nodeId, string outputField, Guid targetNodeId)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
                node.OutputConnections[outputField] = targetNodeId;
        }

        public Guid? GetCurrentConnection(Guid nodeId, string field)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
            {
                if (node.InputConnections.TryGetValue(field, out var t))
                    return t;
                if (node.OutputConnections.TryGetValue(field, out t))
                    return t;
            }
            return null;
        }

        public void TriggerCall(Guid nodeId)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
                node.CallCount++;
        }

        public void TriggerDynamicImpulse(Guid nodeId)
        {
            if (_nodes.TryGetValue(nodeId, out var node))
                node.ImpulseCount++;
        }
    }
}
