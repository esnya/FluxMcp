using FrooxEngine;
using FrooxEngine.ProtoFlux;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elements.Core;

namespace FluxMcp.Tools;

public static class NodeSerialization
{
    public static void RegisterConverters(JsonSerializerOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        options.Converters.Add(new RefIDConverter());
        options.Converters.Add(new WorldElementConverter());
        options.Converters.Add(new ProtoFluxNodeConverter());
        options.Converters.Add(new Float3Converter());
    }
}

public class RefIDConverter : JsonConverter<RefID>
{
    public override RefID Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("Expected string token for RefID.");
        }

        var refIdString = reader.GetString();
        if (refIdString == null || !RefID.TryParse(refIdString, out var refId))
        {
            throw new JsonException($"Invalid RefID format: {refIdString}");
        }

        return refId;
    }

    public override void Write(Utf8JsonWriter writer, RefID value, JsonSerializerOptions options)
    {
        if (writer == null) throw new ArgumentNullException(nameof(writer));
        writer.WriteStringValue(value.ToString());
    }
}

public class WorldElementConverter : JsonConverter<IWorldElement>
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert == typeof(IWorldElement);

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

public class Float3Converter : JsonConverter<float3>
{
    public override float3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization of float3 is not supported.");
    }

    public override void Write(Utf8JsonWriter writer, float3 value, JsonSerializerOptions options)
    {
        if (writer == null) throw new ArgumentNullException(nameof(writer));
        writer.WriteStartObject();
        writer.WriteNumber("x", value.x);
        writer.WriteNumber("y", value.y);
        writer.WriteNumber("z", value.z);
        writer.WriteEndObject();
    }
}
