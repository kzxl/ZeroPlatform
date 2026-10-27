using System;
using System.Reflection;
using Xunit;
using WpfLinearGauge = ZeroUI.Wpf.Industrial.LinearGauge;
using WinFormsLinearGauge = ZeroUI.WinForms.Industrial.ZLinearGauge;
using WinFormsProductionCounter = ZeroUI.WinForms.Industrial.ZProductionCounter;
using WinFormsCircularGauge = ZeroUI.WinForms.Industrial.ZCircularGauge;
using WinFormsPidFaceplate = ZeroUI.WinForms.Industrial.ZPidFaceplate;
using ZeroUI.WinForms.Base;
using ZeroUI.Core.Scada;

namespace ZeroUI.Desktop.Tests
{
    public class IndustrialReadoutRefactorTests
    {
        [Fact]
        public void WpfLinearGauge_DefaultProperties_PreservesCompatibility()
        {
            StaTestRunner.Run(() =>
            {
                var gauge = new WpfLinearGauge();
                Assert.Equal("0.#", gauge.ValueFormat);
                Assert.True(gauge.UseTabularReadout);
            });
        }

        [Fact]
        public void WpfLinearGauge_CustomValueFormat_UpdatesAndPersists()
        {
            StaTestRunner.Run(() =>
            {
                var gauge = new WpfLinearGauge();
                gauge.ValueFormat = "0.0";
                Assert.Equal("0.0", gauge.ValueFormat);

                gauge.ValueFormat = "0.00";
                Assert.Equal("0.00", gauge.ValueFormat);

                gauge.UseTabularReadout = false;
                Assert.False(gauge.UseTabularReadout);
            });
        }

        [Fact]
        public void WpfLinearGauge_FormatValue_SafeFallbackBehavior()
        {
            StaTestRunner.Run(() =>
            {
                var gauge = new WpfLinearGauge();
                MethodInfo? formatMethod = typeof(WpfLinearGauge).GetMethod("FormatValue", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(formatMethod);

                // Default 0.#
                string? resultDefault = formatMethod!.Invoke(gauge, new object[] { 50.0 }) as string;
                Assert.Equal("50", resultDefault);

                string? resultDecimal = formatMethod.Invoke(gauge, new object[] { 50.4 }) as string;
                Assert.Equal("50.4", resultDecimal);

                // Fixed format 0.0
                gauge.ValueFormat = "0.0";
                string? resultFixed = formatMethod.Invoke(gauge, new object[] { 50.0 }) as string;
                Assert.Equal("50.0", resultFixed);

                // Invalid format strings fall back to 0.# without throwing
                gauge.ValueFormat = "Q";
                string? resultInvalid = formatMethod.Invoke(gauge, new object[] { 50.0 }) as string;
                Assert.Equal("50", resultInvalid);

                gauge.ValueFormat = "{0:unclosed";
                string? resultComposite = formatMethod.Invoke(gauge, new object[] { 50.0 }) as string;
                Assert.Equal("50", resultComposite);
            });
        }

        [Fact]
        public void WinFormsLinearGauge_InheritsControlBase_AndSupportsFormatting()
        {
            using var gauge = new WinFormsLinearGauge();
            Assert.IsAssignableFrom<ControlBase>(gauge);
            Assert.Equal("0.#", gauge.ValueFormat);
            Assert.True(gauge.UseTabularReadout);

            gauge.ValueFormat = "0.00";
            Assert.Equal("0.00", gauge.ValueFormat);

            MethodInfo? formatMethod = typeof(WinFormsLinearGauge).GetMethod("FormatValue", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(formatMethod);

            gauge.ValueFormat = "0.#";
            string? res1 = formatMethod!.Invoke(gauge, new object[] { 40.0f }) as string;
            Assert.Equal("40", res1);

            gauge.ValueFormat = "0.0";
            string? res2 = formatMethod.Invoke(gauge, new object[] { 40.0f }) as string;
            Assert.Equal("40.0", res2);
        }

        [Fact]
        public void ProductionCounter_InheritsControlBase_AndSupportsPercentFormat()
        {
            using var counter = new WinFormsProductionCounter();
            Assert.IsAssignableFrom<ControlBase>(counter);
            Assert.Equal("0.#", counter.PercentFormat);

            counter.Plan = 1000;
            counter.Actual = 750;
            Assert.Equal(75.0, counter.CompletionPercent);
            Assert.Equal(250, counter.Remaining);

            MethodInfo? fmtMethod = typeof(WinFormsProductionCounter).GetMethod("FormatPercent", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(fmtMethod);

            string? pctDefault = fmtMethod!.Invoke(counter, new object[] { 75.0 }) as string;
            Assert.Equal("75%", pctDefault);

            counter.PercentFormat = "0.0";
            string? pctFixed = fmtMethod.Invoke(counter, new object[] { 75.0 }) as string;
            Assert.Equal("75.0%", pctFixed);
        }

        [Fact]
        public void CircularGauge_InheritsControlBase_AndSupportsValueFormat()
        {
            using var gauge = new WinFormsCircularGauge();
            Assert.IsAssignableFrom<ControlBase>(gauge);
            Assert.Equal("0.0", gauge.ValueFormat);

            gauge.Value = 85.5f;
            gauge.Suffix = "%";

            MethodInfo? fmtMethod = typeof(WinFormsCircularGauge).GetMethod("FormatValue", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(fmtMethod);

            string? resDefault = fmtMethod!.Invoke(gauge, new object[] { 85.5f }) as string;
            Assert.Equal("85.5%", resDefault);

            gauge.ValueFormat = "0";
            string? resInt = fmtMethod.Invoke(gauge, new object[] { 85.5f }) as string;
            Assert.Equal("86%", resInt);
        }

        [Fact]
        public void PidFaceplate_InheritsControlBase_AndSupportsModesAndFormat()
        {
            using var pid = new WinFormsPidFaceplate();
            Assert.IsAssignableFrom<ControlBase>(pid);
            Assert.Equal("0.0", pid.ValueFormat);
            Assert.Equal(ZeroPidMode.Auto, pid.Mode);

            pid.Mode = ZeroPidMode.Manual;
            Assert.Equal(ZeroPidMode.Manual, pid.Mode);

            MethodInfo? fmtMethod = typeof(WinFormsPidFaceplate).GetMethod("FormatTelemetry", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(fmtMethod);

            string? resDefault = fmtMethod!.Invoke(pid, new object[] { 48.2 }) as string;
            Assert.Equal("48.2", resDefault);

            pid.ValueFormat = "0.00";
            string? resCustom = fmtMethod.Invoke(pid, new object[] { 48.2 }) as string;
            Assert.Equal("48.20", resCustom);
        }
    }
}
