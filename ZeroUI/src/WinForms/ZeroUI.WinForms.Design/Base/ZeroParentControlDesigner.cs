using System;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace ZeroUI.WinForms.Design
{
    /// <summary>
    /// Base Component Designer for container controls supporting visual drag-and-drop of child controls inside Visual Studio.
    /// </summary>
    /// <typeparam name="TControl">Target container control type.</typeparam>
    public abstract class ZeroParentControlDesigner<TControl> : ParentControlDesigner where TControl : Control
    {
        private DesignerActionListCollection? _actionLists;

        public TControl TargetControl => (TControl)Control;

        public override DesignerActionListCollection ActionLists
        {
            get
            {
                if (_actionLists == null)
                {
                    _actionLists = new DesignerActionListCollection();
                    var list = CreateActionList();
                    if (list != null)
                    {
                        _actionLists.Add(list);
                    }
                }
                return _actionLists;
            }
        }

        public override DesignerVerbCollection Verbs
        {
            get
            {
                var verbs = new DesignerVerbCollection();
                verbs.Add(new DesignerVerb("Dock in Parent Container", (s, e) => ToggleDockFill()));
                verbs.Add(new DesignerVerb("Undock / Restore", (s, e) => Undock()));
                return verbs;
            }
        }

        protected virtual DesignerActionList? CreateActionList() => null;

        protected void ToggleDockFill()
        {
            var prop = TypeDescriptor.GetProperties(Control)[nameof(Control.Dock)];
            if (prop != null)
            {
                var current = (DockStyle)(prop.GetValue(Control) ?? DockStyle.None);
                prop.SetValue(Control, current == DockStyle.Fill ? DockStyle.None : DockStyle.Fill);
            }
        }

        protected void Undock()
        {
            var prop = TypeDescriptor.GetProperties(Control)[nameof(Control.Dock)];
            prop?.SetValue(Control, DockStyle.None);
        }
    }
}
