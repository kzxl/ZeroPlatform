using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using ZeroPipeline.Core.Graph;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;
using ZeroPipeline.Recipe.Models;
using ZeroPipeline.Recipe.Registry;

namespace ZeroPipeline.Recipe.Builder
{
    /// <summary>
    /// Builds executable PipelineGraph instances from declarative RecipeModels and exports active graphs to recipes.
    /// </summary>
    public sealed class RecipeGraphBuilder
    {
        private readonly NodeRegistry _registry;

        public NodeRegistry Registry => _registry;

        public RecipeGraphBuilder(NodeRegistry? registry = null)
        {
            _registry = registry ?? NodeRegistry.Default;
        }

        /// <summary>
        /// Instantiates all nodes, hydrates parameters, wires port connections, and validates DAG topology.
        /// </summary>
        public PipelineGraph BuildGraph(RecipeModel recipe)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));

            var graph = new PipelineGraph();
            var nodeMap = new Dictionary<string, IPipelineNode>(StringComparer.OrdinalIgnoreCase);

            // 1. Instantiate and add nodes
            foreach (var nodeModel in recipe.Nodes)
            {
                var node = _registry.Create(nodeModel.NodeType, nodeModel.Id, nodeModel.Name, nodeModel.Parameters);
                graph.AddNode(node);

                if (!string.IsNullOrEmpty(nodeModel.Id))
                {
                    nodeMap[nodeModel.Id] = node;
                }
                if (!string.IsNullOrEmpty(nodeModel.Name) && !nodeMap.ContainsKey(nodeModel.Name))
                {
                    nodeMap[nodeModel.Name] = node;
                }
            }

            // 2. Wire connections
            foreach (var connModel in recipe.Connections)
            {
                if (!nodeMap.TryGetValue(connModel.SourceNodeId, out var srcNode))
                {
                    throw new KeyNotFoundException($"Source node '{connModel.SourceNodeId}' not found in recipe.");
                }

                if (!nodeMap.TryGetValue(connModel.TargetNodeId, out var dstNode))
                {
                    throw new KeyNotFoundException($"Target node '{connModel.TargetNodeId}' not found in recipe.");
                }

                var srcPort = srcNode.OutputPorts.FirstOrDefault(p => p.Name.Equals(connModel.SourcePortName, StringComparison.OrdinalIgnoreCase));
                if (srcPort == null)
                {
                    throw new KeyNotFoundException(
                        $"Output port '{connModel.SourcePortName}' not found on node '{srcNode.Name}'.");
                }

                var dstPort = dstNode.InputPorts.FirstOrDefault(p => p.Name.Equals(connModel.TargetPortName, StringComparison.OrdinalIgnoreCase));
                if (dstPort == null)
                {
                    throw new KeyNotFoundException(
                        $"Input port '{connModel.TargetPortName}' not found on node '{dstNode.Name}'.");
                }

                graph.Connect(srcPort, dstPort);
            }

            // 3. Validate DAG invariant
            graph.Validate();
            return graph;
        }

        /// <summary>
        /// Extracts nodes, parameters, and active connections from a PipelineGraph into a RecipeModel.
        /// </summary>
        public RecipeModel ExportRecipe(PipelineGraph graph, string recipeId, string name, string version = "1.0.0", string description = "")
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            var recipe = new RecipeModel
            {
                RecipeId = recipeId ?? Guid.NewGuid().ToString(),
                Name = name ?? "Exported Recipe",
                Version = version ?? "1.0.0",
                Description = description ?? string.Empty,
                CreatedAtUtc = DateTime.UtcNow
            };

            // 1. Export nodes
            foreach (var node in graph.Nodes)
            {
                var nodeModel = new NodeRecipeModel
                {
                    Id = node.Id,
                    Name = node.Name,
                    NodeType = node.GetType().Name
                };

                // Extract public scalar properties
                var props = node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var p in props)
                {
                    if (!p.CanRead || !p.CanWrite) continue;
                    if (p.PropertyType == typeof(string) || p.PropertyType.IsPrimitive || p.PropertyType.IsEnum)
                    {
                        var val = p.GetValue(node, null);
                        if (val != null)
                        {
                            nodeModel.Parameters[p.Name] = Convert.ToString(val, CultureInfo.InvariantCulture) ?? string.Empty;
                        }
                    }
                }

                recipe.Nodes.Add(nodeModel);
            }

            // 2. Export connections
            foreach (var conn in graph.Connections)
            {
                recipe.Connections.Add(new ConnectionRecipeModel
                {
                    SourceNodeId = conn.Source.OwnerNode.Id,
                    SourcePortName = conn.Source.Name,
                    TargetNodeId = conn.Target.OwnerNode.Id,
                    TargetPortName = conn.Target.Name
                });
            }

            return recipe;
        }
    }
}
