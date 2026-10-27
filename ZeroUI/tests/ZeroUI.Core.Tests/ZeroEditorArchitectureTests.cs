using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Data;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Input;
using ZeroUI.Core.Input.Date;

namespace ZeroUI.Core.Tests
{
    public class ZeroEditorArchitectureTests
    {
        private class MockTypedEditor<T> : IZeroEditor<T> where T : notnull
        {
            private T _value = default!;
            private bool _isModified;
            private bool _readOnly;

            public event EventHandler<ValueChangingEventArgs<T>>? ValueChanging;
            public event EventHandler? ValueChanged;
            public event EventHandler? EditValueChanged;

            public T Value
            {
                get => _value;
                set
                {
                    if (EqualityComparer<T>.Default.Equals(_value, value)) return;

                    var args = new ValueChangingEventArgs<T>(_value, value);
                    ValueChanging?.Invoke(this, args);
                    if (args.Cancel) return;

                    _value = value;
                    _isModified = true;
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }

            public object? EditValue
            {
                get => _value;
                set
                {
                    if (value == null)
                    {
                        Value = default!;
                    }
                    else if (value is T typed)
                    {
                        Value = typed;
                    }
                    else
                    {
                        try
                        {
                            Value = (T)Convert.ChangeType(value, typeof(T));
                        }
                        catch
                        {
                            Value = default!;
                        }
                    }
                }
            }

            public bool IsModified
            {
                get => _isModified;
                set => _isModified = value;
            }

            public bool ReadOnly
            {
                get => _readOnly;
                set => _readOnly = value;
            }

            public void Reset()
            {
                _value = default!;
                _isModified = false;
                ValueChanged?.Invoke(this, EventArgs.Empty);
                EditValueChanged?.Invoke(this, EventArgs.Empty);
            }

            public void Clear() => Reset();
        }

        [Fact]
        public void TypedEditor_ValueChanging_CanCancelMutation()
        {
            var editor = new MockTypedEditor<int>();
            editor.Value = 10;
            Assert.Equal(10, editor.Value);

            // Cancel any value > 100
            editor.ValueChanging += (s, e) =>
            {
                if (e.NewValue > 100)
                {
                    e.Cancel = true;
                }
            };

            editor.Value = 50;
            Assert.Equal(50, editor.Value);

            editor.Value = 150; // Should be cancelled
            Assert.Equal(50, editor.Value);
        }

        [Fact]
        public void TypedEditor_EditValue_BridgesToTypedValue()
        {
            var editor = new MockTypedEditor<decimal>();
            editor.EditValue = "123.45";

            Assert.Equal(123.45m, editor.Value);
            Assert.True(editor.IsModified);

            editor.Reset();
            Assert.Equal(0m, editor.Value);
            Assert.False(editor.IsModified);
        }

        [Fact]
        public void ZeroClipboardHelper_FormatTsv_GeneratesValidTsv()
        {
            var matrix = new string[,]
            {
                { "ID", "Name", "Score" },
                { "101", "Alice", "95.5" },
                { "102", "Bob", "88.0" }
            };

            string tsv = ZeroClipboardHelper.FormatTsv(matrix);

            Assert.Equal("ID\tName\tScore\r\n101\tAlice\t95.5\r\n102\tBob\t88.0", tsv);
        }

        [Fact]
        public void ZeroClipboardHelper_ParseTsv_ParsesZeroAllocCellsCorrectly()
        {
            string tsv = "Header1\tHeader2\r\nVal1\tVal2\r\nVal3\tVal4";
            var parsed = new List<(int row, int col, string val)>();

            ZeroClipboardHelper.ParseTsv(tsv.AsSpan(), (row, col, cellSpan) =>
            {
                parsed.Add((row, col, cellSpan.ToString()));
            });

            Assert.Equal(6, parsed.Count);
            Assert.Equal((0, 0, "Header1"), parsed[0]);
            Assert.Equal((0, 1, "Header2"), parsed[1]);
            Assert.Equal((1, 0, "Val1"), parsed[2]);
            Assert.Equal((1, 1, "Val2"), parsed[3]);
            Assert.Equal((2, 0, "Val3"), parsed[4]);
            Assert.Equal((2, 1, "Val4"), parsed[5]);
        }

