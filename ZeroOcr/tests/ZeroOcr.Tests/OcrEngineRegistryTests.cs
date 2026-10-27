using System;
using Xunit;
using ZeroOcr.Core.Engines;
using ZeroOcr.Windows;

namespace ZeroOcr.Tests;

public class OcrEngineRegistryTests
{
    [Fact]
    public void Registry_RegistersAndResolvesEngines()
    {
        var registry = new OcrEngineRegistry();

        var mock = new MockOcrEngine();
        registry.Register(mock, setAsDefault: true);

        var resolved = registry.GetEngine();
        Assert.NotNull(resolved);
        Assert.Equal("MockOcrEngine", resolved.Name);

        Assert.True(registry.TryGetEngine("MockOcrEngine", out var found));
        Assert.Same(mock, found);
    }

    [Fact]
    public void Registry_RegistersLazyFactoryAndExtension()
    {
        var registry = new OcrEngineRegistry();
        registry.RegisterWindowsOcr(setAsDefault: true);

        var engine = registry.GetEngine("Windows.Media.Ocr");
        Assert.NotNull(engine);
        Assert.IsType<WindowsOcrEngine>(engine);
        Assert.Equal("Windows.Media.Ocr", engine.Name);
    }
}
