using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Windows.Forms;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;
using ZeroUI.WinForms.DataGrid;
using ZeroUI.WinForms.Design.Forms;

namespace ZeroUI.WinForms.Design.Data
{
    /// <summary>
    /// Smart Tag Action List for ZGrid.
    /// </summary>
    public class ZGridActionList : ZeroActionList<ZGrid>
    {
        public ZGridActionList(IComponent component) : base(component)
        {
        }

        public GridDensity Density
        {
            get => GetProperty<GridDensity>(nameof(ZGrid.Density));
            set => SetProperty(nameof(ZGrid.Density), value);
        }

        public GridViewType ViewType
        {
            get => GetProperty<GridViewType>(nameof(ZGrid.ViewType));
            set => SetProperty(nameof(ZGrid.ViewType), value);
        }

        public bool ShowFooter
        {
            get => GetProperty<bool>(nameof(ZGrid.ShowFooter));
            set => SetProperty(nameof(ZGrid.ShowFooter), value);
        }

        public bool ShowAutoFilterRow
        {
            get => GetProperty<bool>(nameof(ZGrid.ShowAutoFilterRow));
            set => SetProperty(nameof(ZGrid.ShowAutoFilterRow), value);
        }

        public bool ShowGroupPanel
        {
            get => GetProperty<bool>(nameof(ZGrid.ShowGroupPanel));
            set => SetProperty(nameof(ZGrid.ShowGroupPanel), value);
        }

        public DockStyle Dock
        {
            get => Control.Dock;
            set => SetProperty(nameof(Control.Dock), value);
        }

        public void LaunchDesigner()
        {
            using var dlg = new GridDesignerForm(Control);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var changeService = GetService(typeof(IComponentChangeService)) as IComponentChangeService;
                changeService?.OnComponentChanged(Control, null, null, null);
            }
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            var items = new DesignerActionItemCollection();

            // Section: In-Place Designer
            items.Add(new DesignerActionHeaderItem("Design-Time Schema"));
            items.Add(new DesignerActionMethodItem(this, nameof(LaunchDesigner), "Run Grid Designer & Column Editor...", "Design-Time Schema", "Opens visual column editor dialog", true));

            // Section: Appearance & Layout
            items.Add(new DesignerActionHeaderItem("Grid Layout"));
            items.Add(new DesignerActionPropertyItem(nameof(ViewType), "Presentation View", "Grid Layout", "Table, CardView, or TileView"));
            items.Add(new DesignerActionPropertyItem(nameof(Density), "Row Density", "Grid Layout", "Compact, Middle, or Large"));
            items.Add(new DesignerActionPropertyItem(nameof(Dock), "Dock in Container", "Grid Layout", "Fill parent container"));

            // Section: Visual Headers & Footers
            items.Add(new DesignerActionHeaderItem("Toolbars & Adorners"));
            items.Add(new DesignerActionPropertyItem(nameof(ShowGroupPanel), "Show Grouping Panel", "Toolbars & Adorners", "Drag-to-group header panel"));
            items.Add(new DesignerActionPropertyItem(nameof(ShowAutoFilterRow), "Show Auto Filter Row", "Toolbars & Adorners", "In-line filter row"));
            items.Add(new DesignerActionPropertyItem(nameof(ShowFooter), "Show Summary Footer", "Toolbars & Adorners", "Toggle aggregate summary footer"));

            return items;
        }
    }

    /// <summary>
    /// Component Designer for ZGrid.
    /// </summary>
    public class ZGridDesigner : ZeroControlDesigner<ZGrid>
    {
        protected override DesignerActionList CreateActionList() => new ZGridActionList(Component);

        public override DesignerVerbCollection Verbs
        {
            get
            {
                var verbs = base.Verbs;
                verbs.Insert(0, new DesignerVerb("Run Grid Designer...", (s, e) =>
                {
                    using var dlg = new GridDesignerForm(TargetControl);
                    dlg.ShowDialog();
                }));
                return verbs;
            }
        }
    }
}