        [Fact]
        public void EditorButtonModel_DefaultProperties_InitializedCorrectly()
        {
            var btn = new EditorButtonModel(EditorButtonKind.BrowseFile);

            Assert.Equal(EditorButtonKind.BrowseFile, btn.Kind);
            Assert.True(btn.Visible);
            Assert.True(btn.IsEnabled);
            Assert.Equal("Browse file...", btn.ToolTip);
        }

        [Theory]
        [InlineData(EditorButtonKind.BrowseFile, "Browse file...")]
        [InlineData(EditorButtonKind.BrowseFolder, "Browse folder...")]
        [InlineData(EditorButtonKind.Clear, "Clear input")]
        [InlineData(EditorButtonKind.Copy, "Copy to clipboard")]
        [InlineData(EditorButtonKind.Search, "Search")]
        [InlineData(EditorButtonKind.DropDown, "Show options")]
        [InlineData(EditorButtonKind.Undo, "Undo")]
        [InlineData(EditorButtonKind.Redo, "Redo")]
        [InlineData(EditorButtonKind.Custom, "")]
        public void EditorButtonModel_GetDefaultToolTip_ReturnsExpectedText(EditorButtonKind kind, string expected)
        {
            string tooltip = EditorButtonModel.GetDefaultToolTip(kind);
            Assert.Equal(expected, tooltip);
        }

        [Fact]
        public void EditorButtonClickEventArgs_HandledFlag_CanBeSet()
        {
            var args = new EditorButtonClickEventArgs(EditorButtonKind.Clear, "tag_123");

            Assert.Equal(EditorButtonKind.Clear, args.Kind);
            Assert.Equal("tag_123", args.Tag);
            Assert.False(args.Handled);

            args.Handled = true;
            Assert.True(args.Handled);
        }

        [Fact]
        public void TokenItem_ConstructorsAndEquality_WorkCorrectly()
        {
            var t1 = new TokenItem("Sensor-A1");
            var t2 = new TokenItem("sensor-a1", 101, "📡", "#10B981");
            var t3 = new TokenItem("Sensor-B2");

            Assert.Equal("Sensor-A1", t1.Text);
            Assert.Equal("Sensor-A1", t1.ToString());
            Assert.Equal(101, t2.Value);
            Assert.Equal("📡", t2.Glyph);
            Assert.Equal("#10B981", t2.ColorHex);

            // Case-insensitive token equality
            Assert.True(t1.Equals(t2));
            Assert.True(t1 == t2);
            Assert.False(t1 == t3);
            Assert.Equal(t1.GetHashCode(), t2.GetHashCode());
        }

        [Fact]
        public void TokenChangedEventArgs_InitializedCorrectly()
        {
            var token = new TokenItem("Priority-Hot");
            var args = new TokenChangedEventArgs(TokenChangeAction.Added, token);

            Assert.Equal(TokenChangeAction.Added, args.Action);
            Assert.Equal(token, args.Item);
        }

        [Fact]
        public void ColorPalette_StandardPalette_ContainsValidHexColors()
        {
            Assert.NotEmpty(ColorPalette.StandardPalette);
            Assert.True(ColorPalette.StandardPalette.Count >= 20);

            foreach (var hex in ColorPalette.StandardPalette)
            {
                Assert.StartsWith("#", hex);
                Assert.True(hex.Length == 7 || hex.Length == 9);
            }
        }

        [Fact]
        public void ColorPalette_IndustrialStatusPalette_ContainsExpectedColors()
        {
            Assert.NotEmpty(ColorPalette.IndustrialStatusPalette);
            Assert.Contains("#10B981", ColorPalette.IndustrialStatusPalette); // Running / Normal
            Assert.Contains("#EF4444", ColorPalette.IndustrialStatusPalette); // Alarm / Critical
        }

