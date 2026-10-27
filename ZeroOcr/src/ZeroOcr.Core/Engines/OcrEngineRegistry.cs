using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ZeroOcr.Core.Interfaces;

namespace ZeroOcr.Core.Engines;

/// <summary>
/// Thread-safe sovereign registry and locator for OCR engine plugins.
/// Follows Universe Plugin Architecture v4.0 (Contract-First, Self-Registration).
/// </summary>
public sealed class OcrEngineRegistry
{
    private readonly ConcurrentDictionary<string, IOcrEngine> _engines = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Func<IOcrEngine>> _factories = new(StringComparer.OrdinalIgnoreCase);
    private volatile string? _defaultEngineName;

    private static readonly Lazy<OcrEngineRegistry> _defaultInstance = new(() => new OcrEngineRegistry());

    /// <summary>
    /// Global shared singleton instance of the OCR engine registry.
    /// </summary>
    public static OcrEngineRegistry Default => _defaultInstance.Value;

    /// <summary>
    /// Registers a concrete engine instance.
    /// </summary>
    public OcrEngineRegistry Register(IOcrEngine engine, bool setAsDefault = false)
    {
        if (engine == null) throw new ArgumentNullException(nameof(engine));

        _engines[engine.Name] = engine;
        if (setAsDefault || _defaultEngineName == null)
        {
            _defaultEngineName = engine.Name;
        }

        return this;
    }

    /// <summary>
    /// Registers a lazy engine factory to instantiate the engine only on first demand.
    /// </summary>
    public OcrEngineRegistry RegisterFactory(string name, Func<IOcrEngine> factory, bool setAsDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Engine name cannot be empty.", nameof(name));
        if (factory == null) throw new ArgumentNullException(nameof(factory));

        _factories[name] = factory;
        if (setAsDefault || _defaultEngineName == null)
        {
            _defaultEngineName = name;
        }

        return this;
    }

    /// <summary>
    /// Sets the default OCR engine name.
    /// </summary>
    public void SetDefault(string engineName)
    {
        if (string.IsNullOrWhiteSpace(engineName))
            throw new ArgumentException("Engine name cannot be empty.", nameof(engineName));

        _defaultEngineName = engineName;
    }

    /// <summary>
    /// Resolves an OCR engine by name, or returns the default registered engine if name is null.
    /// </summary>
    public IOcrEngine? GetEngine(string? engineName = null)
    {
        string? targetName = engineName ?? _defaultEngineName;

        if (string.IsNullOrEmpty(targetName))
        {
            // Fallback: Pick the first available engine
            return GetAvailableEngines().FirstOrDefault();
        }

        string targetKey = targetName!;
        if (_engines.TryGetValue(targetKey, out var existing))
        {
            return existing;
        }

        if (_factories.TryGetValue(targetKey, out var factory))
        {
            var created = factory();
            _engines[targetKey] = created;
            return created;
        }

        return null;
    }

    /// <summary>
    /// Resolves an OCR engine by name safely with a boolean return.
    /// </summary>
    public bool TryGetEngine(string engineName, out IOcrEngine? engine)
    {
        engine = GetEngine(engineName);
        return engine != null;
    }

    /// <summary>
    /// Returns a list of all currently registered and available OCR engines.
    /// </summary>
    public IReadOnlyList<IOcrEngine> GetAvailableEngines()
    {
        var resolved = new List<IOcrEngine>(_engines.Values);

        foreach (var kvp in _factories)
        {
            if (!_engines.ContainsKey(kvp.Key))
            {
                try
                {
                    var instance = kvp.Value();
                    _engines[kvp.Key] = instance;
                    resolved.Add(instance);
                }
                catch
                {
                    // Ignore factory failures during discovery
                }
            }
        }

        return resolved.Where(e => e.IsAvailable).ToList();
    }

    /// <summary>
    /// Clears all registered engines and factories.
    /// </summary>
    public void Clear()
    {
        _engines.Clear();
        _factories.Clear();
        _defaultEngineName = null;
    }
}
