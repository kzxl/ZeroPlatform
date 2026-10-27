using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Process
{
    /// <summary>
    /// Contextual execution payload passed when a process node action is invoked.
    /// </summary>
    public class ProcessActionContext
    {
        /// <summary>
        /// The triggered process node.
        /// </summary>
        public ProcessFlowNode Node { get; }

        /// <summary>
        /// The host UI control dispatching the action (WinForms Control or WPF FrameworkElement).
        /// </summary>
        public object? SourceControl { get; }

        /// <summary>
        /// Optional user parameter or contextual state.
        /// </summary>
        public object? Parameter { get; set; }

        /// <summary>
        /// Set to true to abort execution.
        /// </summary>
        public bool Handled { get; set; }

        public ProcessActionContext(ProcessFlowNode node, object? sourceControl = null, object? parameter = null)
        {
            Node = node;
            SourceControl = sourceControl;
            Parameter = parameter;
        }
    }

    /// <summary>
    /// Metadata descriptor for a reusable action or UserControl target registered in the catalog.
    /// </summary>
    public class ProcessActionDescriptor
    {
        public string Key { get; }
        public string Title { get; }
        public string Category { get; }
        public string Description { get; }
        public Action<ProcessActionContext> Handler { get; }

        public ProcessActionDescriptor(string key, string title, string category, Action<ProcessActionContext> handler, string description = "")
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Title = title ?? key;
            Category = category ?? "General";
            Description = description;
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public override string ToString() => $"[{Category}] {Title} ({Key})";
    }

    /// <summary>
    /// Centralized registry decoupling process nodes from concrete UI views and UserControls.
    /// Allows modular action registration and seamless execution when clicking diagram nodes.
    /// </summary>
    public static class ProcessActionRegistry
    {
        private static readonly object _syncRoot = new object();
        private static readonly Dictionary<string, ProcessActionDescriptor> _registry = new Dictionary<string, ProcessActionDescriptor>(StringComparer.OrdinalIgnoreCase);

        public static event EventHandler<ProcessActionContext>? ActionExecuting;
        public static event EventHandler<ProcessActionContext>? ActionExecuted;

        /// <summary>
        /// Registers an action into the catalog.
        /// </summary>
        public static void Register(string key, string title, string category, Action<ProcessActionContext> handler, string description = "")
        {
            if (string.IsNullOrWhiteSpace(key)) return;

            var desc = new ProcessActionDescriptor(key, title, category, handler, description);
            lock (_syncRoot)
            {
                _registry[key] = desc;
            }
        }

        /// <summary>
        /// Removes an action from the catalog.
        /// </summary>
        public static bool Unregister(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            lock (_syncRoot)
            {
                return _registry.Remove(key);
            }
        }

        /// <summary>
        /// Attempts to locate a registered action descriptor by key.
        /// </summary>
        public static bool TryGetAction(string key, out ProcessActionDescriptor? descriptor)
        {
            descriptor = null;
            if (string.IsNullOrWhiteSpace(key)) return false;

            lock (_syncRoot)
            {
                return _registry.TryGetValue(key, out descriptor);
            }
        }

        /// <summary>
        /// Returns all currently registered actions across all categories.
        /// </summary>
        public static IReadOnlyList<ProcessActionDescriptor> GetAllActions()
        {
            lock (_syncRoot)
            {
                var list = new List<ProcessActionDescriptor>(_registry.Values);
                return list.AsReadOnly();
            }
        }

        /// <summary>
        /// Executes the registered action handler matching the specified key.
        /// </summary>
        public static bool Execute(string key, ProcessActionContext context)
        {
            if (string.IsNullOrWhiteSpace(key) || context == null) return false;

            ProcessActionDescriptor? descriptor;
            lock (_syncRoot)
            {
                if (!_registry.TryGetValue(key, out descriptor))
                {
                    return false;
                }
            }

            ActionExecuting?.Invoke(null, context);
            if (context.Handled) return false;

            try
            {
                descriptor.Handler(context);
                ActionExecuted?.Invoke(null, context);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProcessActionRegistry] Error executing action '{key}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Clears all registered actions.
        /// </summary>
        public static void Clear()
        {
            lock (_syncRoot)
            {
                _registry.Clear();
            }
        }
    }
}
