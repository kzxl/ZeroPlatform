using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ZeroUI.Core.Common;
using ZeroUI.Core.Data;

namespace ZeroUI.Core.Layout
{
    public class ColumnLayoutState
    {
        public string FieldName { get; set; } = string.Empty;
        public int Width { get; set; } = 100;
        public bool IsVisible { get; set; } = true;
        public bool IsPinned { get; set; } = false;
        public int GroupIndex { get; set; } = -1;
        public SortDirection SortOrder { get; set; } = SortDirection.None;
    }

    public class DockContainerLayoutState
    {
        public int LeftWidth { get; set; } = 260;
        public int RightWidth { get; set; } = 280;
        public int TopHeight { get; set; } = 140;
        public int BottomHeight { get; set; } = 180;
        public string ActiveDocumentTitle { get; set; } = string.Empty;
    }

    public class DockPanelLayoutState
    {
        public string Name { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string DockPosition { get; set; } = "Document";
        public bool IsPinned { get; set; } = true;
        public bool AutoHide { get; set; } = false;
        public bool Closable { get; set; } = true;
        public bool Floatable { get; set; } = true;
        public int Width { get; set; } = 240;
        public int Height { get; set; } = 200;
        public int FloatX { get; set; } = 100;
        public int FloatY { get; set; } = 100;
        public int FloatWidth { get; set; } = 320;
        public int FloatHeight { get; set; } = 420;
        public int OrderIndex { get; set; } = 0;
    }

    public class WorkspaceLayoutState
    {
        public string Version { get; set; } = "1.1";
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;
        public DockContainerLayoutState Containers { get; set; } = new DockContainerLayoutState();
        public List<DockPanelLayoutState> DockPanels { get; } = new List<DockPanelLayoutState>();
        public List<ColumnLayoutState> GridColumns { get; } = new List<ColumnLayoutState>();
    }

    /// <summary>
    /// Pure, zero-dependency Workspace Layout Serializer and Deserializer for ZeroUI.
    /// Serializes and restores DockManager layout hierarchies (panels, positions, floating coordinates,
    /// auto-hide sidebars, container split ratios) and DataGrid column configurations to clean JSON.
    /// Compatible with .NET Standard 2.0, .NET Framework 4.6.2, and .NET 8.0+.
    /// </summary>
    public static class ZeroWorkspaceSerializer
    {
        public static string Serialize(WorkspaceLayoutState state)
        {
            if (state == null) return "{}";

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"Version\": \"{Escape(state.Version)}\",");
            sb.AppendLine($"  \"SavedAt\": \"{state.SavedAt:O}\",");

            // Containers
            var cont = state.Containers ?? new DockContainerLayoutState();
            sb.AppendLine("  \"Containers\": {");
            sb.AppendLine($"    \"LeftWidth\": {cont.LeftWidth},");
            sb.AppendLine($"    \"RightWidth\": {cont.RightWidth},");
            sb.AppendLine($"    \"TopHeight\": {cont.TopHeight},");
            sb.AppendLine($"    \"BottomHeight\": {cont.BottomHeight},");
            sb.AppendLine($"    \"ActiveDocumentTitle\": \"{Escape(cont.ActiveDocumentTitle)}\"");
            sb.AppendLine("  },");

