using System;
using System.Linq;
using System.Reflection;
using Xunit;
using ZeroUI.Core.Industrial;
using WpfSevenSegment = ZeroUI.Wpf.Industrial.SevenSegment;
using WinFormsSevenSegment = ZeroUI.WinForms.Industrial.SevenSegment;

namespace ZeroUI.Desktop.Tests
{
    public class SevenSegmentParityTests
    {
        [Fact]
        public void BothPlatforms_ExposeHarmonizedStandardProperties()
        {
            var expectedProperties = new[]
            {
                "Value",
                "SegmentColor",
                "DimColor",
                "ColorPreset",
                "DigitCount",
                "SlantAngle",
                "LeadingZeroMode",
                "FrameStyle",
                "ShowGhostSegments",
                "ShowGlow",
                "BlinkColon",
                "Blink",
                "BlinkInterval",
                "Unit",
                "UnitColor"
            };

            var wfProps = typeof(WinFormsSevenSegment).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name).ToHashSet();
            var wpfProps = typeof(WpfSevenSegment).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name).ToHashSet();

            foreach (var prop in expectedProperties)
            {
                Assert.True(wfProps.Contains(prop), $"WinForms SevenSegment must expose property '{prop}'.");
                Assert.True(wpfProps.Contains(prop), $"WPF SevenSegment must expose property '{prop}'.");
            }
        }

        [Fact]
        public void DefaultPropertyValues_MatchBetweenWinFormsAndWpf()
        {
            StaTestRunner.Run(() =>
            {
                using var wf = new WinFormsSevenSegment();
                var wpf = new WpfSevenSegment();

                Assert.Equal(wf.Value, wpf.Value);
                Assert.Equal(wf.DigitCount, wpf.DigitCount);
                Assert.Equal((double)wf.SlantAngle, wpf.SlantAngle);
                Assert.Equal(wf.BlinkInterval, wpf.BlinkInterval);
                Assert.Equal((int)wf.LeadingZeroMode, (int)wpf.LeadingZeroMode);
                Assert.Equal((int)wf.FrameStyle, (int)wpf.FrameStyle);
                Assert.Equal(wf.ShowGhostSegments, wpf.ShowGhostSegments);
                Assert.Equal(wf.ShowGlow, wpf.ShowGlow);
                Assert.Equal(wf.BlinkColon, wpf.BlinkColon);
                Assert.Equal(wf.Blink, wpf.Blink);
                Assert.Equal(wf.Unit, wpf.Unit);
            });
        }

        [Fact]
        public void WpfBackwardCompatibilityShims_PreserveLegacyCallers()
        {
            StaTestRunner.Run(() =>
            {
                var wpf = new WpfSevenSegment();

#pragma warning disable CS0618 // Type or member is obsolete
                // Test ValueText forwards to Value
                wpf.ValueText = "8888";
                Assert.Equal("8888", wpf.Value);
                Assert.Equal("8888", wpf.ValueText);

                // Test Title forwards to Unit
                wpf.Title = "RPM";
                Assert.Equal("RPM", wpf.Unit);
                Assert.Equal("RPM", wpf.Title);

                // Test LedColor forwards to SegmentColor
                var cyan = System.Windows.Media.Color.FromRgb(56, 189, 248);
                wpf.LedColor = cyan;
                Assert.Equal(cyan, wpf.SegmentColor);
                Assert.Equal(cyan, wpf.LedColor);
#pragma warning restore CS0618
            });
        }

        [Fact]
        public void CoreSevenSegmentState_DecodesAlphanumericPatternsCorrectly()
        {
            // Digits
            Assert.Equal(0x3F, SevenSegmentState.GetPattern('0'));
            Assert.Equal(0x06, SevenSegmentState.GetPattern('1'));
            Assert.Equal(0x7F, SevenSegmentState.GetPattern('8'));

            // Letters for SCADA telemetry
            Assert.Equal(0x79, SevenSegmentState.GetPattern('E')); // E
            Assert.Equal(0x50, SevenSegmentState.GetPattern('r')); // r
            Assert.Equal(0x40, SevenSegmentState.GetPattern('-')); // Dash
            Assert.Equal(0x00, SevenSegmentState.GetPattern(' ')); // Space

            // Value parsing with colons and decimals
            var parsed = SevenSegmentState.ParseValue("12:45.8", 6, LeadingZeroDisplayMode.Blank);
            Assert.True(parsed.Any(p => p.IsColon), "Colon item must be recognized.");
            Assert.True(parsed.Any(p => p.HasDecimal), "Decimal item must be recognized.");
        }
    }
}
