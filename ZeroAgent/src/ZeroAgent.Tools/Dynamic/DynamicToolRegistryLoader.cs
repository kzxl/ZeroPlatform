using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Core.Tools;
using ZeroAgent.Tools.Dynamic.Model;
using ZeroAgent.Tools.Dynamic.Provider;

namespace ZeroAgent.Tools.Dynamic
{
    /// <summary>
    /// Coordinates dynamic tool loading from multiple providers (JSON, DB) into an AgentToolRegistry.
    /// Manages live hot-reloading when definitions change at the source.
    /// </summary>
    public sealed class DynamicToolRegistryLoader : IDisposable
    {
        private readonly AgentToolRegistry _registry;
        private readonly DynamicToolFactory _factory;
        private readonly List<IToolDefinitionProvider> _providers;
        private readonly object _syncLock = new object();
        private bool _disposed;

        /// <summary>
        /// Fired after tools are loaded or hot-reloaded into the registry.
        /// Useful for pushing notifications to MCP clients or UI status bars.
        /// </summary>
        public event EventHandler<int>? RegistryReloaded;

        public DynamicToolRegistryLoader(AgentToolRegistry registry, DynamicToolFactory factory)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _providers = new List<IToolDefinitionProvider>();
        }

        public void AddProvider(IToolDefinitionProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            lock (_syncLock)
            {
                _providers.Add(provider);
                provider.DefinitionsChanged += OnProviderDefinitionsChanged;
            }
        }

        public async Task<int> LoadAllAsync(CancellationToken cancellationToken = default)
        {
            var allRecords = new List<ToolDefinitionRecord>();

            lock (_syncLock)
            {
                if (_disposed) return 0;
            }

            foreach (var provider in _providers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var records = await provider.LoadDefinitionsAsync(cancellationToken).ConfigureAwait(false);
                    allRecords.AddRange(records);
                }
                catch (Exception ex)
                {
                    global::System.Diagnostics.Trace.WriteLine($"[DynamicToolRegistryLoader] Failed to load from '{provider.ProviderSource}': {ex.Message}");
                }
            }

            int loadedCount = 0;
            lock (_syncLock)
            {
                foreach (var rec in allRecords)
                {
                    try
                    {
                        var tool = _factory.CreateTool(rec);
                        _registry.Register(tool);
                        loadedCount++;
                    }
                    catch (Exception ex)
                    {
                        global::System.Diagnostics.Trace.WriteLine($"[DynamicToolRegistryLoader] Failed to create tool '{rec.Name}': {ex.Message}");
                    }
                }
            }

            RegistryReloaded?.Invoke(this, loadedCount);
            return loadedCount;
        }

        private async void OnProviderDefinitionsChanged(object? sender, EventArgs e)
        {
            try
            {
                await LoadAllAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Trace.WriteLine($"[DynamicToolRegistryLoader] Error on hot-reload: {ex.Message}");
            }
        }

        public void Dispose()
        {
            lock (_syncLock)
            {
                if (_disposed) return;
                _disposed = true;

                foreach (var p in _providers)
                {
                    p.DefinitionsChanged -= OnProviderDefinitionsChanged;
                    p.Dispose();
                }
                _providers.Clear();
            }
        }
    }
}