        [Fact]
        public void TimeSpanModel_SteppingAndClamping_WorkCorrectly()
        {
            var model = new TimeSpanModel(TimeSpan.FromHours(2))
            {
                Minimum = TimeSpan.Zero,
                Maximum = TimeSpan.FromDays(5)
            };

            Assert.Equal(TimeSpan.FromHours(2), model.Value);

            // Step Hours
            model.FocusedPart = TimeSpanPart.Hours;
            model.StepUp();
            Assert.Equal(TimeSpan.FromHours(3), model.Value);

            model.StepDown();
            Assert.Equal(TimeSpan.FromHours(2), model.Value);

            // Step Minutes
            model.FocusedPart = TimeSpanPart.Minutes;
            model.StepUp();
            Assert.Equal(new TimeSpan(2, 1, 0), model.Value);

            // Bounds clamping
            model.Value = TimeSpan.FromDays(10);
            Assert.Equal(TimeSpan.FromDays(5), model.Value); // Clamped to maximum

            model.Value = TimeSpan.FromDays(-1);
            Assert.Equal(TimeSpan.Zero, model.Value); // Clamped to minimum
        }

        [Fact]
        public void TimeSpanModel_FormattingAndParsing_WorkCorrectly()
        {
            var ts = new TimeSpan(1, 2, 30, 45); // 1d 02:30:45
            string clock = TimeSpanModel.FormatTimeSpan(ts, TimeSpanFormatMode.Clock, showDays: true);
            Assert.Contains("1d", clock);
            Assert.Contains("02:30:45", clock);

            string verbose = TimeSpanModel.FormatTimeSpan(ts, TimeSpanFormatMode.Verbose, showDays: true);
            Assert.Contains("1d", verbose);
            Assert.Contains("2h", verbose);
            Assert.Contains("30m", verbose);
            Assert.Contains("45s", verbose);

            // Parsing shorthand
            Assert.True(TimeSpanModel.TryParse("90s", out var parsedSec));
            Assert.Equal(TimeSpan.FromSeconds(90), parsedSec);

            Assert.True(TimeSpanModel.TryParse("15m", out var parsedMin));
            Assert.Equal(TimeSpan.FromMinutes(15), parsedMin);

            Assert.True(TimeSpanModel.TryParse("2h", out var parsedHr));
            Assert.Equal(TimeSpan.FromHours(2), parsedHr);

            Assert.True(TimeSpanModel.TryParse("3d", out var parsedDay));
            Assert.Equal(TimeSpan.FromDays(3), parsedDay);

            Assert.True(TimeSpanModel.TryParse("01:15:30", out var parsedClock));
            Assert.Equal(new TimeSpan(1, 15, 30), parsedClock);
        }

        [Fact]
        public void HyperlinkModel_DetectKindAndArgs_WorkCorrectly()
        {
            Assert.Equal(HyperlinkEditKind.Web, HyperlinkHelper.DetectKind("https://zeroui.io"));
            Assert.Equal(HyperlinkEditKind.Email, HyperlinkHelper.DetectKind("support@zeroui.io"));
            Assert.Equal(HyperlinkEditKind.Email, HyperlinkHelper.DetectKind("mailto:admin@zeroui.io"));
            Assert.Equal(HyperlinkEditKind.File, HyperlinkHelper.DetectKind("C:\\ZeroPlatform\\Config.json"));

            Assert.True(HyperlinkHelper.TryCreateUri("zeroui.io", out var uri));
            Assert.NotNull(uri);
            Assert.Equal("https", uri!.Scheme);

            var args = new HyperlinkNavigateEventArgs("https://zeroui.io", uri);
            Assert.False(args.Cancel);
            Assert.False(args.Handled);
            args.Cancel = true;
            Assert.True(args.Cancel);
        }

        [Fact]
        public void RangeSpanModel_BoundsAndSpanTranslation_WorkCorrectly()
        {
            var model = new RangeSpanModel(0f, 100f, 20f, 80f, 5f, 10f);

            Assert.Equal(20f, model.LowerValue);
            Assert.Equal(80f, model.UpperValue);
            Assert.Equal(60f, model.Span);
            Assert.Equal(0.2f, model.LowerFraction, 2);
            Assert.Equal(0.8f, model.UpperFraction, 2);

            // Translate span forward
            model.TranslateSpan(15f);
            Assert.Equal(35f, model.LowerValue);
            Assert.Equal(95f, model.UpperValue);

            // Translate span past maximum bound
            model.TranslateSpan(20f);
            Assert.Equal(100f, model.UpperValue);
            Assert.Equal(40f, model.LowerValue); // Preserves 60 span

            // Min separation constraint
            model.LowerValue = 95f;
            Assert.Equal(90f, model.LowerValue); // 100 - minRangeSpan (10)
        }

