using System;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Represents a single crumb in a hierarchical navigation Breadcrumb bar.
    /// </summary>
    public class BreadcrumbItem
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public object? Tag { get; set; }

        public BreadcrumbItem() { }

        public BreadcrumbItem(string key, string displayText, object? tag = null)
        {
            Key = key;
            DisplayText = displayText;
            Tag = tag;
        }

        public override string ToString() => DisplayText;
    }

    /// <summary>
    /// Obsolete alias for <see cref="BreadcrumbItem"/> to maintain backward compatibility.
    /// </summary>
    [Obsolete("Use BreadcrumbItem instead.")]
    public class ZeroBreadcrumbItem : BreadcrumbItem
    {
        public ZeroBreadcrumbItem() { }
        public ZeroBreadcrumbItem(string key, string displayText, object? tag = null) : base(key, displayText, tag) { }
    }
}
