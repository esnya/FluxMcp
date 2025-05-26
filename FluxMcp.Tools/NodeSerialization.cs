using FrooxEngine;
using FrooxEngine.ProtoFlux;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Elements.Core;

namespace FluxMcp.Tools;

/// <summary>
/// Provides JSON serialization utilities for ProtoFlux nodes and related types.
/// </summary>
public static class NodeSerialization
{
    /// <summary>
    /// Registers all custom JSON converters for ProtoFlux types.
    /// </summary>
    /// <param name="options">The JsonSerializerOptions to register converters with.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    public static void RegisterConverters(JsonSerializerOptions options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));

        options.Converters.Add(new RefIDConverter());
        options.Converters.Add(new WorldElementConverter());
        options.Converters.Add(new ProtoFluxNodeConverter());
        options.Converters.Add(new Float3Converter());
    }
}

/// <summary>
/// JSON converter for RefID types.
/// </summary>
public class RefIDConverter : JsonConverter<RefID>
{
    /// <inheritdoc />
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

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, RefID value, JsonSerializerOptions options)
    {
        if (writer == null) throw new ArgumentNullException(nameof(writer));
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// JSON converter for IWorldElement types.
/// </summary>
public class WorldElementConverter : JsonConverter<IWorldElement>
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert == typeof(IWorldElement);

    /// <inheritdoc />
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

    /// <inheritdoc />
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

/// <summary>
/// JSON converter for ProtoFluxNode types.
/// </summary>
public class ProtoFluxNodeConverter : JsonConverter<ProtoFluxNode>
{
    /// <inheritdoc />
    public override ProtoFluxNode? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization of ProtoFluxNode is not supported.");
    }

    /// <inheritdoc />
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

/// <summary>
/// JSON converter for float3 types.
/// </summary>
public class Float3Converter : JsonConverter<float3>
{
    /// <inheritdoc />
    public override float3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        throw new NotSupportedException("Deserialization of float3 is not supported.");
    }

    /// <inheritdoc />
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