            // Dock Panels
            sb.AppendLine("  \"DockPanels\": [");
            for (int i = 0; i < state.DockPanels.Count; i++)
            {
                var p = state.DockPanels[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"Name\": \"{Escape(p.Name)}\",");
                sb.AppendLine($"      \"Title\": \"{Escape(p.Title)}\",");
                sb.AppendLine($"      \"DockPosition\": \"{Escape(p.DockPosition)}\",");
                sb.AppendLine($"      \"IsPinned\": {(p.IsPinned ? "true" : "false")},");
                sb.AppendLine($"      \"AutoHide\": {(p.AutoHide ? "true" : "false")},");
                sb.AppendLine($"      \"Closable\": {(p.Closable ? "true" : "false")},");
                sb.AppendLine($"      \"Floatable\": {(p.Floatable ? "true" : "false")},");
                sb.AppendLine($"      \"Width\": {p.Width},");
                sb.AppendLine($"      \"Height\": {p.Height},");
                sb.AppendLine($"      \"FloatX\": {p.FloatX},");
                sb.AppendLine($"      \"FloatY\": {p.FloatY},");
                sb.AppendLine($"      \"FloatWidth\": {p.FloatWidth},");
                sb.AppendLine($"      \"FloatHeight\": {p.FloatHeight},");
                sb.AppendLine($"      \"OrderIndex\": {p.OrderIndex}");
                sb.Append("    }");
                if (i < state.DockPanels.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ],");

            // Grid Columns
            sb.AppendLine("  \"GridColumns\": [");
            for (int i = 0; i < state.GridColumns.Count; i++)
            {
                var c = state.GridColumns[i];
                sb.AppendLine("    {");
                sb.AppendLine($"      \"FieldName\": \"{Escape(c.FieldName)}\",");
                sb.AppendLine($"      \"Width\": {c.Width},");
                sb.AppendLine($"      \"IsVisible\": {(c.IsVisible ? "true" : "false")},");
                sb.AppendLine($"      \"IsPinned\": {(c.IsPinned ? "true" : "false")},");
                sb.AppendLine($"      \"GroupIndex\": {c.GroupIndex},");
                sb.AppendLine($"      \"SortOrder\": {(int)c.SortOrder}");
                sb.Append("    }");
                if (i < state.GridColumns.Count - 1) sb.Append(",");
                sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");

            return sb.ToString();
        }

        public static WorkspaceLayoutState Deserialize(string json)
        {
            var state = new WorkspaceLayoutState();
            if (string.IsNullOrWhiteSpace(json)) return state;

            try
            {
                var root = MiniJsonParser.Parse(json) as Dictionary<string, object>;
                if (root == null) return state;

                if (root.TryGetValue("Version", out var verObj) && verObj is string verStr)
                {
                    state.Version = verStr;
                }

                if (root.TryGetValue("SavedAt", out var savedObj) && savedObj is string savedStr)
                {
                    if (DateTime.TryParse(savedStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                    {
                        state.SavedAt = dt;
                    }
                }

                if (root.TryGetValue("Containers", out var contObj) && contObj is Dictionary<string, object> contDict)
                {
                    state.Containers.LeftWidth = GetInt(contDict, "LeftWidth", 260);
                    state.Containers.RightWidth = GetInt(contDict, "RightWidth", 280);
                    state.Containers.TopHeight = GetInt(contDict, "TopHeight", 140);
                    state.Containers.BottomHeight = GetInt(contDict, "BottomHeight", 180);
                    state.Containers.ActiveDocumentTitle = GetString(contDict, "ActiveDocumentTitle", "");
                }

                if (root.TryGetValue("DockPanels", out var panelsObj) && panelsObj is List<object> panelsList)
                {
                    foreach (var item in panelsList)
                    {
                        if (item is Dictionary<string, object> pDict)
                        {
                            var p = new DockPanelLayoutState
                            {
                                Name = GetString(pDict, "Name", ""),
                                Title = GetString(pDict, "Title", "Panel"),
                                DockPosition = GetString(pDict, "DockPosition", "Document"),
                                IsPinned = GetBool(pDict, "IsPinned", true),
                                AutoHide = GetBool(pDict, "AutoHide", false),
                                Closable = GetBool(pDict, "Closable", true),
                                Floatable = GetBool(pDict, "Floatable", true),
                                Width = GetInt(pDict, "Width", 240),
                                Height = GetInt(pDict, "Height", 200),
                                FloatX = GetInt(pDict, "FloatX", 100),
                                FloatY = GetInt(pDict, "FloatY", 100),
                                FloatWidth = GetInt(pDict, "FloatWidth", 320),
                                FloatHeight = GetInt(pDict, "FloatHeight", 420),
                                OrderIndex = GetInt(pDict, "OrderIndex", 0)
                            };
                            state.DockPanels.Add(p);
                        }
                    }
                }

                if (root.TryGetValue("GridColumns", out var colsObj) && colsObj is List<object> colsList)
                {
                    foreach (var item in colsList)
                    {
                        if (item is Dictionary<string, object> cDict)
                        {
                            var c = new ColumnLayoutState
                            {
                                FieldName = GetString(cDict, "FieldName", ""),
                                Width = GetInt(cDict, "Width", 100),
                                IsVisible = GetBool(cDict, "IsVisible", true),
                                IsPinned = GetBool(cDict, "IsPinned", false),
                                GroupIndex = GetInt(cDict, "GroupIndex", -1),
                                SortOrder = (SortDirection)GetInt(cDict, "SortOrder", 0)
                            };
                            state.GridColumns.Add(c);
                        }
                    }
                }
            }
            catch
            {
                // Fault-tolerant fallback to empty state
            }

            return state;
        }

        public static string ToJson(WorkspaceLayoutState state) => Serialize(state);

        public static WorkspaceLayoutState FromJson(string json) => Deserialize(json);

        public static WorkspaceLayoutState CaptureGrid(IEnumerable<ZeroColumn> columns)
        {
            var ws = new WorkspaceLayoutState();
            if (columns != null)
            {
                foreach (var col in columns)
                {
                    ws.GridColumns.Add(new ColumnLayoutState
                    {
                        FieldName = !string.IsNullOrEmpty(col.FieldName) ? col.FieldName : col.HeaderText,
                        Width = col.Width,
                        IsVisible = col.IsVisible,
                        IsPinned = col.IsPinned,
                        GroupIndex = col.GroupIndex,
                        SortOrder = col.SortOrder
                    });
                }
            }
            return ws;
        }

        public static void ApplyToGrid(IEnumerable<ZeroColumn> columns, WorkspaceLayoutState state)
        {
            if (columns == null || state == null || state.GridColumns.Count == 0) return;

            var map = new Dictionary<string, ColumnLayoutState>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in state.GridColumns)
            {
                if (!string.IsNullOrEmpty(c.FieldName)) map[c.FieldName] = c;
            }

            foreach (var col in columns)
            {
                string key = !string.IsNullOrEmpty(col.FieldName) ? col.FieldName : col.HeaderText;
                if (map.TryGetValue(key, out var s))
                {
                    col.Width = s.Width;
                    col.IsVisible = s.IsVisible;
                    col.IsPinned = s.IsPinned;
                    col.GroupIndex = s.GroupIndex;
                    col.SortOrder = s.SortOrder;
                }
            }
        }

        private static string GetString(Dictionary<string, object> dict, string key, string defVal)
        {
            return dict.TryGetValue(key, out var v) && v is string s ? s : defVal;
        }

        private static int GetInt(Dictionary<string, object> dict, string key, int defVal)
        {
            if (dict.TryGetValue(key, out var v))
            {
                if (v is double d) return (int)d;
                if (v is int i) return i;
                if (v is long l) return (int)l;
                if (v is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    return parsed;
                }
            }
            return defVal;
        }

        private static bool GetBool(Dictionary<string, object> dict, string key, bool defVal)
        {
            if (dict.TryGetValue(key, out var v))
            {
                if (v is bool b) return b;
                if (v is string s && bool.TryParse(s, out var parsed)) return parsed;
            }
            return defVal;
        }

        private static string Escape(string? s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") ?? "";

        #region Embedded Lightweight Zero-Dependency JSON Parser

        private static class MiniJsonParser
        {
            public static object? Parse(string json)
            {
                int index = 0;
                return ParseValue(json, ref index);
            }

            private static object? ParseValue(string json, ref int index)
            {
                SkipWhitespace(json, ref index);
                if (index >= json.Length) return null;

                char c = json[index];
                if (c == '{') return ParseObject(json, ref index);
                if (c == '[') return ParseArray(json, ref index);
                if (c == '"') return ParseString(json, ref index);
                if (c == 't' || c == 'f') return ParseBool(json, ref index);
                if (c == 'n') return ParseNull(json, ref index);
                if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(json, ref index);

                return null;
            }

            private static Dictionary<string, object> ParseObject(string json, ref int index)
            {
                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                index++; // Skip '{'

                while (index < json.Length)
                {
                    SkipWhitespace(json, ref index);
                    if (index >= json.Length) break;
                    if (json[index] == '}')
                    {
                        index++;
                        break;
                    }

                    if (json[index] == ',')
                    {
                        index++;
                        continue;
                    }

                    string key = ParseString(json, ref index);
                    SkipWhitespace(json, ref index);

                    if (index < json.Length && json[index] == ':')
                    {
                        index++;
                    }

                    object? value = ParseValue(json, ref index);
                    if (value != null)
                    {
                        dict[key] = value;
                    }

                    SkipWhitespace(json, ref index);
                    if (index < json.Length && json[index] == ',')
                    {
                        index++;
                    }
                }

                return dict;
            }

            private static List<object> ParseArray(string json, ref int index)
            {
                var list = new List<object>();
                index++; // Skip '['

                while (index < json.Length)
                {
                    SkipWhitespace(json, ref index);
                    if (index >= json.Length) break;
                    if (json[index] == ']')
                    {
                        index++;
                        break;
                    }

                    if (json[index] == ',')
                    {
                        index++;
                        continue;
                    }

                    object? value = ParseValue(json, ref index);
                    if (value != null)
                    {
                        list.Add(value);
                    }

                    SkipWhitespace(json, ref index);
                    if (index < json.Length && json[index] == ',')
                    {
                        index++;
                    }
                }

                return list;
            }

            private static string ParseString(string json, ref int index)
            {
                if (index >= json.Length || json[index] != '"') return string.Empty;
                index++; // Skip opening '"'

                var sb = new StringBuilder();
                while (index < json.Length)
                {
                    char c = json[index++];
                    if (c == '"') break;

                    if (c == '\\' && index < json.Length)
                    {
                        char escaped = json[index++];
                        switch (escaped)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            default: sb.Append(escaped); break;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                return sb.ToString();
            }

            private static double ParseNumber(string json, ref int index)
            {
                int start = index;
                while (index < json.Length && (json[index] == '-' || json[index] == '+' || json[index] == '.' || json[index] == 'e' || json[index] == 'E' || (json[index] >= '0' && json[index] <= '9')))
                {
                    index++;
                }

                string numStr = json.Substring(start, index - start);
                if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
                {
                    return val;
                }
                return 0.0;
            }

            private static bool ParseBool(string json, ref int index)
            {
                if (json.Substring(index).StartsWith("true", StringComparison.OrdinalIgnoreCase))
                {
                    index += 4;
                    return true;
                }
                if (json.Substring(index).StartsWith("false", StringComparison.OrdinalIgnoreCase))
                {
                    index += 5;
                    return false;
                }
                index++;
                return false;
            }

            private static object? ParseNull(string json, ref int index)
            {
                if (json.Substring(index).StartsWith("null", StringComparison.OrdinalIgnoreCase))
                {
                    index += 4;
                }
                else
                {
                    index++;
                }
                return null;
            }

            private static void SkipWhitespace(string json, ref int index)
            {
                while (index < json.Length && (json[index] == ' ' || json[index] == '\t' || json[index] == '\r' || json[index] == '\n'))
                {
                    index++;
                }
            }
        }

        #endregion
    }
}
