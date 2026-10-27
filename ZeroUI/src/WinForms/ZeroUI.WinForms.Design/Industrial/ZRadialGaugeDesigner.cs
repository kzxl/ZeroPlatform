using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using ZeroUI.WinForms.Industrial;

namespace ZeroUI.WinForms.Design.Industrial
{
    /// <summary>
    /// Smart Tag Action List for ZRadialGauge.
    /// </summary>
    public class ZRadialGaugeActionList : ZeroActionList<ZRadialGauge>
    {
        public ZRadialGaugeActionList(IComponent component) : base(component)
        {
        }

        public double Minimum
        {
            get => GetProperty<double>(nameof(ZRadialGauge.Minimum));
            set => SetProperty(nameof(ZRadialGauge.Minimum), value);
        }

        public double Maximum
        {
            get => GetProperty<double>(nameof(ZRadialGauge.Maximum));
            set => SetProperty(nameof(ZRadialGauge.Maximum), value);
        }

        public double Value
        {
            get => GetProperty<double>(nameof(ZRadialGauge.Value));
            set => SetProperty(nameof(ZRadialGauge.Value), value);
        }

        public string Title
        {
            get => GetProperty<string>(nameof(ZRadialGauge.Title)) ?? string.Empty;
            set => SetProperty(nameof(ZRadialGauge.Title), value);
        }

        public string Unit
        {
            get => GetProperty<string>(nameof(ZRadialGauge.Unit)) ?? string.Empty;
            set => SetProperty(nameof(ZRadialGauge.Unit), value);
        }

        public float SweepAngle
        {
            get => GetProperty<float>(nameof(ZRadialGauge.SweepAngle));
            set => SetProperty(nameof(ZRadialGauge.SweepAngle), value);
        }

        public bool EnableDamping
        {
            get => GetProperty<bool>(nameof(ZRadialGauge.EnableDamping));
            set => SetProperty(nameof(ZRadialGauge.EnableDamping), value);
        }

        public string? BoundTagPath
        {
            get => GetProperty<string?>(nameof(ZRadialGauge.BoundTagPath));
            set => SetProperty(nameof(ZRadialGauge.BoundTagPath), value);
        }

        public void PresetIndustrialPressure()
        {
            SetProperty(nameof(ZRadialGauge.Title), "Main Boiler Pressure");
            SetProperty(nameof(ZRadialGauge.Unit), "bar");
            SetProperty(nameof(ZRadialGauge.Minimum), 0.0);
            SetProperty(nameof(ZRadialGauge.Maximum), 16.0);
            SetProperty(nameof(ZRadialGauge.Value), 10.5);
            SetProperty(nameof(ZRadialGauge.WarningThreshold), 12.0);
            SetProperty(nameof(ZRadialGauge.DangerThreshold), 14.5);
        }

        public void PresetTemperature()
        {
            SetProperty(nameof(ZRadialGauge.Title), "Reactor Core Temp");
            SetProperty(nameof(ZRadialGauge.Unit), "°C");
            SetProperty(nameof(ZRadialGauge.Minimum), 0.0);
            SetProperty(nameof(ZRadialGauge.Maximum), 150.0);
            SetProperty(nameof(ZRadialGauge.Value), 82.0);
            SetProperty(nameof(ZRadialGauge.WarningThreshold), 110.0);
            SetProperty(nameof(ZRadialGauge.DangerThreshold), 135.0);
        }

        public void PresetRpmSpeed()
        {
            SetProperty(nameof(ZRadialGauge.Title), "Turbine RPM");
            SetProperty(nameof(ZRadialGauge.Unit), "kRPM");
            SetProperty(nameof(ZRadialGauge.Minimum), 0.0);
            SetProperty(nameof(ZRadialGauge.Maximum), 10.0);
            SetProperty(nameof(ZRadialGauge.Value), 6.2);
            SetProperty(nameof(ZRadialGauge.WarningThreshold), 8.0);
            SetProperty(nameof(ZRadialGauge.DangerThreshold), 9.2);
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            var items = new DesignerActionItemCollection();

            items.Add(new DesignerActionHeaderItem("SCADA Presets"));
            items.Add(new DesignerActionMethodItem(this, nameof(PresetIndustrialPressure), "Apply Pressure Gauge Preset (0..16 bar)", "SCADA Presets", "Configures boiler pressure telemetry scale", true));
            items.Add(new DesignerActionMethodItem(this, nameof(PresetTemperature), "Apply Temperature Preset (0..150 °C)", "SCADA Presets", "Configures reactor temperature scale"));
            items.Add(new DesignerActionMethodItem(this, nameof(PresetRpmSpeed), "Apply Turbine RPM Preset (0..10 kRPM)", "SCADA Presets", "Configures turbine RPM scale"));

            items.Add(new DesignerActionHeaderItem("Scale & Telemetry"));
            items.Add(new DesignerActionPropertyItem(nameof(Title), "Title", "Scale & Telemetry", "Instrument caption"));
            items.Add(new DesignerActionPropertyItem(nameof(Unit), "Unit", "Scale & Telemetry", "Engineering unit string"));
            items.Add(new DesignerActionPropertyItem(nameof(Minimum), "Minimum", "Scale & Telemetry", "Scale lower bound"));
            items.Add(new DesignerActionPropertyItem(nameof(Maximum), "Maximum", "Scale & Telemetry", "Scale upper bound"));
            items.Add(new DesignerActionPropertyItem(nameof(Value), "Current Value", "Scale & Telemetry", "Indicated dial value"));
            items.Add(new DesignerActionPropertyItem(nameof(BoundTagPath), "Bound Tag Path", "Scale & Telemetry", "SCADA runtime tag binding path"));

            items.Add(new DesignerActionHeaderItem("Dial Geometry"));
            items.Add(new DesignerActionPropertyItem(nameof(SweepAngle), "Sweep Angle (°)", "Dial Geometry", "Angular span (180° or 270°)"));
            items.Add(new DesignerActionPropertyItem(nameof(EnableDamping), "Enable Needle Damping", "Dial Geometry", "Smooth needle inertia physics"));

            return items;
        }
    }

    /// <summary>
    /// Component Designer for ZRadialGauge.
    /// </summary>
    public class ZRadialGaugeDesigner : ZeroControlDesigner<ZRadialGauge>
    {
        protected override DesignerActionList CreateActionList() => new ZRadialGaugeActionList(Component);
    }
}
