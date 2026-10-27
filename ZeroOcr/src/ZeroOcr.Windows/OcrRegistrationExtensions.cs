using System;
using ZeroOcr.Core.Engines;

namespace ZeroOcr.Windows;

/// <summary>
/// Plugin registration extension methods for ZeroOcr.Windows.
/// </summary>
public static class OcrRegistrationExtensions
{
    /// <summary>
    /// Registers the native Windows 10/11 OCR engine into the specified or default OcrEngineRegistry.
    /// </summary>
    public static OcrEngineRegistry RegisterWindowsOcr(this OcrEngineRegistry? registry, bool setAsDefault = true)
    {
        var targetRegistry = registry ?? OcrEngineRegistry.Default;
        targetRegistry.RegisterFactory("Windows.Media.Ocr", () => new WindowsOcrEngine(), setAsDefault);
        return targetRegistry;
    }
}
