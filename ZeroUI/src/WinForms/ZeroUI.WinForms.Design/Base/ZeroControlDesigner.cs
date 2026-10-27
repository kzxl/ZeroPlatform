using System;
using System.Collections;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace ZeroUI.WinForms.Design
{
    /// <summary>
    /// Base Component Designer for single-element ZeroUI WinForms controls.
    /// Provides Smart Tag ActionLists, designer verbs, and property filtering.
    /// </summary>
    /// <typeparam name="TControl">Target WinForms control type.</typeparam>
    public abstract class ZeroControlDesigner<TControl> : ControlDesigner where TControl : Control
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
                verbs.Add(new DesignerVerb("ZeroUI Control Documentation...", (s, e) => OpenDocumentation()));
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

        protected virtual void OpenDocumentation()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/kzxl/ZeroUI",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
