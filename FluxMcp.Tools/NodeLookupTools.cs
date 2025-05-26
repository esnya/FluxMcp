using FrooxEngine;
using FrooxEngine.ProtoFlux;
using ModelContextProtocol.Server;
using ResoniteModLoader;
using System;
using System.ComponentModel;
using System.Linq;
using Elements.Core;

namespace FluxMcp.Tools;

/// <summary>
/// Provides tools for finding and browsing ProtoFlux nodes in the focused world.
/// </summary>
[McpServerToolType]
public static class NodeLookupTools
{
    /// <summary>
    /// Finds a ProtoFlux node by its reference ID.
    /// </summary>
    /// <param name="reference">The RefID string of the node to find.</param>
    /// <returns>The found ProtoFlux node or null if an error occurs.</returns>
    [McpServerTool(Name = "findNode"), Description("Finds a node by its reference ID.")]
    public static object? FindNode(string reference)
    {
        return NodeToolHelpers.Handle(() => FindNodeInternal(reference));
    }

    internal static ProtoFluxNode FindNodeInternal(string reference)
    {
        try
        {
            ResoniteMod.DebugFunc(() => $"Finding node {reference}");
            if (!RefID.TryParse(reference, out var refID))
            {
                throw new ArgumentException("Invalid RefID format.", nameof(reference));
            }

            var obj = NodeToolHelpers.FocusedWorld.ReferenceController.GetObjectOrNull(refID);
            ResoniteMod.DebugFunc(() => $"Found {obj} ({obj?.GetType()})");
            return obj as ProtoFluxNode ?? throw new InvalidOperationException($"{reference} does not exist or is not a ProtoFluxNode");
        }
        catch (Exception ex)
        {
            ResoniteMod.Warn(ex.ToString());
            throw;
        }
    }

    /// <summary>
    /// Gets all available ProtoFlux node categories.
    /// </summary>
    /// <returns>A collection of category names or null if an error occurs.</returns>
    [McpServerTool(Name = "getCategories"), Description("Get all ProtoFlux node categories.")]
    public static object? GetCategories()
    {
        return NodeToolHelpers.Handle(() =>
        {
            ResoniteMod.Debug("Gathering ProtoFlux node categories");
            var root = GetProtoFluxNodeCategory();
            return GatherSubcategories(root);
        });
    }

    /// <summary>
    /// Lists all ProtoFlux node types in a specific category.
    /// </summary>
    /// <param name="category">The category path (e.g., "Math", "Actions", "Actions/IndirectActions").</param>
    /// <returns>A collection of node type names in the specified category or null if an error occurs.</returns>
    [McpServerTool(Name = "listNodeTypes"), Description("List ProtoFlux nodes in category (e.g. Math, Actions, Actions/IndirectActions). Use this to browse available node types in a specific category.")]
    public static object? ListNodeTypesInCategory(string category)
    {
        return NodeToolHelpers.Handle(() =>
        {
            return GetProtoFluxNodeCategory(category).Elements.Select(NodeToolHelpers.EncodeType);
        });
    }

    /// <summary>
    /// Searches for ProtoFlux nodes across all categories by name or functionality.
    /// </summary>
    /// <param name="search">The search term to look for in node names.</param>
    /// <param name="maxItems">Maximum number of results to return.</param>
    /// <param name="skip">Number of results to skip (for pagination).</param>
    /// <returns>A collection of matching node type names or null if an error occurs.</returns>
    [McpServerTool(Name = "searchNodeType"), Description("Search for ProtoFlux nodes across all categories. Use this to find nodes by name or functionality. Ideal for discovering available node types when you're not sure of the exact name.")]
    public static object? SearchNodeType(string search, int maxItems, int skip = 0)
    {
        return NodeToolHelpers.Handle(() =>
        {
            var category = WorkerInitializer.ComponentLibrary.GetSubcategory("ProtoFlux/Runtimes/Execution/Nodes");
            return SearchNodeTypeInternal(category, search, maxItems, skip);
        });
    }

    /// <summary>
    /// Gets information about any world element by its RefID.
    /// </summary>
    /// <param name="refId">The RefID string of the element to retrieve.</param>
    /// <returns>The found world element or null if an error occurs.</returns>
    [McpServerTool(Name = "getWorldElement"), Description("Gets information about an element by its RefID.")]
    public static object? GetWorldElement(string refId)
    {
        return NodeToolHelpers.Handle(() =>
        {
            if (!RefID.TryParse(refId, out var parsedRefId))
            {
                throw new ArgumentException("Invalid RefID format.", nameof(refId));
            }

            var element = NodeToolHelpers.FocusedWorld.ReferenceController.GetObjectOrNull(parsedRefId) ?? throw new InvalidOperationException($"No element found with RefID: {refId}");
            return (object?)element;
        });
    }

    private static CategoryNode<Type> GetProtoFluxNodeCategory(string category = "")
    {
        var fullCategory = "ProtoFlux/Runtimes/Execution/Nodes/" + category;
        ResoniteMod.DebugFunc(() => $"Getting ProtoFlux Node Category {fullCategory}");
        return WorkerInitializer.ComponentLibrary.GetSubcategory(fullCategory);
    }

    private static System.Collections.Generic.IEnumerable<string> GatherSubcategories(CategoryNode<Type> category, string prefix = "")
    {
        ResoniteMod.DebugFunc(() => $"Gathering subcategories for {category.Name} with prefix {prefix}");
        var subcategories = category.Subcategories?.ToList();
        if (category.ElementCount == 0 && (subcategories == null || subcategories.Count == 0))
        {
            ResoniteMod.DebugFunc(() => $"No elements in category {category.Name}");
            return Enumerable.Empty<string>();
        }

        return subcategories?.SelectMany(sub =>
            {
                var subPrefix = prefix + sub.Name + '/';
                return GatherSubcategories(sub, subPrefix).Prepend(prefix + sub.Name);
            }) ?? Enumerable.Repeat(prefix + category.Name, 1);
    }

    private static System.Collections.Generic.IEnumerable<string> SearchNodeTypeInternal(CategoryNode<Type> category, string search, int maxItems, int skip = 0)
    {
        var results = new System.Collections.Generic.List<(string Name, int Distance)>();
        var cleanedSearch = NodeToolHelpers.CleanTypeName(search).Replace(" ", string.Empty).ToUpperInvariant();

        void Gather(CategoryNode<Type> node)
        {
            foreach (var name in node.Elements.Select(NodeToolHelpers.EncodeType))
            {
                var cleanedName = NodeToolHelpers.CleanTypeName(name).Replace(" ", string.Empty).ToUpperInvariant();
                var distance = cleanedName.Contains(cleanedSearch)
                    ? 0
                    : NodeToolHelpers.LevenshteinDistance(cleanedName.AsSpan(), cleanedSearch.AsSpan());
                results.Add((name, distance));
            }

            foreach (var sub in node.Subcategories)
            {
                Gather(sub);
            }
        }

        Gather(category);

        return results
            .OrderBy(r => r.Distance)
            .ThenBy(r => r.Name.Length)
            .Skip(skip)
            .Take(maxItems)
            .Select(r => r.Name);
    }
}