        [Fact]
        public void ZeroColor_HexParsingAndLuminance_WorkCorrectly()
        {
            // Standard 6-char hex
            Assert.True(ZeroColor.TryParseHex("#4F46E5", out var c1));
            Assert.Equal(79, c1.R);
            Assert.Equal(70, c1.G);
            Assert.Equal(229, c1.B);
            Assert.Equal(255, c1.A);
            Assert.Equal("#4F46E5", c1.ToHex());

            // 3-char shorthand
            Assert.True(ZeroColor.TryParseHex("#FFF", out var c2));
            Assert.Equal(255, c2.R);
            Assert.Equal(255, c2.G);
            Assert.Equal(255, c2.B);
            Assert.False(c2.IsDarkColor);
            Assert.Equal("#0F172A", c2.GetContrastingHex());

            // 8-char hex with alpha
            Assert.True(ZeroColor.TryParseHex("#80FF0000", out var c3));
            Assert.Equal(128, c3.A);
            Assert.Equal(255, c3.R);
            Assert.Equal(0, c3.G);
            Assert.Equal(0, c3.B);

            // Dark color contrast
            var dark = new ZeroColor(15, 23, 42);
            Assert.True(dark.IsDarkColor);
            Assert.Equal("#FFFFFF", dark.GetContrastingHex());
        }

        [Fact]
        public void TokenModel_CollectionManagementAndFiltering_WorkCorrectly()
        {
            var model = new TokenModel
            {
                AllowDuplicates = false,
                MaxTokens = 3
            };

            Assert.True(model.Add("Tag1"));
            Assert.True(model.Add("Tag2"));
            Assert.False(model.Add("tag1")); // Duplicate rejected
            Assert.Equal(2, model.Count);

            Assert.True(model.Add("Tag3"));
            Assert.False(model.Add("Tag4")); // MaxTokens reached
            Assert.Equal(3, model.Count);

            // Remove last
            Assert.True(model.RemoveLast());
            Assert.Equal(2, model.Count);

            // Delimited parsing
            model.MaxTokens = 0;
            model.SetFromDelimitedString("Alpha, Beta; Gamma");
            Assert.Equal(3, model.Count);
            Assert.Equal("Alpha, Beta, Gamma", model.ToDelimitedString());

            // Suggestion filtering
            model.AvailableTokens = new[]
            {
                new TokenItem("Alpha-1"),
                new TokenItem("Alpha-2"),
                new TokenItem("Beta-1")
            };
            var suggestions = model.FilterSuggestions("Alpha");
            Assert.Equal(2, suggestions.Count);
        }

        [Fact]
        public void IPAddressModel_ClampingAndTextParsing_WorkCorrectly()
        {
            var model = new IPAddressModel(192, 168, 1, 100);
            Assert.Equal("192.168.1.100", model.Text);
            Assert.Equal("192.168.1.100", model.Address.ToString());

            // Clamping
            model.SetOctet(3, 300); // Exceeds 255
            Assert.Equal(255, model.Octet4);

            model.SetOctet(3, -10); // Below 0
            Assert.Equal(0, model.Octet4);

            // String parsing
            Assert.True(model.TrySetFromText("10.0.0.1"));
            Assert.Equal(10, model.Octet1);
            Assert.Equal(0, model.Octet2);
            Assert.Equal(0, model.Octet3);
            Assert.Equal(1, model.Octet4);

            Assert.False(model.TrySetFromText("invalid.ip"));
            Assert.False(model.TrySetFromText("256.0.0.1"));
        }

        [Fact]
        public void RatingModel_FractionalSnappingAndSpatialScoring_WorkCorrectly()
        {
            var model = new RatingModel(3.2m, maxRating: 5, allowHalf: true);
            // 3.2 snaps to nearest 0.5 -> 3.0
            Assert.Equal(3.0m, model.Value);

            model.Value = 4.4m;
            // 4.4 snaps to nearest 0.5 -> 4.5
            Assert.Equal(4.5m, model.Value);

            // AllowHalf = false
            model.AllowHalf = false;
            // 4.5 rounds to 5.0
            Assert.Equal(5.0m, model.Value);

            // Spatial score calculation (itemSize = 20, itemSpacing = 10, startX = 0)
            model.AllowHalf = true;
            Assert.Equal(0.5m, model.CalculateScoreFromPosition(5, 0, 20, 10));
            Assert.Equal(1.0m, model.CalculateScoreFromPosition(15, 0, 20, 10));
            Assert.Equal(1.5m, model.CalculateScoreFromPosition(35, 0, 20, 10));
            Assert.Equal(2.0m, model.CalculateScoreFromPosition(45, 0, 20, 10));

            // Keyboard stepping
            model.Value = 3.0m;
            model.StepUp();
            Assert.Equal(3.5m, model.Value);
            model.StepDown();
            Assert.Equal(3.0m, model.Value);

            // Toggle off
            model.Value = 1.0m;
            model.ToggleOrSet(1.0m);
            Assert.Equal(0m, model.Value);
        }

