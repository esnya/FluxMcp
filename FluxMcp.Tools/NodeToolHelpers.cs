using Elements.Core;
using FrooxEngine;
using ResoniteModLoader;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;

namespace FluxMcp.Tools;

/// <summary>
/// Provides helper utilities for ProtoFlux node tool operations.
/// </summary>
public static class NodeToolHelpers
{
    internal static World FocusedWorld => Engine.Current.WorldManager.FocusedWorld;
    internal static TypeManager Types => FocusedWorld.Types;
    internal static Slot LocalUserSpace => FocusedWorld.LocalUserSpace;
    internal const string WorkspaceTag = "__FLUXMCP_WORKSPACE__";
    internal static Slot WorkspaceSlot => FocusedWorld.RootSlot
        .GetChildrenWithTag(WorkspaceTag)
        .Append(LocalUserSpace)
        .First();


    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Error should be sent to client")]
    internal static async Task<T> UpdateAction<T>(Slot slot, Func<T> action)
    {
        T result = default!;
        var completionSource = new TaskCompletionSource<T>();

        slot.RunSynchronously(() =>
        {
            try
            {
                result = action();
                completionSource.SetResult(result);
            }
            catch (Exception ex)
            {
                ResoniteMod.Warn(ex);
                completionSource.SetException(ex);
            }
        });

        return await completionSource.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a function and handles any exceptions, returning appropriate error content.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="func">The function to execute.</param>
    /// <returns>The result of the function or error content if an exception occurs.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Error should be sent to client")]
    public static object Handle<T>(Func<T> func)
    {
        if (func == null) throw new ArgumentNullException(nameof(func));
        try
        {
            var result = func();
            return MapResult(result);
        }
        catch (Exception ex)
        {
            return new ErrorContent(ex.Message);
        }
    }

    /// <summary>
    /// Asynchronously executes a function and handles any exceptions, returning appropriate error content.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="func">The asynchronous function to execute.</param>
    /// <returns>A task that represents the asynchronous operation, containing the result or error content.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Error should be sent to client")]
    public static async Task<object> HandleAsync<T>(Func<Task<T>> func)
    {
        if (func == null) throw new ArgumentNullException(nameof(func));
        try
        {
            var result = await func().ConfigureAwait(false);
            return MapResult(result);
        }
        catch (Exception ex)
        {
            return new ErrorContent(ex.Message);
        }
    }

    internal static string EncodeType(Type type)
    {
        return Types.EncodeType(type).Replace("<>", "<T>").Replace("<,>", "<T1,T2>");
    }


    private static AIContent ToAIContent(object? item)
    {
        if (item is AIContent ai)
        {
            return ai;
        }
        else if (item is string s)
        {
            return new TextContent(s);
        }
        else if (item is IWorldElement worldElement)
        {
            var json = JsonSerializer.Serialize<IWorldElement>(worldElement, JsonOptions);
            return new TextContent(json);
        }

        return new TextContent(JsonSerializer.Serialize(item, JsonOptions));
    }

    private static AIContent ToContent(object? item) => ToAIContent(item);

    private static object MapResult(object? result)
    {
        if (result is System.Collections.IEnumerable seq && result is not string)
        {
            return seq.Cast<object?>()
                .Select(ToContent)
                .ToList();
        }

        return ToAIContent(result);
    }

    internal static string CleanTypeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (name.StartsWith("[", StringComparison.Ordinal))
        {
            var endBracket = name.IndexOf(']');
            if (endBracket >= 0)
            {
                name = name[(endBracket + 1)..];
            }
        }
        var lt = name.LastIndexOf('<');
        var gt = name.LastIndexOf('>');
        if (lt >= 0 && gt == name.Length - 1)
        {
            name = name[..lt];
        }
        var lastDot = name.LastIndexOf('.');
        if (lastDot >= 0)
        {
            name = name[(lastDot + 1)..];
        }
        return name;
    }

    /// <summary>
    /// Calculates the Levenshtein distance between two strings.
    /// </summary>
    /// <param name="a">The first string.</param>
    /// <param name="b">The second string.</param>
    /// <returns>The Levenshtein distance between the two strings.</returns>
    public static int LevenshteinDistance(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        if (a.IsEmpty)
        {
            return b.Length;
        }

        if (b.IsEmpty)
        {
            return a.Length;
        }

        Span<int> previous = new int[b.Length + 1];
        Span<int> current = new int[b.Length + 1];

        for (int j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(previous[j] + 1, current[j - 1] + 1),
                    previous[j - 1] + cost);
            }

            var temp = previous;
            previous = current;
            current = temp;
        }

        return previous[b.Length];
    }


    internal static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = false };
    static NodeToolHelpers()
    {
        NodeSerialization.RegisterConverters(JsonOptions);
    }
}
