using System;
using System.Globalization;
using System.Reflection;
using Xunit;
using WpfRadialGauge = ZeroUI.Wpf.Industrial.RadialGauge;
using WinFormsRadialGauge = ZeroUI.WinForms.Industrial.ZRadialGauge;

namespace ZeroUI.Desktop.Tests
{
    public class RadialGaugeReadoutTests
    {
        [Fact]
        public void WpfRadialGauge_DefaultProperties_PreservesCompatibility()
        {
            StaTestRunner.Run(() =>
            {
                var gauge = new WpfRadialGauge();
                Assert.Equal("0.#", gauge.ValueFormat);
                Assert.True(gauge.UseTabularReadout);
            });
        }

        [Fact]
        public void WpfRadialGauge_CustomValueFormat_UpdatesAndPersists()
        {
            StaTestRunner.Run(() =>
            {
                var gauge = new WpfRadialGauge();
                gauge.ValueFormat = "0.0";
                Assert.Equal("0.0", gauge.ValueFormat);

                gauge.ValueFormat = "F2";
                Assert.Equal("F2", gauge.ValueFormat);

                gauge.UseTabularReadout = false;
                Assert.False(gauge.UseTabularReadout);
            });
        }

        [Fact]
        public void WpfRadialGauge_FormatValue_SafeFallbackBehavior()
        {
            StaTestRunner.Run(() =>
            {
                var gauge = new WpfRadialGauge();

                MethodInfo? formatMethod = typeof(WpfRadialGauge).GetMethod("FormatValue", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.NotNull(formatMethod);

                // Default 0.#
                string? resultDefault = formatMethod!.Invoke(gauge, new object[] { 85.0 }) as string;
                Assert.Equal("85", resultDefault);

                string? resultWithDecimal = formatMethod.Invoke(gauge, new object[] { 84.1 }) as string;
                Assert.Equal("84.1", resultWithDecimal);

                // Fixed format 0.0 prevents integer crossing jump
                gauge.ValueFormat = "0.0";
                string? resultFixed = formatMethod.Invoke(gauge, new object[] { 85.0 }) as string;
                Assert.Equal("85.0", resultFixed);

                // Blank format falls back to 0.#
                gauge.ValueFormat = "";
                string? resultBlank = formatMethod.Invoke(gauge, new object[] { 85.0 }) as string;
                Assert.Equal("85", resultBlank);

                // Invalid format strings fall back to 0.# without throwing
                gauge.ValueFormat = "Q"; // 'Q' is not a valid standard format specifier in .NET, throws FormatException
                string? resultInvalidNumeric = formatMethod.Invoke(gauge, new object[] { 85.0 }) as string;
                Assert.Equal("85", resultInvalidNumeric);

                gauge.ValueFormat = "{0:unclosed"; // Malformed composite format causes FormatException
                string? resultInvalidComposite = formatMethod.Invoke(gauge, new object[] { 85.0 }) as string;
                Assert.Equal("85", resultInvalidComposite);
            });
        }

        [Fact]
        public void WinFormsRadialGauge_DefaultProperties_PreservesCompatibility()
        {
            using var gauge = new WinFormsRadialGauge();
            Assert.Equal("0.#", gauge.ValueFormat);
            Assert.True(gauge.UseTabularReadout);
        }

        [Fact]
        public void WinFormsRadialGauge_CustomValueFormat_UpdatesAndPersists()
        {
            using var gauge = new WinFormsRadialGauge();
            gauge.ValueFormat = "0.0";
            Assert.Equal("0.0", gauge.ValueFormat);

            gauge.UseTabularReadout = false;
            Assert.False(gauge.UseTabularReadout);
        }

        [Fact]
        public void WinFormsRadialGauge_FormatValue_SafeFallbackBehavior()
        {
            using var gauge = new WinFormsRadialGauge();

            MethodInfo? formatMethod = typeof(WinFormsRadialGauge).GetMethod("FormatValue", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(formatMethod);

            // Default 0.#
            string? resultDefault = formatMethod!.Invoke(gauge, new object[] { 85.0 }) as string;
            Assert.Equal("85", resultDefault);

            // Fixed format 0.0 prevents integer crossing jump
            gauge.ValueFormat = "0.0";
            string? resultFixed = formatMethod.Invoke(gauge, new object[] { 85.0 }) as string;
            Assert.Equal("85.0", resultFixed);

            // Blank format falls back to 0.#
            gauge.ValueFormat = "   ";
            string? resultBlank = formatMethod.Invoke(gauge, new object[] { 85.0 }) as string;
            Assert.Equal("85", resultBlank);

            // Invalid formats fall back safely to 0.#
            gauge.ValueFormat = "Q";
            string? resultInvalidNumeric = formatMethod.Invoke(gauge, new object[] { 85.0 }) as string;
            Assert.Equal("85", resultInvalidNumeric);

            gauge.ValueFormat = "{0:unclosed";
            string? resultInvalidComposite = formatMethod.Invoke(gauge, new object[] { 85.0 }) as string;
            Assert.Equal("85", resultInvalidComposite);
        }

        [Fact]
        public void WinFormsRadialGauge_OnPaint_RendersWithoutException()
        {
            using var gauge = new WinFormsRadialGauge
            {
                Width = 200,
                Height = 200,
                Value = 84.1,
                ValueFormat = "0.0",
                UseTabularReadout = true
            };

            using var bmp = new System.Drawing.Bitmap(200, 200);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            using var pe = new System.Windows.Forms.PaintEventArgs(g, new System.Drawing.Rectangle(0, 0, 200, 200));

            // Test render execution
            MethodInfo? onPaint = typeof(WinFormsRadialGauge).GetMethod("OnPaint", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(onPaint);
            onPaint!.Invoke(gauge, new object[] { pe });

            // Integer crossing value render
            gauge.Value = 85.0;
            onPaint.Invoke(gauge, new object[] { pe });
        }
    }
}
