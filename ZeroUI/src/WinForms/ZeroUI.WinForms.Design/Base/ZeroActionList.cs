using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Design
{
    /// <summary>
    /// Base Designer Action List for ZeroUI controls providing Smart Tag items and property reflection helpers.
    /// </summary>
    /// <typeparam name="TControl">The target component control type.</typeparam>
    public abstract class ZeroActionList<TControl> : DesignerActionList where TControl : Control
    {
        protected readonly TControl Control;
        protected readonly DesignerActionUIService? ActionUiService;

        protected ZeroActionList(IComponent component) : base(component)
        {
            Control = (TControl)component;
            ActionUiService = GetService(typeof(DesignerActionUIService)) as DesignerActionUIService;
        }

        /// <summary>
        /// Gets a property value safely through TypeDescriptor to support designer undo/redo transactions.
        /// </summary>
        protected TProp? GetProperty<TProp>(string propertyName)
        {
            var prop = TypeDescriptor.GetProperties(Control)[propertyName];
            if (prop != null)
            {
                object? val = prop.GetValue(Control);
                if (val is TProp typedVal) return typedVal;
            }
            return default;
        }

        /// <summary>
        /// Sets a property value safely through TypeDescriptor, triggering designer transactions and property grid refreshes.
        /// </summary>
        protected void SetProperty(string propertyName, object? value)
        {
            var prop = TypeDescriptor.GetProperties(Control)[propertyName];
            if (prop != null)
            {
                prop.SetValue(Control, value);
                ActionUiService?.Refresh(Control);
            }
        }

        /// <summary>
        /// Toggles docking between DockStyle.Fill and DockStyle.None.
        /// </summary>
        public void ToggleDockInParent()
        {
            if (Control.Dock == DockStyle.Fill)
            {
                SetProperty(nameof(Control.Dock), DockStyle.None);
            }
            else
            {
                SetProperty(nameof(Control.Dock), DockStyle.Fill);
            }
        }
    }
}