        [Fact]
        public void DateRangePresetHelper_PresetCalculations_WorkAccurately()
        {
            var baseDate = new DateTime(2026, 9, 11);

            var todayRange = DateRangePresetHelper.CalculateRange(DateRangePreset.Today, baseDate);
            Assert.Equal(baseDate, todayRange.Start);
            Assert.Equal(baseDate, todayRange.End);

            var last7 = DateRangePresetHelper.CalculateRange(DateRangePreset.Last7Days, baseDate);
            Assert.Equal(baseDate.AddDays(-6), last7.Start);
            Assert.Equal(baseDate, last7.End);

            var thisMonthMtd = DateRangePresetHelper.CalculateRange(DateRangePreset.ThisMonth, baseDate, fullMonthForThisMonth: false);
            Assert.Equal(new DateTime(2026, 9, 1), thisMonthMtd.Start);
            Assert.Equal(baseDate, thisMonthMtd.End);

            var thisMonthFull = DateRangePresetHelper.CalculateRange(DateRangePreset.ThisMonth, baseDate, fullMonthForThisMonth: true);
            Assert.Equal(new DateTime(2026, 9, 1), thisMonthFull.Start);
            Assert.Equal(new DateTime(2026, 9, 30), thisMonthFull.End);

            var ytd = DateRangePresetHelper.CalculateRange(DateRangePreset.YearToDate, baseDate);
            Assert.Equal(new DateTime(2026, 1, 1), ytd.Start);
            Assert.Equal(baseDate, ytd.End);
        }

        [Fact]
        public void PictureScaleMath_Calculations_AreAccurate()
        {
            // Target: 200x100, Image: 100x100
            PictureScaleMath.CalculateDestination(0, 0, 200, 100, 100, 100, ImageScaleMode.Stretch,
                out double sx, out double sy, out double sw, out double sh);
            Assert.Equal(0, sx);
            Assert.Equal(0, sy);
            Assert.Equal(200, sw);
            Assert.Equal(100, sh);

            // Center
            PictureScaleMath.CalculateDestination(0, 0, 200, 100, 100, 100, ImageScaleMode.Center,
                out double cx, out double cy, out double cw, out double ch);
            Assert.Equal(50, cx);
            Assert.Equal(0, cy);
            Assert.Equal(100, cw);
            Assert.Equal(100, ch);

            // Contain: fits inside 100x100 at center (x: 50)
            PictureScaleMath.CalculateDestination(0, 0, 200, 100, 100, 100, ImageScaleMode.Contain,
                out double fitX, out double fitY, out double fitW, out double fitH);
            Assert.Equal(50, fitX);
            Assert.Equal(0, fitY);
            Assert.Equal(100, fitW);
            Assert.Equal(100, fitH);

            // Cover: covers entire 200x100 -> scale 2.0 -> 200x200, centered vertically (y: -50)
            PictureScaleMath.CalculateDestination(0, 0, 200, 100, 100, 100, ImageScaleMode.Cover,
                out double covX, out double covY, out double covW, out double covH);
            Assert.Equal(0, covX);
            Assert.Equal(-50, covY);
            Assert.Equal(200, covW);
            Assert.Equal(200, covH);
        }

