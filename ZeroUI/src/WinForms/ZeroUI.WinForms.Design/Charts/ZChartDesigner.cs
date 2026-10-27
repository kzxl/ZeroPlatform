using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Windows.Forms;
using ZeroUI.WinForms.Charts;
using ZeroUI.WinForms.Design.Forms;

namespace ZeroUI.WinForms.Design.Charts
{
    /// <summary>
    /// Smart Tag Action List for ZChart.
    /// </summary>
    public class ZChartActionList : ZeroActionList<ZChart>
    {
        public ZChartActionList(IComponent component) : base(component)
        {
        }

        public bool ShowLegend
        {
            get => Control.LegendPosition != ZeroUI.WinForms.Charts.Model.ChartLegendPosition.None;
            set => SetProperty(nameof(ZChart.LegendPosition), value ? ZeroUI.WinForms.Charts.Model.ChartLegendPosition.Top : ZeroUI.WinForms.Charts.Model.ChartLegendPosition.None);
        }

        public ZeroUI.WinForms.Charts.Model.ChartLegendPosition LegendPosition
        {
            get => GetProperty<ZeroUI.WinForms.Charts.Model.ChartLegendPosition>(nameof(ZChart.LegendPosition));
            set => SetProperty(nameof(ZChart.LegendPosition), value);
        }

        public bool ShowTooltips
        {
            get => GetProperty<bool>(nameof(ZChart.ShowTooltips));
            set => SetProperty(nameof(ZChart.ShowTooltips), value);
        }

        public bool ShowCrosshair
        {
            get => GetProperty<bool>(nameof(ZChart.ShowCrosshair));
            set => SetProperty(nameof(ZChart.ShowCrosshair), value);
        }

        public DockStyle Dock
        {
            get => Control.Dock;
            set => SetProperty(nameof(Control.Dock), value);
        }

        public void LaunchWizard()
        {
            using var dlg = new ChartWizardForm(Control);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var changeService = GetService(typeof(IComponentChangeService)) as IComponentChangeService;
                changeService?.OnComponentChanged(Control, null, null, null);
            }
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            var items = new DesignerActionItemCollection();

            items.Add(new DesignerActionHeaderItem("Chart Configuration"));
            items.Add(new DesignerActionMethodItem(this, nameof(LaunchWizard), "Run Chart Wizard...", "Chart Configuration", "Opens visual 3-step chart wizard", true));

            items.Add(new DesignerActionHeaderItem("Display & HUD"));
            items.Add(new DesignerActionPropertyItem(nameof(ShowLegend), "Show Legend", "Display & HUD", "Toggle series legend box"));
            items.Add(new DesignerActionPropertyItem(nameof(LegendPosition), "Legend Position", "Display & HUD", "Set legend position (Top/Bottom/Right/None)"));
            items.Add(new DesignerActionPropertyItem(nameof(ShowTooltips), "Show Hover Tooltips", "Display & HUD", "Toggle interactive hover tooltips"));
            items.Add(new DesignerActionPropertyItem(nameof(ShowCrosshair), "Show Crosshair Cursor", "Display & HUD", "Toggle cursor tracking lines"));
            items.Add(new DesignerActionPropertyItem(nameof(Dock), "Dock in Container", "Display & HUD", "Fill parent container"));

            return items;
        }
    }

    /// <summary>
    /// Component Designer for ZChart.
    /// </summary>
    public class ZChartDesigner : ZeroControlDesigner<ZChart>
    {
        protected override DesignerActionList CreateActionList() => new ZChartActionList(Component);

        public override DesignerVerbCollection Verbs
        {
            get
            {
                var verbs = base.Verbs;
                verbs.Insert(0, new DesignerVerb("Run Chart Wizard...", (s, e) =>
                {
                    using var dlg = new ChartWizardForm(TargetControl);
                    dlg.ShowDialog();
                }));
                return verbs;
            }
        }
    }
}
