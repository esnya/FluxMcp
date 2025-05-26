using FrooxEngine;
using FrooxEngine.ProtoFlux;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FluxMcp.Tools;

public static class NodeSerialization
{
    public static void RegisterConverters(JsonSerializerOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        options.Converters.Add(new WorldElementConverter());
        options.Converters.Add(new ProtoFluxNodeConverter());
    }
}

public class WorldElementConverter : JsonConverter<IWorldElement>
{
    public override IWorldElement? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization of IWorldElement is not supported.");
    }

    internal static void WriteFields(Utf8JsonWriter writer, IWorldElement element)
    {
        writer.WriteString("refId", element.ReferenceID.ToString());
        writer.WriteString("name", element.Name);
        writer.WriteString("type", element.GetType().Name);
    }

    public override void Write(Utf8JsonWriter writer, IWorldElement value, JsonSerializerOptions options)
    {
        if (writer == null) throw new ArgumentNullException(nameof(writer));
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (value == null) throw new ArgumentNullException(nameof(value));
        writer.WriteStartObject();

        WriteFields(writer, value);

        var parent = value.Parent;
        if (parent != null)
        {
            writer.WriteStartObject("parent");
            WriteFields(writer, parent);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }
}

public class ProtoFluxNodeConverter : JsonConverter<ProtoFluxNode>
{
    public override ProtoFluxNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization of ProtoFluxNode is not supported.");
    }

    public override void Write(Utf8JsonWriter writer, ProtoFluxNode node, JsonSerializerOptions options)
    {
        if (writer == null) throw new ArgumentNullException(nameof(writer));
        if (options == null) throw new ArgumentNullException(nameof(options));
        if (node == null) throw new ArgumentNullException(nameof(node));

        writer.WriteStartObject();

        WorldElementConverter.WriteFields(writer, node);

        writer.WriteNumber("nodeInputCount", node.NodeInputCount);
        writer.WriteNumber("nodeInputListCount", node.NodeInputListCount);
        writer.WriteNumber("nodeOutputCount", node.NodeOutputCount);
        writer.WriteNumber("nodeOutputListCount", node.NodeOutputListCount);
        writer.WriteNumber("nodeImpulseCount", node.NodeImpulseCount);
        writer.WriteNumber("nodeImpulseListCount", node.NodeImpulseListCount);
        writer.WriteNumber("nodeOperationCount", node.NodeOperationCount);
        writer.WriteNumber("nodeOperationListCount", node.NodeOperationListCount);
        writer.WriteNumber("nodeReferenceCount", node.NodeReferenceCount);
        writer.WriteNumber("nodeGlobalRefCount", node.NodeGlobalRefCount);
        writer.WriteNumber("nodeGlobalRefListCount", node.NodeGlobalRefListCount);
        writer.WriteEndObject();
    }
}
