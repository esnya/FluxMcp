using Elements.Core;
using FrooxEngine;
using ModelContextProtocol.Server;
using ResoniteModLoader;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FluxMcp;

internal static class NodeToolHelpers
{
    internal static World FocusedWorld => Engine.Current.WorldManager.FocusedWorld;
    internal static TypeManager Types => FocusedWorld.Types;
    internal static Slot LocalUserSpace => FocusedWorld.LocalUserSpace;
    internal static Slot WorkspaceSlot => FocusedWorld.RootSlot
        .GetChildrenWithTag("__FLUXMCP_WORKSPACE__")
        .Append(LocalUserSpace)
        .First();

    internal static async Task<T> UpdateAction<T>(Slot slot, Func<T> action)
    {
        T result = default!;
        var done = false;
        Exception? error = null;

        slot.RunSynchronously(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                ResoniteMod.Warn(ex);
                error = ex;
            }
            finally
            {
                done = true;
            }
        });

        while (!done && error == null)
        {
            ResoniteMod.Debug("Waiting slot creation");
            await Task.Delay(100).ConfigureAwait(false);
        }

        if (error != null)
        {
            throw error;
        }

        return result;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Error should be sent to client")]
    internal static object? Handle<T>(Func<T> func)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            return new ErrorContent(ex.Message);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Error should be sent to client")]
    internal static async Task<object?> HandleAsync<T>(Func<Task<T>> func)
    {
        try
        {
            return await func().ConfigureAwait(false);
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

    internal static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

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

    internal static int LevenshteinDistance(ReadOnlySpan<char> a, ReadOnlySpan<char> b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[a.Length, b.Length];
    }
}
