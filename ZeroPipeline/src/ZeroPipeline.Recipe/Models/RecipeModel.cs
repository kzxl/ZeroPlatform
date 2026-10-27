using System;
using System.Collections.Generic;

namespace ZeroPipeline.Recipe.Models
{
    /// <summary>
    /// Declarative specification of a pipeline inspection routine (DAG topology + node configurations).
    /// Can be saved to / loaded from disk recipes for dynamic reconfiguration without recompilation.
    /// </summary>
    public sealed class RecipeModel
    {
        public string RecipeId { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Untitled Recipe";
        public string Version { get; set; } = "1.0.0";
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public List<NodeRecipeModel> Nodes { get; set; } = new List<NodeRecipeModel>();
        public List<ConnectionRecipeModel> Connections { get; set; } = new List<ConnectionRecipeModel>();

        public override string ToString() =>
            $"Recipe '{Name}' (v{Version}) [{Nodes.Count} nodes, {Connections.Count} connections]";
    }

    /// <summary>
    /// Configuration descriptor for an individual pipeline node within a recipe.
    /// </summary>
    public sealed class NodeRecipeModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string NodeType { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public override string ToString() =>
            $"{NodeType} [Id={Id}, Name={Name}, Params={Parameters.Count}]";
    }

    /// <summary>
    /// Directed edge descriptor connecting an output port of a source node to an input port of a target node.
    /// </summary>
    public sealed class ConnectionRecipeModel
    {
        public string SourceNodeId { get; set; } = string.Empty;
        public string SourcePortName { get; set; } = string.Empty;
        public string TargetNodeId { get; set; } = string.Empty;
        public string TargetPortName { get; set; } = string.Empty;

        public override string ToString() =>
            $"{SourceNodeId}.{SourcePortName} -> {TargetNodeId}.{TargetPortName}";
    }
}
