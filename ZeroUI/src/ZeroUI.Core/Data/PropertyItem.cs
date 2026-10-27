using System;
using System.ComponentModel;
using System.Reflection;

namespace ZeroUI.Core.Data
{
    /// <summary>
    /// Supported in-place editor types for property grid items.
    /// </summary>
    public enum PropertyEditorType
    {
        Text = 0,
        Numeric = 1,
        Boolean = 2,
        Date = 3,
        Dropdown = 4,
        Color = 5
    }

    /// <summary>
    /// Represents an inspectable property in PropertyGridControl.
    /// </summary>
    public class PropertyItem
    {
        private object? _value;

        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = "General";
        public string Description { get; set; } = string.Empty;
        public Type PropertyType { get; set; } = typeof(string);
        public bool IsReadOnly { get; set; } = false;
        public object? DefaultValue { get; set; }
        public PropertyEditorType EditorType { get; set; } = PropertyEditorType.Text;
        public string[]? Choices { get; set; }

        public object? Value
        {
            get => _value;
            set
            {
                if (!Equals(_value, value))
                {
                    var oldVal = _value;
                    _value = value;
                    ValueChanged?.Invoke(this, new PropertyValueChangedEventArgs(oldVal, value));
                }
            }
        }

        public event EventHandler<PropertyValueChangedEventArgs>? ValueChanged;

        public PropertyItem() { }

        public PropertyItem(string name, object? value, string category = "General", string description = "", bool isReadOnly = false)
        {
            Name = name;
            DisplayName = name;
            _value = value;
            Category = category;
            Description = description;
            IsReadOnly = isReadOnly;
            PropertyType = value?.GetType() ?? typeof(string);
            EditorType = InferEditorType(PropertyType);
        }

        public static PropertyEditorType InferEditorType(Type type)
        {
            if (type == typeof(bool)) return PropertyEditorType.Boolean;
            if (type == typeof(int) || type == typeof(double) || type == typeof(float) ||
                type == typeof(decimal) || type == typeof(long) || type == typeof(short))
                return PropertyEditorType.Numeric;
            if (type == typeof(DateTime)) return PropertyEditorType.Date;
            if (type.IsEnum) return PropertyEditorType.Dropdown;
            return PropertyEditorType.Text;
        }

        public static PropertyItem FromPropertyInfo(PropertyInfo prop, object target)
        {
            var catAttr = prop.GetCustomAttribute<CategoryAttribute>();
            var descAttr = prop.GetCustomAttribute<DescriptionAttribute>();
            var dispAttr = prop.GetCustomAttribute<DisplayNameAttribute>();
            var readAttr = prop.GetCustomAttribute<ReadOnlyAttribute>();
            var defAttr = prop.GetCustomAttribute<DefaultValueAttribute>();

            var item = new PropertyItem
            {
                Name = prop.Name,
                DisplayName = dispAttr?.DisplayName ?? prop.Name,
                Category = (catAttr != null && !string.IsNullOrWhiteSpace(catAttr.Category)) ? catAttr.Category : "General",
                Description = descAttr?.Description ?? string.Empty,
                IsReadOnly = !prop.CanWrite || (readAttr?.IsReadOnly ?? false),
                PropertyType = prop.PropertyType,
                DefaultValue = defAttr?.Value,
                EditorType = InferEditorType(prop.PropertyType),
                Value = prop.GetValue(target)
            };

            if (prop.PropertyType.IsEnum)
            {
                item.Choices = Enum.GetNames(prop.PropertyType);
            }

            return item;
        }
    }

    /// <summary>
    /// Obsolete alias for <see cref="PropertyItem"/> to maintain backward compatibility.
    /// </summary>
    [Obsolete("Use PropertyItem instead.")]
    public class ZeroPropertyItem : PropertyItem
    {
        public ZeroPropertyItem() { }
        public ZeroPropertyItem(string name, object? value, string category = "General", string description = "", bool isReadOnly = false)
            : base(name, value, category, description, isReadOnly) { }
    }

    public class PropertyValueChangedEventArgs : EventArgs
    {
        public object? OldValue { get; }
        public object? NewValue { get; }

        public PropertyValueChangedEventArgs(object? oldValue, object? newValue)
        {
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
