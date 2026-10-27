using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ZeroPipeline.Core.Nodes;

namespace ZeroPipeline.Recipe.Registry
{
    /// <summary>
    /// Thread-safe registry and dynamic instantiation factory for pipeline nodes.
    /// Maps recipe 'NodeType' strings to concrete IPipelineNode implementations and hydrates node parameters.
    /// </summary>
    public sealed class NodeRegistry
    {
        private readonly ConcurrentDictionary<string, Func<string, string, IDictionary<string, string>, IPipelineNode>> _factories =
            new ConcurrentDictionary<string, Func<string, string, IDictionary<string, string>, IPipelineNode>>(StringComparer.OrdinalIgnoreCase);

        private static readonly Lazy<NodeRegistry> _defaultInstance = new Lazy<NodeRegistry>(() => new NodeRegistry());
        public static NodeRegistry Default => _defaultInstance.Value;

        /// <summary>
        /// Registers a pipeline node type with parameterless or (string name) constructor and automatic property hydration.
        /// </summary>
        public NodeRegistry Register<TNode>(string? customTypeName = null) where TNode : IPipelineNode
        {
            var type = typeof(TNode);
            string key = customTypeName ?? type.Name;

            _factories[key] = (id, name, parameters) =>
            {
                // 1. Try constructor (string name, string id) or (string name) or parameterless
                IPipelineNode? node = null;
                var ctorNameId = type.GetConstructor(new[] { typeof(string), typeof(string) });
                if (ctorNameId != null)
                {
                    node = (IPipelineNode)ctorNameId.Invoke(new object[] { name, id });
                }
                else
                {
                    var ctorName = type.GetConstructor(new[] { typeof(string) });
                    if (ctorName != null)
                    {
                        node = (IPipelineNode)ctorName.Invoke(new object[] { name });
                    }
                    else
                    {
                        var ctorEmpty = type.GetConstructor(Type.EmptyTypes);
                        if (ctorEmpty != null)
                        {
                            node = (IPipelineNode)ctorEmpty.Invoke(null);
                        }
                    }
                }

                if (node == null)
                {
                    throw new InvalidOperationException(
                        $"Cannot instantiate '{type.FullName}'. No compatible public constructor found.");
                }

                // 2. Hydrate properties from parameters dictionary
                HydrateProperties(node, parameters);
                return node;
            };

            return this;
        }

        /// <summary>
        /// Registers a custom factory delegate for specialized node types with parameterized constructors.
        /// </summary>
        public NodeRegistry Register(string nodeType, Func<string, string, IDictionary<string, string>, IPipelineNode> factory)
        {
            if (string.IsNullOrEmpty(nodeType)) throw new ArgumentNullException(nameof(nodeType));
            if (factory == null) throw new ArgumentNullException(nameof(factory));

            _factories[nodeType] = factory;
            return this;
        }

        /// <summary>
        /// Instantiates a pipeline node by its registered type name, assigning Id, Name, and configuring parameters.
        /// </summary>
        public IPipelineNode Create(string nodeType, string id, string name, IDictionary<string, string> parameters)
        {
            if (string.IsNullOrEmpty(nodeType)) throw new ArgumentNullException(nameof(nodeType));

            if (!_factories.TryGetValue(nodeType, out var factory))
            {
                throw new KeyNotFoundException($"Pipeline node type '{nodeType}' is not registered in NodeRegistry.");
            }

            return factory(id, name, parameters ?? new Dictionary<string, string>());
        }

        public bool IsRegistered(string nodeType) => _factories.ContainsKey(nodeType);

        public static void HydrateProperties(object target, IDictionary<string, string> parameters)
        {
            if (target == null || parameters == null || parameters.Count == 0) return;

            var targetType = target.GetType();
            var properties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (!prop.CanWrite) continue;

                if (parameters.TryGetValue(prop.Name, out string? strVal) && strVal != null)
                {
                    try
                    {
                        object converted = ConvertValue(strVal, prop.PropertyType);
                        prop.SetValue(target, converted, null);
                    }
                    catch
                    {
                        // Skip incompatible property conversions gracefully
                    }
                }
            }
        }

        private static object ConvertValue(string str, Type targetType)
        {
            if (targetType == typeof(string)) return str;
            if (targetType == typeof(int)) return int.Parse(str, CultureInfo.InvariantCulture);
            if (targetType == typeof(long)) return long.Parse(str, CultureInfo.InvariantCulture);
            if (targetType == typeof(double)) return double.Parse(str, CultureInfo.InvariantCulture);
            if (targetType == typeof(float)) return float.Parse(str, CultureInfo.InvariantCulture);
            if (targetType == typeof(bool)) return bool.Parse(str);
            if (targetType.IsEnum) return Enum.Parse(targetType, str, ignoreCase: true);

            return Convert.ChangeType(str, targetType, CultureInfo.InvariantCulture);
        }
    }
}
