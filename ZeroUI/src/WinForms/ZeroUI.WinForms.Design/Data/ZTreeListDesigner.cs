using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Windows.Forms;
using ZeroUI.WinForms.Data;
using ZeroUI.WinForms.Design.Forms;

namespace ZeroUI.WinForms.Design.Data
{
    /// <summary>
    /// Smart Tag Action List for ZTreeList.
    /// </summary>
    public class ZTreeListActionList : ZeroActionList<ZTreeList>
    {
        public ZTreeListActionList(IComponent component) : base(component)
        {
        }

        public string KeyFieldName
        {
            get => GetProperty<string>(nameof(ZTreeList.KeyFieldName)) ?? string.Empty;
            set => SetProperty(nameof(ZTreeList.KeyFieldName), value);
        }

        public string ParentFieldName
        {
            get => GetProperty<string>(nameof(ZTreeList.ParentFieldName)) ?? string.Empty;
            set => SetProperty(nameof(ZTreeList.ParentFieldName), value);
        }

        public bool ShowCheckBoxes
        {
            get => GetProperty<bool>(nameof(ZTreeList.ShowCheckBoxes));
            set => SetProperty(nameof(ZTreeList.ShowCheckBoxes), value);
        }

        public bool ShowLines
        {
            get => GetProperty<bool>(nameof(ZTreeList.ShowLines));
            set => SetProperty(nameof(ZTreeList.ShowLines), value);
        }

        public bool ShowColumnHeaders
        {
            get => GetProperty<bool>(nameof(ZTreeList.ShowColumnHeaders));
            set => SetProperty(nameof(ZTreeList.ShowColumnHeaders), value);
        }

        public DockStyle Dock
        {
            get => Control.Dock;
            set => SetProperty(nameof(Control.Dock), value);
        }

        public void LaunchColumnDesigner()
        {
            using var dlg = new TreeListDesignerForm(Control);
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                var changeService = GetService(typeof(IComponentChangeService)) as IComponentChangeService;
                changeService?.OnComponentChanged(Control, null, null, null);
            }
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            var items = new DesignerActionItemCollection();

            items.Add(new DesignerActionHeaderItem("Hierarchy & Columns"));
            items.Add(new DesignerActionMethodItem(this, nameof(LaunchColumnDesigner), "Edit Columns & Schema...", "Hierarchy & Columns", "Opens visual TreeList column schema designer", true));

            items.Add(new DesignerActionPropertyItem(nameof(KeyFieldName), "Key Field Name", "Hierarchy & Columns", "Unique node ID field"));
            items.Add(new DesignerActionPropertyItem(nameof(ParentFieldName), "Parent Field Name", "Hierarchy & Columns", "Parent node ID field"));

            items.Add(new DesignerActionHeaderItem("Appearance & Visuals"));
            items.Add(new DesignerActionPropertyItem(nameof(ShowCheckBoxes), "Show CheckBoxes", "Appearance & Visuals", "Toggle node selection checkboxes"));
            items.Add(new DesignerActionPropertyItem(nameof(ShowLines), "Show Tree Lines", "Appearance & Visuals", "Toggle hierarchy dotted connecting lines"));
            items.Add(new DesignerActionPropertyItem(nameof(ShowColumnHeaders), "Show Column Headers", "Appearance & Visuals", "Toggle column header bar"));
            items.Add(new DesignerActionPropertyItem(nameof(Dock), "Dock in Container", "Appearance & Visuals", "Fill parent container"));

            return items;
        }
    }

    /// <summary>
    /// Component Designer for ZTreeList.
    /// </summary>
    public class ZTreeListDesigner : ZeroControlDesigner<ZTreeList>
    {
        protected override DesignerActionList CreateActionList() => new ZTreeListActionList(Component);

        public override DesignerVerbCollection Verbs
        {
            get
            {
                var verbs = base.Verbs;
                verbs.Insert(0, new DesignerVerb("Edit Columns & Schema...", (s, e) =>
                {
                    using var dlg = new TreeListDesignerForm(TargetControl);
                    dlg.ShowDialog();
                }));
                return verbs;
            }
        }
    }
}
