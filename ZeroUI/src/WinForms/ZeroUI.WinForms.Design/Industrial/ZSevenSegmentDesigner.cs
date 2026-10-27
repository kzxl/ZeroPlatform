using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using ZeroUI.WinForms.Industrial;

namespace ZeroUI.WinForms.Design.Industrial
{
    /// <summary>
    /// Smart Tag Action List for ZSevenSegment.
    /// </summary>
    public class ZSevenSegmentActionList : ZeroActionList<ZSevenSegment>
    {
        public ZSevenSegmentActionList(IComponent component) : base(component)
        {
        }

        public string Value
        {
            get => GetProperty<string>(nameof(ZSevenSegment.Value)) ?? string.Empty;
            set => SetProperty(nameof(ZSevenSegment.Value), value);
        }

        public SevenSegmentColorPreset ColorPreset
        {
            get => GetProperty<SevenSegmentColorPreset>(nameof(ZSevenSegment.ColorPreset));
            set => SetProperty(nameof(ZSevenSegment.ColorPreset), value);
        }

        public int DigitCount
        {
            get => GetProperty<int>(nameof(ZSevenSegment.DigitCount));
            set => SetProperty(nameof(ZSevenSegment.DigitCount), value);
        }

        public float SlantAngle
        {
            get => GetProperty<float>(nameof(ZSevenSegment.SlantAngle));
            set => SetProperty(nameof(ZSevenSegment.SlantAngle), value);
        }

        public bool ShowGhostSegments
        {
            get => GetProperty<bool>(nameof(ZSevenSegment.ShowGhostSegments));
            set => SetProperty(nameof(ZSevenSegment.ShowGhostSegments), value);
        }

        public bool BlinkColon
        {
            get => GetProperty<bool>(nameof(ZSevenSegment.BlinkColon));
            set => SetProperty(nameof(ZSevenSegment.BlinkColon), value);
        }

        public string Unit
        {
            get => GetProperty<string>(nameof(ZSevenSegment.Unit)) ?? string.Empty;
            set => SetProperty(nameof(ZSevenSegment.Unit), value);
        }

        public void PresetClockDisplay()
        {
            SetProperty(nameof(ZSevenSegment.Value), "12:45:00");
            SetProperty(nameof(ZSevenSegment.DigitCount), 8);
            SetProperty(nameof(ZSevenSegment.ColorPreset), SevenSegmentColorPreset.NeonCyan);
            SetProperty(nameof(ZSevenSegment.BlinkColon), true);
            SetProperty(nameof(ZSevenSegment.Unit), "");
        }

        public void PresetCounter()
        {
            SetProperty(nameof(ZSevenSegment.Value), "004520");
            SetProperty(nameof(ZSevenSegment.DigitCount), 6);
            SetProperty(nameof(ZSevenSegment.ColorPreset), SevenSegmentColorPreset.NeonEmerald);
            SetProperty(nameof(ZSevenSegment.BlinkColon), false);
            SetProperty(nameof(ZSevenSegment.Unit), "PCS");
        }

        public void PresetAlarmReadout()
        {
            SetProperty(nameof(ZSevenSegment.Value), "HI-ALM");
            SetProperty(nameof(ZSevenSegment.DigitCount), 6);
            SetProperty(nameof(ZSevenSegment.ColorPreset), SevenSegmentColorPreset.NeonRed);
            SetProperty(nameof(ZSevenSegment.BlinkColon), false);
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            var items = new DesignerActionItemCollection();

            items.Add(new DesignerActionHeaderItem("Quick Presets"));
            items.Add(new DesignerActionMethodItem(this, nameof(PresetClockDisplay), "Clock Format (HH:mm:ss)", "Quick Presets", "Configures digital clock LED display", true));
            items.Add(new DesignerActionMethodItem(this, nameof(PresetCounter), "Parts Counter (004520 PCS)", "Quick Presets", "Configures piece counter display"));
            items.Add(new DesignerActionMethodItem(this, nameof(PresetAlarmReadout), "Alarm Indicator (HI-ALM)", "Quick Presets", "Configures high alarm annunciator"));

            items.Add(new DesignerActionHeaderItem("Display & Digits"));
            items.Add(new DesignerActionPropertyItem(nameof(Value), "Display Value", "Display & Digits", "Text or numeric string to display"));
            items.Add(new DesignerActionPropertyItem(nameof(DigitCount), "Digit Slots", "Display & Digits", "Number of digital positions"));
            items.Add(new DesignerActionPropertyItem(nameof(Unit), "Unit Suffix", "Display & Digits", "Measurement unit label"));

            items.Add(new DesignerActionHeaderItem("Optics & Style"));
            items.Add(new DesignerActionPropertyItem(nameof(ColorPreset), "LED Color Palette", "Optics & Style", "Neon Emerald, Cyan, Amber, Red, or White"));
            items.Add(new DesignerActionPropertyItem(nameof(SlantAngle), "Slant (Italic °)", "Optics & Style", "Angle in degrees (0..15)"));
            items.Add(new DesignerActionPropertyItem(nameof(ShowGhostSegments), "Show Ghost Segments", "Optics & Style", "Realistic unlit LCD/LED ghost physics"));
            items.Add(new DesignerActionPropertyItem(nameof(BlinkColon), "Blink Colon Separator", "Optics & Style", "1Hz blinking colon for clock formats"));

            return items;
        }
    }

    /// <summary>
    /// Component Designer for ZSevenSegment.
    /// </summary>
    public class ZSevenSegmentDesigner : ZeroControlDesigner<ZSevenSegment>
    {
        protected override DesignerActionList CreateActionList() => new ZSevenSegmentActionList(Component);
    }
}
