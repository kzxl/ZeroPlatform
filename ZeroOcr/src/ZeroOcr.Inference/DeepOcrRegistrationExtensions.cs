using System;
using ZeroOcr.Core.Engines;
using ZeroOcr.Inference.Engines;

namespace ZeroOcr.Inference;

/// <summary>
/// Sovereign OCR engine registration extensions for ZeroPlatform.
/// Enables transparent service location via OcrEngineRegistry and DI containers.
/// </summary>
public static class DeepOcrRegistrationExtensions
{
    public const string EngineName = "ZeroPlatform.DeepOcr.Sovereign";

    /// <summary>
    /// Registers the sovereign Deep OCR engine into the registry.
    /// </summary>
    /// <param name="registry">The registry instance, or default if null.</param>
    /// <param name="engine">Optional pre-configured engine instance.</param>
    /// <param name="setAsDefault">Whether to designate this as the primary default engine.</param>
    public static OcrEngineRegistry RegisterDeepOcr(
        this OcrEngineRegistry? registry,
        ZeroDeepOcrEngine? engine = null,
        bool setAsDefault = true)
    {
        var target = registry ?? OcrEngineRegistry.Default;
        target.RegisterFactory(EngineName, () => engine ?? new ZeroDeepOcrEngine(), setAsDefault);
        return target;
    }

    /// <summary>
    /// Registers a lazy factory for creating the sovereign Deep OCR engine on first call.
    /// </summary>
    public static OcrEngineRegistry RegisterDeepOcrFactory(
        this OcrEngineRegistry? registry,
        Func<ZeroDeepOcrEngine> factory,
        bool setAsDefault = true)
    {
        if (factory == null) throw new ArgumentNullException(nameof(factory));
        var target = registry ?? OcrEngineRegistry.Default;
        target.RegisterFactory(EngineName, factory, setAsDefault);
        return target;
    }
}
