using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using ZeroUI.WinForms.Editors;

namespace ZeroUI.WinForms.Design.Editors
{
    /// <summary>
    /// Smart Tag Action List for ZDateEdit.
    /// </summary>
    public class ZDateEditActionList : ZeroActionList<ZDateEdit>
    {
        public ZDateEditActionList(IComponent component) : base(component)
        {
        }

        public DateTime Value
        {
            get => GetProperty<DateTime>(nameof(ZDateEdit.Value));
            set => SetProperty(nameof(ZDateEdit.Value), value);
        }

        public string DateFormat
        {
            get => GetProperty<string>(nameof(ZDateEdit.DateFormat)) ?? "yyyy-MM-dd";
            set => SetProperty(nameof(ZDateEdit.DateFormat), value);
        }

        public bool ShowPresets
        {
            get => GetProperty<bool>(nameof(ZDateEdit.ShowPresets));
            set => SetProperty(nameof(ZDateEdit.ShowPresets), value);
        }

        public bool ReadOnly
        {
            get => GetProperty<bool>(nameof(ZDateEdit.ReadOnly));
            set => SetProperty(nameof(ZDateEdit.ReadOnly), value);
        }

        public void SetIsoDateFormat()
        {
            SetProperty(nameof(ZDateEdit.DateFormat), "yyyy-MM-dd");
        }

        public void SetDateTimeFormat()
        {
            SetProperty(nameof(ZDateEdit.DateFormat), "yyyy-MM-dd HH:mm:ss");
        }

        public void SetShortDateFormat()
        {
            SetProperty(nameof(ZDateEdit.DateFormat), "dd/MM/yyyy");
        }

        public override DesignerActionItemCollection GetSortedActionItems()
        {
            var items = new DesignerActionItemCollection();

            items.Add(new DesignerActionHeaderItem("Format Presets"));
            items.Add(new DesignerActionMethodItem(this, nameof(SetIsoDateFormat), "ISO Format (yyyy-MM-dd)", "Format Presets", "Sets ISO-8601 standard date format", true));
            items.Add(new DesignerActionMethodItem(this, nameof(SetDateTimeFormat), "Date & Time (yyyy-MM-dd HH:mm:ss)", "Format Presets", "Sets date and time timestamp format"));
            items.Add(new DesignerActionMethodItem(this, nameof(SetShortDateFormat), "Short Date (dd/MM/yyyy)", "Format Presets", "Sets local enterprise date format"));

            items.Add(new DesignerActionHeaderItem("Calendar & Behavior"));
            items.Add(new DesignerActionPropertyItem(nameof(DateFormat), "Custom Format String", "Calendar & Behavior", "Date display mask"));
            items.Add(new DesignerActionPropertyItem(nameof(ShowPresets), "Show Quick Preset Pills", "Calendar & Behavior", "Today / Yesterday / +7 Days quick buttons"));
            items.Add(new DesignerActionPropertyItem(nameof(ReadOnly), "Read Only", "Calendar & Behavior", "Prevent text entry"));

            return items;
        }
    }

    /// <summary>
    /// Component Designer for ZDateEdit.
    /// </summary>
    public class ZDateEditDesigner : ZeroControlDesigner<ZDateEdit>
    {
        protected override DesignerActionList CreateActionList() => new ZDateEditActionList(Component);
    }
}
