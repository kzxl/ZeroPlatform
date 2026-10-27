using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Ribbon
{
    public enum RibbonItemStyle
    {
        /// <summary>
        /// Prominent large button with 32x32 icon on top and text below.
        /// </summary>
        Large,

        /// <summary>
        /// Compact small button with 16x16 icon on the left and text on the right.
        /// Stacked vertically up to 3 buttons per column.
        /// </summary>
        Small,

        /// <summary>
        /// Dropdown button with arrow glyph (⯆) that opens a sub-menu or approval list.
        /// </summary>
        DropDown
    }

    public enum RibbonGroupAlignment
    {
        /// <summary>
        /// Left-aligned standard functional group, flowing from left to right.
        /// </summary>
        Left,

        /// <summary>
        /// Right-aligned specialized group (such as approval queues, quick status, options).
        /// </summary>
        Right
    }

    /// <summary>
    /// Display mode configuration for approval queue groups on the ribbon.
    /// </summary>
    public enum RibbonApprovalDisplayMode
    {
        /// <summary>
        /// Automatically expands individual approval items if sufficient screen width is available;
        /// collapses into compact dropdown buttons when space is constrained.
        /// </summary>
        Auto,

        /// <summary>
        /// Always renders individual approval items directly on the ribbon canvas.
        /// </summary>
        Expanded,

        /// <summary>
        /// Always collapses approval queues into compact dropdown buttons.
        /// </summary>
        Compact
    }

    /// <summary>
    /// Decoupled persistence contract for saving and loading ribbon group visibility and preferences.
    /// Prevents hardcoding Registry or local storage into business forms.
    /// </summary>
    public interface IRibbonStateStore
    {
        bool LoadGroupVisibility(string groupKey, bool defaultVisible);
        void SaveGroupVisibility(string groupKey, bool isVisible);
    }

    /// <summary>
    /// Functional delegate-based implementation of IRibbonStateStore for easy inline configuration.
    /// </summary>
    public class DelegateRibbonStateStore : IRibbonStateStore
    {
        private readonly Func<string, bool, bool> _loader;
        private readonly Action<string, bool> _saver;

        public DelegateRibbonStateStore(Func<string, bool, bool> loader, Action<string, bool> saver)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _saver = saver ?? throw new ArgumentNullException(nameof(saver));
        }

        public bool LoadGroupVisibility(string groupKey, bool defaultVisible) => _loader(groupKey, defaultVisible);
        public void SaveGroupVisibility(string groupKey, bool isVisible) => _saver(groupKey, isVisible);
    }

    /// <summary>
    /// Represents an actionable item (button, dropdown) on a ribbon group.
    /// </summary>
    public class RibbonItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Text { get; set; } = string.Empty;
        public string? BadgeText { get; set; }
        public Image? Icon { get; set; }
        public RibbonItemStyle Style { get; set; } = RibbonItemStyle.Large;
        public bool Enabled { get; set; } = true;
        public bool Visible { get; set; } = true;
        public string? ToolTipText { get; set; }
        public bool IsCheckable { get; set; } = false;
        public bool Checked { get; set; } = false;
        public Color? BadgeColor { get; set; }
        public object? Tag { get; set; }
        public ContextMenuStrip? DropDownMenu { get; set; }

        public event EventHandler? Click;
        public event EventHandler? CheckedChanged;

        public RibbonItem() { }

        public RibbonItem(string text, Image? icon = null, RibbonItemStyle style = RibbonItemStyle.Large, EventHandler? onClick = null)
        {
            Text = text;
            Icon = icon;
            Style = style;
            if (onClick != null) Click += onClick;
        }

        public void PerformClick()
        {
            if (Enabled && Visible)
            {
                if (IsCheckable)
                {
                    Checked = !Checked;
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                }
                Click?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Represents an approval task item inside an approval dropdown group.
    /// Example: "Pending Orders: 3" (red dot) or "Handover: 0" (green/blue dot).
    /// </summary>
    public class RibbonApprovalItem
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; } = 0;
        public object? Tag { get; set; }
        public Action? ClickAction { get; set; }

        /// <summary>
        /// Explicit dot indicator color for this item. If null, resolved via StatusColorResolver.
        /// </summary>
        public Color? StatusColor { get; set; }

        /// <summary>
        /// Custom resolver function returning the status color for this specific item.
        /// </summary>
        public Func<RibbonApprovalItem, Color>? StatusColorResolver { get; set; }

        /// <summary>
        /// Formatting template for display text. Default: "{0}: {1}" (Name: Count).
        /// </summary>
        public string DisplayFormat { get; set; } = "{0}: {1}";

        public RibbonApprovalItem() { }

        public RibbonApprovalItem(string key, string name, int count = 0, Action? onClick = null)
        {
            Key = key;
            Name = name;
            Count = count;
            ClickAction = onClick;
        }

        public string GetDisplayText() => string.Format(DisplayFormat, Name, Count);
    }

    /// <summary>
    /// Specialized group container for approval queues (e.g., General, Production, Inventory).
    /// Displays a dropdown button with current total count and opens a status popup with color-coded dots.
    /// </summary>
    public class RibbonApprovalGroup
    {
        public string GroupKey { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool Visible { get; set; } = true;
        public RibbonGroupAlignment Alignment { get; set; } = RibbonGroupAlignment.Right;
        public List<RibbonApprovalItem> Items { get; } = new List<RibbonApprovalItem>();

        /// <summary>
        /// Optional delegate to resolve status dot color for items in this group.
        /// </summary>
        public Func<RibbonApprovalItem, Color>? StatusColorResolver { get; set; }

        /// <summary>
        /// Optional delegate to resolve group badge color based on total count.
        /// </summary>
        public Func<int, Color>? BadgeColorResolver { get; set; }

        public event EventHandler? ItemsChanged;

        public RibbonApprovalGroup(string title, string groupKey)
        {
            Title = title;
            GroupKey = groupKey;
        }

        public int TotalCount
        {
            get
            {
                int sum = 0;
                foreach (var item in Items) sum += Math.Max(0, item.Count);
                return sum;
            }
        }

        public void AddItem(string key, string name, int count = 0, Action? onClick = null)
        {
            Items.Add(new RibbonApprovalItem(key, name, count, onClick));
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void UpdateCount(string key, int count)
        {
            var found = Items.Find(i => i.Key == key);
            if (found != null)
            {
                found.Count = count;
                ItemsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        public RibbonApprovalItem? GetItem(string key) => Items.Find(i => i.Key == key);
    }

    /// <summary>
    /// Represents a functional group of ribbon items under a specific page.
    /// Example: "Information", "General Management", "Team Management", "System".
    /// </summary>
    public class RibbonGroup
    {
        public string Text { get; set; } = string.Empty;
        public bool Visible { get; set; } = true;
        public RibbonGroupAlignment Alignment { get; set; } = RibbonGroupAlignment.Left;
        public List<RibbonItem> Items { get; } = new List<RibbonItem>();

        // Optional link to specialized approval group
        public RibbonApprovalGroup? ApprovalGroup { get; set; }

        public RibbonGroup() { }

        public RibbonGroup(string text)
        {
            Text = text;
        }

        public RibbonItem? FindItem(string idOrText)
        {
            return Items.Find(i => i.Id == idOrText || string.Equals(i.Text, idOrText, StringComparison.OrdinalIgnoreCase));
        }

        public RibbonItem AddLargeButton(string text, Image? icon = null, EventHandler? onClick = null, string? badge = null)
        {
            var item = new RibbonItem(text, icon, RibbonItemStyle.Large, onClick)
            {
                BadgeText = badge
            };
            Items.Add(item);
            return item;
        }

        public RibbonItem AddSmallButton(string text, Image? icon = null, EventHandler? onClick = null)
        {
            var item = new RibbonItem(text, icon, RibbonItemStyle.Small, onClick);
            Items.Add(item);
            return item;
        }

        public RibbonItem AddDropDownButton(string text, Image? icon = null, ContextMenuStrip? menu = null)
        {
            var item = new RibbonItem(text, icon, RibbonItemStyle.DropDown)
            {
                DropDownMenu = menu
            };
            Items.Add(item);
            return item;
        }
    }

    /// <summary>
    /// Represents a primary tab page inside the ribbon control.
    /// </summary>
    public class RibbonPage
    {
        public string Text { get; set; } = string.Empty;
        public bool Visible { get; set; } = true;
        public List<RibbonGroup> Groups { get; } = new List<RibbonGroup>();
        public List<RibbonApprovalGroup> ApprovalGroups { get; } = new List<RibbonApprovalGroup>();

        public RibbonPage() { }

        public RibbonPage(string text)
        {
            Text = text;
        }

        public RibbonItem? FindItem(string idOrText)
        {
            foreach (var grp in Groups)
            {
                var item = grp.FindItem(idOrText);
                if (item != null) return item;
            }
            return null;
        }

        public RibbonApprovalGroup? GetApprovalGroup(string groupKey)
        {
            return ApprovalGroups.Find(ag => string.Equals(ag.GroupKey, groupKey, StringComparison.OrdinalIgnoreCase));
        }

        public RibbonGroup AddGroup(string title)
        {
            var grp = new RibbonGroup(title);
            Groups.Add(grp);
            return grp;
        }

        public RibbonApprovalGroup AddApprovalGroup(string title, string groupKey)
        {
            var appGroup = new RibbonApprovalGroup(title, groupKey);
            ApprovalGroups.Add(appGroup);

            // Also create a corresponding RibbonGroup for unified layout
            var grp = new RibbonGroup(title)
            {
                ApprovalGroup = appGroup
            };
            Groups.Add(grp);
            return appGroup;
        }
    }
}
