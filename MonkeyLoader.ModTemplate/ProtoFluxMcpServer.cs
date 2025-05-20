using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MonkeyLoader.ModTemplate;

internal sealed class ProtoFluxMcpServer
{
    private TcpListener? _listener;
    private readonly ConcurrentDictionary<Guid, ProtoFluxNode> _nodes = new();

    public Task StartAsync(int port, CancellationToken cancellationToken)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        return AcceptLoopAsync(cancellationToken);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        if (_listener == null) return;

        while (!cancellationToken.IsCancellationRequested)
        {
            var client = await _listener.AcceptTcpClientAsync();
            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        while (!cancellationToken.IsCancellationRequested && client.Connected)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;

            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            var cmd = parts[0].ToUpperInvariant();
            var response = "ok";

            try
            {
                switch (cmd)
                {
                    case "CREATE" when parts.Length >= 3:
                        var node = CreateNode(parts[1], parts[2]);
                        response = node.Id.ToString();
                        break;
                    case "FIND" when parts.Length >= 2:
                        var found = FindNodes(parts[1]);
                        response = string.Join(",", found.Select(n => $"{n.Id}:{n.Name}"));
                        break;
                    case "GET_OUTPUT_DISPLAY" when parts.Length >= 2:
                        var od = GetOutputDisplay(Guid.Parse(parts[1]));
                        response = od ?? string.Empty;
                        break;
                    case "GET_INPUT_FIELDS" when parts.Length >= 2:
                        response = string.Join(",", GetInputFields(Guid.Parse(parts[1])));
                        break;
                    case "GET_OUTPUT_FIELDS" when parts.Length >= 2:
                        response = string.Join(",", GetOutputFields(Guid.Parse(parts[1])));
                        break;
                    case "CONNECT_INPUT" when parts.Length >= 4:
                        ConnectInput(Guid.Parse(parts[1]), parts[2], Guid.Parse(parts[3]));
                        break;
                    case "CONNECT_OUTPUT" when parts.Length >= 4:
                        ConnectOutput(Guid.Parse(parts[1]), parts[2], Guid.Parse(parts[3]));
                        break;
                    case "GET_CONNECTION" when parts.Length >= 3:
                        var conn = GetCurrentConnection(Guid.Parse(parts[1]), parts[2]);
                        response = conn?.ToString() ?? string.Empty;
                        break;
                    case "CALL" when parts.Length >= 2:
                        TriggerCall(Guid.Parse(parts[1]));
                        break;
                    case "IMPULSE" when parts.Length >= 2:
                        TriggerDynamicImpulse(Guid.Parse(parts[1]));
                        break;
                    default:
                        response = "error";
                        break;
                }
            }
            catch (Exception ex)
            {
                response = "error " + ex.Message;
            }

            await writer.WriteLineAsync(response);
        }
    }

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

    // The following methods are placeholders for future implementation.
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

internal sealed class ProtoFluxNode
{
    public ProtoFluxNode(Guid id, string name, string type)
    {
        Id = id;
        Name = name;
        Type = type;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string Type { get; }

    // Placeholder properties for the server API.
    public string OutputDisplay { get; set; } = string.Empty;
    public List<string> InputFields { get; } = new();
    public List<string> OutputFields { get; } = new();

    public Dictionary<string, Guid> InputConnections { get; } = new();
    public Dictionary<string, Guid> OutputConnections { get; } = new();

    public int CallCount { get; set; }
    public int ImpulseCount { get; set; }
}

