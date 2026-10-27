using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Specifies the behavioral type of an item in a button group.
    /// </summary>
    public enum ButtonGroupItemType
    {
        /// <summary>Standard push action button.</summary>
        Push,
        /// <summary>Toggleable button maintaining checked/unchecked state.</summary>
        Toggle,
        /// <summary>Button with dropdown arrow triggering contextual options.</summary>
        DropDown,
        /// <summary>Vertical or horizontal line separator dividing functional blocks.</summary>
        Separator
    }

    /// <summary>
    /// Specifies how item widths are allocated inside a button group.
    /// </summary>
    public enum ButtonGroupSizeMode
    {
        /// <summary>Each button is sized according to its text, icon, and padding without artificial stretching.</summary>
        AutoFit,
        /// <summary>All buttons receive an equal fraction of the available width.</summary>
        EqualWidth,
        /// <summary>Buttons are stretched proportionally to fill the full available width of the container.</summary>
        Fill
    }

    /// <summary>
    /// Specifies the visual style variant of a button in a group.
    /// </summary>
    public enum ButtonGroupItemStyle
    {
        Primary,
        Secondary,
        Success,
        Danger,
        Ghost
    }

    /// <summary>
    /// Specifies selection behavior for toggle items inside a button group.
    /// </summary>
    public enum ButtonGroupSelectionMode
    {
        /// <summary>Buttons act independently without enforcing mutual exclusion.</summary>
        None,
        /// <summary>Radio-style mutual exclusion: at most one toggle item can be checked.</summary>
        SingleSelect,
        /// <summary>Checkbox-style: multiple toggle items can be checked concurrently.</summary>
        MultiSelect
    }

    /// <summary>
    /// Represents an individual actionable item within a <see cref="ButtonGroupModel"/>.
    /// </summary>
    public class ButtonGroupItem
    {
        public string Id { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string? IconGlyph { get; set; }
        public string? ToolTip { get; set; }
        public ButtonGroupItemType Type { get; set; } = ButtonGroupItemType.Push;
        public ButtonGroupItemStyle Style { get; set; } = ButtonGroupItemStyle.Secondary;
        public bool IsChecked { get; set; }
        public bool IsEnabled { get; set; } = true;
        public bool IsVisible { get; set; } = true;
        public string? BadgeText { get; set; }
        /// <summary>Gets or sets whether to display a small indicator dot (e.g. for notifications or uncommitted changes).</summary>
        public bool ShowBadgeDot { get; set; }
        /// <summary>Gets or sets the custom hex color for badge pill or badge dot (e.g. "#EF4444").</summary>
        public string? BadgeColorHex { get; set; }
        /// <summary>Gets or sets the minimum width allocated for this button item.</summary>
        public int MinWidth { get; set; }
        /// <summary>Gets or sets custom horizontal padding for this specific item (overriding group-level padding when set).</summary>
        public int? CustomPaddingHorizontal { get; set; }
        /// <summary>Gets or sets a custom background hex color for this item (e.g. "#1E293B").</summary>
        public string? CustomBackColorHex { get; set; }
        /// <summary>Gets or sets a custom text/foreground hex color for this item (e.g. "#FFFFFF").</summary>
        public string? CustomForeColorHex { get; set; }
        /// <summary>Gets or sets an associated context menu or drop-down popup object (ContextMenuStrip in WinForms, ContextMenu in WPF).</summary>
        public object? DropDownMenu { get; set; }
        /// <summary>Gets or sets an optional raw image object (Bitmap/Image in WinForms, ImageSource in WPF).</summary>
        public object? IconImage { get; set; }
        public object? Tag { get; set; }
        public Action<ButtonGroupItem>? Action { get; set; }

        public ButtonGroupItem() { }

        public ButtonGroupItem(string id, string text, string? iconGlyph = null, ButtonGroupItemType type = ButtonGroupItemType.Push, Action<ButtonGroupItem>? action = null)
        {
            Id = id;
            Text = text;
            IconGlyph = iconGlyph;
            Type = type;
            Action = action;
        }

        public ButtonGroupItem(string id, string text, string? iconGlyph, ButtonGroupItemType type, Action<ButtonGroupItem>? action, object? dropDownMenu)
            : this(id, text, iconGlyph, type, action)
        {
            DropDownMenu = dropDownMenu;
        }
    }

    /// <summary>
    /// Event arguments raised when a button item inside a group is clicked.
    /// </summary>
    public class ButtonGroupItemEventArgs : EventArgs
    {
        public ButtonGroupItem Item { get; }
        public int Index { get; }
        public bool Handled { get; set; }

        public ButtonGroupItemEventArgs(ButtonGroupItem item, int index)
        {
            Item = item;
            Index = index;
        }
    }

    /// <summary>
    /// Headless state coordinator and collection manager for button clusters and action strips.
    /// </summary>
    public class ButtonGroupModel
    {
        private readonly List<ButtonGroupItem> _items = new List<ButtonGroupItem>();
        private ButtonGroupSelectionMode _selectionMode = ButtonGroupSelectionMode.None;

        public event EventHandler<ButtonGroupItemEventArgs>? ItemClicked;
        public event EventHandler<ButtonGroupItem>? SelectionChanged;
        public event EventHandler? ItemsChanged;

        public IReadOnlyList<ButtonGroupItem> Items => _items;

        public ButtonGroupSelectionMode SelectionMode
        {
            get => _selectionMode;
            set => _selectionMode = value;
        }

        public int Count => _items.Count;

        public ButtonGroupItem this[int index] => _items[index];

        public ButtonGroupItem? SelectedItem => _items.FirstOrDefault(i => i.Type == ButtonGroupItemType.Toggle && i.IsChecked);

        public void Add(ButtonGroupItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            _items.Add(item);
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddRange(IEnumerable<ButtonGroupItem> items)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            _items.AddRange(items);
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool Remove(ButtonGroupItem item)
        {
            if (item == null) return false;
            bool removed = _items.Remove(item);
            if (removed)
            {
                ItemsChanged?.Invoke(this, EventArgs.Empty);
            }
            return removed;
        }

        public void Clear()
        {
            _items.Clear();
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        public ButtonGroupItem? FindById(string id)
        {
            return _items.FirstOrDefault(i => string.Equals(i.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Sets the checked state of a toggle item by ID.
        /// </summary>
        public bool SetChecked(string id, bool isChecked)
        {
            var item = FindById(id);
            if (item == null || item.Type != ButtonGroupItemType.Toggle) return false;

            if (_selectionMode == ButtonGroupSelectionMode.SingleSelect && isChecked)
            {
                foreach (var itm in _items)
                {
                    if (itm.Type == ButtonGroupItemType.Toggle)
                    {
                        itm.IsChecked = ReferenceEquals(itm, item);
                    }
                }
            }
            else
            {
                item.IsChecked = isChecked;
            }

            SelectionChanged?.Invoke(this, item);
            ItemsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Updates the badge text, dot visibility, and badge color of an item by ID.
        /// </summary>
        public bool SetBadge(string id, string? badgeText, bool showBadgeDot = false, string? badgeColorHex = null)
        {
            var item = FindById(id);
            if (item == null) return false;

            item.BadgeText = badgeText;
            item.ShowBadgeDot = showBadgeDot;
            if (!string.IsNullOrEmpty(badgeColorHex))
            {
                item.BadgeColorHex = badgeColorHex;
            }

            ItemsChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        /// <summary>
        /// Selects an item by index, enforcing single-select mutual exclusion if configured.
        /// </summary>
        public void SelectIndex(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            var target = _items[index];
            if (target.Type != ButtonGroupItemType.Toggle || !target.IsEnabled) return;

            if (_selectionMode == ButtonGroupSelectionMode.SingleSelect)
            {
                foreach (var itm in _items)
                {
                    if (itm.Type == ButtonGroupItemType.Toggle)
                    {
                        itm.IsChecked = ReferenceEquals(itm, target);
                    }
                }
            }
            else
            {
                target.IsChecked = !target.IsChecked;
            }

            SelectionChanged?.Invoke(this, target);
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handles click on an item, performing action dispatch and state toggling.
        /// </summary>
        public void TriggerClick(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            var item = _items[index];
            if (!item.IsEnabled || item.Type == ButtonGroupItemType.Separator) return;

            if (item.Type == ButtonGroupItemType.Toggle)
            {
                SelectIndex(index);
            }

            item.Action?.Invoke(item);
            ItemClicked?.Invoke(this, new ButtonGroupItemEventArgs(item, index));
        }
    }
}
