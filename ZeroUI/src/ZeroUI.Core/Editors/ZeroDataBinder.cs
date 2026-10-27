using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Optional attribute to explicitly map an editor or DTO property to a persistent data field name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class DataFieldAttribute : Attribute
    {
        public string FieldName { get; }

        public DataFieldAttribute(string fieldName)
        {
            FieldName = fieldName ?? throw new ArgumentNullException(nameof(fieldName));
        }
    }

    /// <summary>
    /// High-performance generic Form Data-Binding Coordinator for ZeroUI.
    /// Enables automated 1-line bidirectional data population and extraction
    /// between UI editor containers and strongly-typed business DTO models.
    /// </summary>
    public static class ZeroDataBinder
    {
        private static readonly string[] CommonPrefixes = new[] { "txt", "num", "chk", "dt", "cmb", "lookup", "token", "col", "spin", "date" };

        /// <summary>
        /// Populates an enumeration of editors with values from matching properties of a DTO model.
        /// </summary>
        public static void Populate(IEnumerable<IZeroEditor> editors, object dto)
        {
            if (editors == null || dto == null) return;

            var dtoType = dto.GetType();
            var props = dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var propDict = new Dictionary<string, PropertyInfo>(props.Length, StringComparer.OrdinalIgnoreCase);

            foreach (var p in props)
            {
                if (!p.CanRead) continue;
                propDict[p.Name] = p;

                var attr = p.GetCustomAttribute<DataFieldAttribute>();
                if (attr != null)
                {
                    propDict[attr.FieldName] = p;
                }
            }

            foreach (var editor in editors)
            {
                string? fieldKey = ResolveEditorKey(editor);
                if (string.IsNullOrEmpty(fieldKey)) continue;

                if (propDict.TryGetValue(fieldKey!, out var prop))
                {
                    object? val = prop.GetValue(dto);
                    editor.EditValue = val;
                    editor.IsModified = false;
                }
            }
        }

        /// <summary>
        /// Populates mapped editors with values from matching properties of a DTO model.
        /// </summary>
        public static void Populate(IDictionary<string, IZeroEditor> editorMap, object dto)
        {
            if (editorMap == null || dto == null) return;

            var dtoType = dto.GetType();
            var props = dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var propDict = new Dictionary<string, PropertyInfo>(props.Length, StringComparer.OrdinalIgnoreCase);

            foreach (var p in props)
            {
                if (!p.CanRead) continue;
                propDict[p.Name] = p;

                var attr = p.GetCustomAttribute<DataFieldAttribute>();
                if (attr != null)
                {
                    propDict[attr.FieldName] = p;
                }
            }

            foreach (var kvp in editorMap)
            {
                string fieldKey = kvp.Key;
                var editor = kvp.Value;
                if (editor == null) continue;

                if (propDict.TryGetValue(fieldKey, out var prop))
                {
                    object? val = prop.GetValue(dto);
                    editor.EditValue = val;
                    editor.IsModified = false;
                }
            }
        }

        /// <summary>
        /// Populates an editor map with values from matching properties of a DTO model.
        /// </summary>
        public static void Populate(object dto, IDictionary<string, IZeroEditor> editorMap) => Populate(editorMap, dto);

        /// <summary>
        /// Extracts edited values from an enumeration of editors into a new instance of a strongly-typed DTO.
        /// </summary>
        public static T Collect<T>(IEnumerable<IZeroEditor> editors) where T : new()
        {
            var result = new T();
            CollectInto(editors, result);
            return result;
        }

        /// <summary>
        /// Extracts edited values from an editor map into a new instance of a strongly-typed DTO.
        /// </summary>
        public static T Collect<T>(IDictionary<string, IZeroEditor> editorMap) where T : new()
        {
            var result = new T();
            CollectInto(editorMap, result);
            return result;
        }

        /// <summary>
        /// Extracts edited values from an editor map into an existing target DTO instance.
        /// </summary>
        public static T Collect<T>(T targetDto, IDictionary<string, IZeroEditor> editorMap)
        {
            if (targetDto != null) CollectInto(editorMap, targetDto);
            return targetDto;
        }

        /// <summary>
        /// Extracts edited values from an enumeration of editors into an existing target DTO instance.
        /// </summary>
        public static T Collect<T>(T targetDto, IEnumerable<IZeroEditor> editors)
        {
            if (targetDto != null) CollectInto(editors, targetDto);
            return targetDto;
        }

        /// <summary>
        /// Extracts edited values from an enumeration of editors and writes them into an existing target DTO.
        /// </summary>
        public static void CollectInto(IEnumerable<IZeroEditor> editors, object targetDto)
        {
            if (editors == null || targetDto == null) return;

            var dtoType = targetDto.GetType();
            var props = dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var propDict = new Dictionary<string, PropertyInfo>(props.Length, StringComparer.OrdinalIgnoreCase);

            foreach (var p in props)
            {
                if (!p.CanWrite) continue;
                propDict[p.Name] = p;

                var attr = p.GetCustomAttribute<DataFieldAttribute>();
                if (attr != null)
                {
                    propDict[attr.FieldName] = p;
                }
            }

            foreach (var editor in editors)
            {
                string? fieldKey = ResolveEditorKey(editor);
                if (string.IsNullOrEmpty(fieldKey)) continue;

                if (propDict.TryGetValue(fieldKey!, out var prop))
                {
                    object? rawVal = editor.EditValue;
                    object? convertedVal = ConvertValue(rawVal, prop.PropertyType);
                    prop.SetValue(targetDto, convertedVal);
                }
            }
        }

        /// <summary>
        /// Extracts edited values from an editor map and writes them into an existing target DTO.
        /// </summary>
        public static void CollectInto(IDictionary<string, IZeroEditor> editorMap, object targetDto)
        {
            if (editorMap == null || targetDto == null) return;

            var dtoType = targetDto.GetType();
            var props = dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var propDict = new Dictionary<string, PropertyInfo>(props.Length, StringComparer.OrdinalIgnoreCase);

            foreach (var p in props)
            {
                if (!p.CanWrite) continue;
                propDict[p.Name] = p;

                var attr = p.GetCustomAttribute<DataFieldAttribute>();
                if (attr != null)
                {
                    propDict[attr.FieldName] = p;
                }
            }

            foreach (var kvp in editorMap)
            {
                string fieldKey = kvp.Key;
                var editor = kvp.Value;
                if (editor == null) continue;

                if (propDict.TryGetValue(fieldKey, out var prop))
                {
                    object? rawVal = editor.EditValue;
                    object? convertedVal = ConvertValue(rawVal, prop.PropertyType);
                    prop.SetValue(targetDto, convertedVal);
                }
            }
        }

        /// <summary>
        /// Returns true if any editor in the enumeration has been modified by the user.
        /// </summary>
        public static bool IsModified(IEnumerable<IZeroEditor> editors)
        {
            if (editors == null) return false;
            foreach (var editor in editors)
            {
                if (editor.IsModified) return true;
            }
            return false;
        }

        /// <summary>
        /// Resets all editors in the enumeration to their default states and marks IsModified as false.
        /// </summary>
        public static void ResetAll(IEnumerable<IZeroEditor> editors)
        {
            if (editors == null) return;
            foreach (var editor in editors)
            {
                editor.Reset();
                editor.IsModified = false;
            }
        }

        /// <summary>
        /// Resets all editors in the enumeration to their default states and marks IsModified as false.
        /// </summary>
        public static void Reset(IEnumerable<IZeroEditor> editors) => ResetAll(editors);

        private static string? ResolveEditorKey(IZeroEditor editor)
        {
            // 1. Check if editor has DataField attribute
            var attr = editor.GetType().GetCustomAttribute<DataFieldAttribute>();
            if (attr != null) return attr.FieldName;

            // 2. Check Name property via Reflection (WinForms Control.Name or WPF FrameworkElement.Name)
            var nameProp = editor.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
            if (nameProp != null && nameProp.PropertyType == typeof(string))
            {
                string? name = nameProp.GetValue(editor) as string;
                if (!string.IsNullOrEmpty(name))
                {
                    // Strip common hungarian notation prefixes (e.g. txtCustomerName -> CustomerName)
                    string sanitized = name!;
                    foreach (var prefix in CommonPrefixes)
                    {
                        if (sanitized.Length > prefix.Length &&
                            sanitized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                            char.IsUpper(sanitized[prefix.Length]))
                        {
                            sanitized = sanitized.Substring(prefix.Length);
                            break;
                        }
                    }
                    return sanitized;
                }
            }

            return null;
        }

        private static object? ConvertValue(object? value, Type targetType)
        {
            if (value == null)
            {
                if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                {
                    return Activator.CreateInstance(targetType);
                }
                return null;
            }

            Type underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            if (underlyingType.IsInstanceOfType(value))
            {
                return value;
            }

            if (underlyingType == typeof(string))
            {
                return value.ToString();
            }

            if (underlyingType.IsEnum)
            {
                if (value is string strEnum)
                {
                    return Enum.Parse(underlyingType, strEnum, true);
                }
                return Enum.ToObject(underlyingType, value);
            }

            if (value is IConvertible)
            {
                try
                {
                    return Convert.ChangeType(value, underlyingType, CultureInfo.InvariantCulture);
                }
                catch
                {
                    // Fall back to TypeDescriptor
                }
            }

            var converter = TypeDescriptor.GetConverter(underlyingType);
            if (converter.CanConvertFrom(value.GetType()))
            {
                return converter.ConvertFrom(null, CultureInfo.InvariantCulture, value);
            }

            return value;
        }
    }
}
