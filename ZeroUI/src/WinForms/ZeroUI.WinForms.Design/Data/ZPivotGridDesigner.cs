using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Windows.Forms;
using ZeroUI.WinForms.PivotGrid;

namespace ZeroUI.WinForms.Design.Data
{
    /// <summary>
    /// Smart Tag Action List for ZPivotGrid.
    /// </summary>
    public class ZPivotGridActionList : ZeroActionList<ZPivotGrid>
    {
        public ZPivotGridActionList(IComponent component) : base(component)
        {
        }

        public DockStyle Dock
        {
            get => Control.Dock;
            set => SetProperty(nameof(Control.Dock), value);
        }

        public void LaunchFieldChooser()
        {
            using var dlg = new PivotFieldChooserDialog(Control);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var changeService = GetService(typeof(IComponentChangeService)) as IComponentChangeService;
                changeService?.OnComponentChanged(Control, null, null, null);
            }
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            var items = new DesignerActionItemCollection();

            items.Add(new DesignerActionHeaderItem("OLAP Schema & Fields"));
            items.Add(new DesignerActionMethodItem(this, nameof(LaunchFieldChooser), "Open Field Chooser & Layout...", "OLAP Schema & Fields", "Opens OLAP dimensional field layout manager", true));

            items.Add(new DesignerActionHeaderItem("Layout & Display"));
            items.Add(new DesignerActionPropertyItem(nameof(Dock), "Dock in Container", "Layout & Display", "Fill parent container"));

            return items;
        }
    }

    /// <summary>
    /// Component Designer for ZPivotGrid.
    /// </summary>
    public class ZPivotGridDesigner : ZeroControlDesigner<ZPivotGrid>
    {
        protected override DesignerActionList CreateActionList() => new ZPivotGridActionList(Component);

        public override DesignerVerbCollection Verbs
        {
            get
            {
                var verbs = base.Verbs;
                verbs.Insert(0, new DesignerVerb("Open Field Chooser & Layout...", (s, e) =>
                {
                    using var dlg = new PivotFieldChooserDialog(TargetControl);
                    dlg.ShowDialog();
                }));
                return verbs;
            }
        }
    }
}
