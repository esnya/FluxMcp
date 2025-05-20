using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
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
            var client = await _listener.AcceptTcpClientAsync(cancellationToken);
            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
        }
    }

    private static async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        while (!cancellationToken.IsCancellationRequested && client.Connected)
        {
            var line = await reader.ReadLineAsync();
            if (line == null) break;
            // TODO: Parse and execute MCP commands.
            await writer.WriteLineAsync("ok");
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
    public string? GetOutputDisplay(Guid nodeId) => _nodes.TryGetValue(nodeId, out var node) ? node.OutputDisplay : null;
    public IReadOnlyList<string> GetInputFields(Guid nodeId) => _nodes.TryGetValue(nodeId, out var node) ? node.InputFields : Array.Empty<string>();
    public IReadOnlyList<string> GetOutputFields(Guid nodeId) => _nodes.TryGetValue(nodeId, out var node) ? node.OutputFields : Array.Empty<string>();
    public void ConnectInput(Guid nodeId, string inputField, Guid targetNodeId) { /* TODO */ }
    public void ConnectOutput(Guid nodeId, string outputField, Guid targetNodeId) { /* TODO */ }
    public Guid? GetCurrentConnection(Guid nodeId, string field) => null; // TODO
    public void TriggerCall(Guid nodeId) { /* TODO */ }
    public void TriggerDynamicImpulse(Guid nodeId) { /* TODO */ }
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
}