        [Fact]
        public void AvatarHelper_InitialsAndColors_WorkProperly()
        {
            Assert.Equal("JD", AvatarHelper.ExtractInitials("John Doe"));
            Assert.Equal("AD", AvatarHelper.ExtractInitials("Admin"));
            Assert.Equal("A", AvatarHelper.ExtractInitials("A"));
            Assert.Equal("AM", AvatarHelper.ExtractInitials("Anna-Marie"));
            Assert.Equal("AS", AvatarHelper.ExtractInitials("Anna_Smith"));
            Assert.Equal(string.Empty, AvatarHelper.ExtractInitials(""));
            Assert.Equal(string.Empty, AvatarHelper.ExtractInitials(null));

            string hex = AvatarHelper.GetDeterministicColorHex("op-001");
            Assert.StartsWith("#", hex);
            Assert.Equal(7, hex.Length);

            Assert.Equal("#10B981", AvatarHelper.GetStatusColorHex(AvatarStatus.Online));
            Assert.Equal("#EF4444", AvatarHelper.GetStatusColorHex(AvatarStatus.Busy));
            Assert.Equal("#F59E0B", AvatarHelper.GetStatusColorHex(AvatarStatus.Away));
            Assert.Equal("#64748B", AvatarHelper.GetStatusColorHex(AvatarStatus.Offline));
        }

        [Fact]
        public void LookUpFilterHelper_FilteringAndMatching_WorksProperly()
        {
            var item = new LookUpItem("ITEM-01", "Ball Bearing 6205", "Mechanical Parts", "Bearings");

            Assert.True(LookUpFilterHelper.Matches(item, "bearing"));
            Assert.True(LookUpFilterHelper.Matches(item, "ITEM-01"));
            Assert.True(LookUpFilterHelper.Matches(item, "mechanical"));
            Assert.True(LookUpFilterHelper.Matches(item, "bearings"));
            Assert.False(LookUpFilterHelper.Matches(item, "Electronics"));

            var list = new List<LookUpItem>
            {
                new LookUpItem("1", "Motor 5kW"),
                new LookUpItem("2", "Motor 10kW"),
                new LookUpItem("3", "Pump 2kW")
            };

            var motors = LookUpFilterHelper.Filter(list, "motor");
            Assert.Equal(2, motors.Count);

            var top1 = LookUpFilterHelper.Filter(list, "motor", 1);
            Assert.Single(top1);
        }

        [Fact]
        public void CheckedComboHelper_FormattingAndSelectAll_AreAccurate()
        {
            // Display text
            Assert.Equal("Select...", CheckedComboHelper.FormatDisplayText(Array.Empty<string>(), "Select...", "{0} selected"));
            Assert.Equal("Apple", CheckedComboHelper.FormatDisplayText(new[] { "Apple" }, "Select...", "{0} selected"));
            Assert.Equal("Apple, Banana", CheckedComboHelper.FormatDisplayText(new[] { "Apple", "Banana" }, "Select...", "{0} selected"));
            Assert.Equal("3 items selected", CheckedComboHelper.FormatDisplayText(new[] { "Apple", "Banana", "Cherry" }, "Select...", "{0} items selected"));

            // Select all tri-state
            Assert.Equal(false, CheckedComboHelper.CalculateSelectAllState(0, 5));
            Assert.Equal(false, CheckedComboHelper.CalculateSelectAllState(0, 0));
            Assert.Equal(true, CheckedComboHelper.CalculateSelectAllState(5, 5));
            Assert.Null(CheckedComboHelper.CalculateSelectAllState(2, 5));
        }

        [Fact]
        public void TextCounterHelper_FormattingAndWordCounting_WorksProperly()
        {
            Assert.Equal("12 / 500", TextCounterHelper.FormatCharacterCount(12, 500, false));
            Assert.Equal("12 / 500", TextCounterHelper.FormatCharacterCount(12, 500, true));
            Assert.Equal("12", TextCounterHelper.FormatCharacterCount(12, 0, false));
            Assert.Equal("12 chars", TextCounterHelper.FormatCharacterCount(12, 0, true));
            Assert.Equal("12", TextCounterHelper.FormatCharacterCount(12, 32767, false));

            Assert.Equal(4, TextCounterHelper.CountWords("Hello world from ZeroUI"));
            Assert.Equal(0, TextCounterHelper.CountWords("   "));
            Assert.Equal(0, TextCounterHelper.CountWords(null));

            Assert.Equal(3, TextCounterHelper.CountLines("Line 1\nLine 2\nLine 3"));
            Assert.Equal(1, TextCounterHelper.CountLines("Single line"));
            Assert.Equal(0, TextCounterHelper.CountLines(""));
        }
    }
}
