using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ZeroAgent.Tools.Dynamic.Model;

namespace ZeroAgent.Tools.Dynamic.Provider
{
    /// <summary>
    /// Loads tool definitions from JSON manifest files on disk with FileSystemWatcher hot-reloading.
    /// </summary>
    public sealed class JsonFileToolDefinitionProvider : IToolDefinitionProvider
    {
        private readonly string _targetPath;
        private readonly bool _isDirectory;
        private readonly FileSystemWatcher? _watcher;
        private readonly Timer? _debounceTimer;
        private readonly object _lock = new object();
        private bool _disposed;

        public string ProviderSource => $"JsonFile: {_targetPath}";

        public event EventHandler? DefinitionsChanged;

        public JsonFileToolDefinitionProvider(string filePathOrDirectory, bool enableHotReload = true)
        {
            _targetPath = filePathOrDirectory ?? throw new ArgumentNullException(nameof(filePathOrDirectory));
            _isDirectory = Directory.Exists(_targetPath);

            if (enableHotReload)
            {
                try
                {
                    string watchDir = _isDirectory 
                        ? _targetPath 
                        : (Path.GetDirectoryName(Path.GetFullPath(_targetPath)) ?? ".");

                    string filter = _isDirectory 
                        ? "*.json" 
                        : Path.GetFileName(_targetPath);

                    if (Directory.Exists(watchDir))
                    {
                        _debounceTimer = new Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

                        _watcher = new FileSystemWatcher(watchDir, filter)
                        {
                            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                            EnableRaisingEvents = true
                        };

                        _watcher.Changed += OnFileChanged;
                        _watcher.Created += OnFileChanged;
                        _watcher.Deleted += OnFileChanged;
                        _watcher.Renamed += (s, e) => OnFileChanged(s, e);
                    }
                }
                catch
                {
                    // Non-fatal if FileSystemWatcher fails due to permissions or environment
                }
            }
        }

        public Task<IReadOnlyList<ToolDefinitionRecord>> LoadDefinitionsAsync(CancellationToken cancellationToken = default)
        {
            var results = new List<ToolDefinitionRecord>();

            if (_isDirectory && Directory.Exists(_targetPath))
            {
                var files = Directory.GetFiles(_targetPath, "*.json", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    LoadFileIntoList(file, results);
                }
            }
            else if (File.Exists(_targetPath))
            {
                LoadFileIntoList(_targetPath, results);
            }

            return Task.FromResult<IReadOnlyList<ToolDefinitionRecord>>(results);
        }

        private static void LoadFileIntoList(string filePath, List<ToolDefinitionRecord> results)
        {
            try
            {
                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json)) return;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("tools", out var toolsElem) && toolsElem.ValueKind == JsonValueKind.Array)
                {
                    var manifest = JsonSerializer.Deserialize<ToolsManifestFile>(json, options);
                    if (manifest?.Tools != null)
                    {
                        results.AddRange(manifest.Tools.FindAll(t => t.IsActive));
                    }
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    var list = JsonSerializer.Deserialize<List<ToolDefinitionRecord>>(json, options);
                    if (list != null)
                    {
                        results.AddRange(list.FindAll(t => t.IsActive));
                    }
                }
            }
            catch (Exception ex)
            {
                global::System.Diagnostics.Trace.WriteLine($"[JsonFileToolDefinitionProvider] Error parsing '{filePath}': {ex.Message}");
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce file system events by 250ms
            _debounceTimer?.Change(250, Timeout.Infinite);
        }

        private void OnDebounceElapsed(object? state)
        {
            lock (_lock)
            {
                if (!_disposed)
                {
                    DefinitionsChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                _debounceTimer?.Dispose();
                _watcher?.Dispose();
            }
        }
    }
}
