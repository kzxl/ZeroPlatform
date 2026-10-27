using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ZeroUI.Core.Process
{
    /// <summary>
    /// Lightweight, zero-dependency JSON serializer and parser for <see cref="ProcessFlowDefinition"/>.
    /// Supports .NET Standard 2.0, .NET Framework 4.6.2, and .NET 8.0 without external packages.
    /// </summary>
    public static class ProcessFlowSerializer
    {
        /// <summary>
        /// Serializes a <see cref="ProcessFlowDefinition"/> to a clean, formatted JSON string.
        /// </summary>
        public static string ToJson(ProcessFlowDefinition def, bool indented = true)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));

            var sb = new StringBuilder(4096);
            string nl = indented ? "\r\n" : "";
            string sp = indented ? "  " : "";
            string sp2 = indented ? "    " : "";
            string sp3 = indented ? "      " : "";

            sb.Append("{").Append(nl);
            sb.Append(sp).Append("\"id\": \"").Append(Escape(def.Id)).Append("\",").Append(nl);
            sb.Append(sp).Append("\"code\": \"").Append(Escape(def.Code)).Append("\",").Append(nl);
            sb.Append(sp).Append("\"title\": \"").Append(Escape(def.Title)).Append("\",").Append(nl);
            sb.Append(sp).Append("\"description\": \"").Append(Escape(def.Description)).Append("\",").Append(nl);
            sb.Append(sp).Append("\"version\": ").Append(def.Version).Append(",").Append(nl);

            // 1. Lanes
            sb.Append(sp).Append("\"lanes\": [").Append(nl);
            for (int i = 0; i < def.Lanes.Count; i++)
            {
                var lane = def.Lanes[i];
                sb.Append(sp2).Append("{").Append(nl);
                sb.Append(sp3).Append("\"id\": \"").Append(Escape(lane.Id)).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"title\": \"").Append(Escape(lane.Title)).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"x\": ").Append(lane.X.ToString(CultureInfo.InvariantCulture)).Append(",").Append(nl);
                sb.Append(sp3).Append("\"y\": ").Append(lane.Y.ToString(CultureInfo.InvariantCulture)).Append(",").Append(nl);
                sb.Append(sp3).Append("\"width\": ").Append(lane.Width.ToString(CultureInfo.InvariantCulture)).Append(",").Append(nl);
                sb.Append(sp3).Append("\"height\": ").Append(lane.Height.ToString(CultureInfo.InvariantCulture)).Append(",").Append(nl);
                sb.Append(sp3).Append("\"headerColor\": \"").Append(Escape(lane.HeaderColorHex)).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"backgroundColor\": \"").Append(Escape(lane.BackgroundColorHex)).Append("\"").Append(nl);
                sb.Append(sp2).Append(i < def.Lanes.Count - 1 ? "}," : "}").Append(nl);
            }
            sb.Append(sp).Append("],").Append(nl);

            // 2. Nodes
            sb.Append(sp).Append("\"nodes\": [").Append(nl);
            for (int i = 0; i < def.Nodes.Count; i++)
            {
                var node = def.Nodes[i];
                sb.Append(sp2).Append("{").Append(nl);
                sb.Append(sp3).Append("\"id\": \"").Append(Escape(node.Id)).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"title\": \"").Append(Escape(node.Title)).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"subtitle\": \"").Append(Escape(node.Subtitle)).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"shape\": \"").Append(node.Shape.ToString()).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"status\": \"").Append(node.Status.ToString()).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"x\": ").Append(node.X.ToString(CultureInfo.InvariantCulture)).Append(",").Append(nl);
                sb.Append(sp3).Append("\"y\": ").Append(node.Y.ToString(CultureInfo.InvariantCulture)).Append(",").Append(nl);
                sb.Append(sp3).Append("\"width\": ").Append(node.Width.ToString(CultureInfo.InvariantCulture)).Append(",").Append(nl);
                sb.Append(sp3).Append("\"height\": ").Append(node.Height.ToString(CultureInfo.InvariantCulture)).Append(",").Append(nl);
                sb.Append(sp3).Append("\"headerColor\": \"").Append(Escape(node.HeaderColorHex)).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"borderColor\": \"").Append(Escape(node.BorderColorHex)).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"icon\": \"").Append(Escape(node.IconGlyph)).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"actionKey\": \"").Append(Escape(node.ActionKey)).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"laneId\": \"").Append(Escape(node.LaneId ?? "")).Append("\"").Append(nl);
                sb.Append(sp2).Append(i < def.Nodes.Count - 1 ? "}," : "}").Append(nl);
            }
            sb.Append(sp).Append("],").Append(nl);

            // 3. Connections
            sb.Append(sp).Append("\"connections\": [").Append(nl);
            for (int i = 0; i < def.Connections.Count; i++)
            {
                var conn = def.Connections[i];
                sb.Append(sp2).Append("{").Append(nl);
                sb.Append(sp3).Append("\"id\": \"").Append(Escape(conn.Id)).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"source\": \"").Append(Escape(conn.SourceNodeId)).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"target\": \"").Append(Escape(conn.TargetNodeId)).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"sourcePort\": \"").Append(conn.SourcePort.ToString()).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"targetPort\": \"").Append(conn.TargetPort.ToString()).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"label\": \"").Append(Escape(conn.Label)).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"color\": \"").Append(Escape(conn.StrokeColorHex)).Append("\",").Append(nl);
                sb.Append(sp3).Append("\"thickness\": ").Append(conn.StrokeThickness.ToString(CultureInfo.InvariantCulture)).Append(",").Append(nl);
                sb.Append(sp3).Append("\"dashed\": ").Append(conn.IsDashed ? "true" : "false").Append(",").Append(nl);
                sb.Append(sp3).Append("\"routing\": \"").Append(conn.RoutingMode.ToString()).Append("\"").Append(nl);
                sb.Append(sp2).Append(i < def.Connections.Count - 1 ? "}," : "}").Append(nl);
            }
            sb.Append(sp).Append("]").Append(nl);

            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Deserializes a <see cref="ProcessFlowDefinition"/> from a JSON string.
        /// </summary>
        public static ProcessFlowDefinition FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON payload is empty.", nameof(json));

            var root = ParseObject(json);
            var def = new ProcessFlowDefinition
            {
                Id = GetString(root, "id", Guid.NewGuid().ToString("N")),
                Code = GetString(root, "code", "PROC_CUSTOM"),
                Title = GetString(root, "title", "Custom Process"),
                Description = GetString(root, "description", ""),
                Version = (int)GetDouble(root, "version", 1)
            };

            // Lanes
            if (root.TryGetValue("lanes", out var lanesObj) && lanesObj is List<object> lanesList)
            {
                foreach (var item in lanesList)
                {
                    if (item is Dictionary<string, object> laneDict)
                    {
                        var lane = new ProcessFlowLane
                        {
                            Id = GetString(laneDict, "id", Guid.NewGuid().ToString("N")),
                            Title = GetString(laneDict, "title", "Lane"),
                            X = GetDouble(laneDict, "x", 0),
                            Y = GetDouble(laneDict, "y", 0),
                            Width = GetDouble(laneDict, "width", 800),
                            Height = GetDouble(laneDict, "height", 300),
                            HeaderColorHex = GetString(laneDict, "headerColor", "#64748B"),
                            BackgroundColorHex = GetString(laneDict, "backgroundColor", "#F8FAFC")
                        };
                        def.Lanes.Add(lane);
                    }
                }
            }

            // Nodes
            if (root.TryGetValue("nodes", out var nodesObj) && nodesObj is List<object> nodesList)
            {
                foreach (var item in nodesList)
                {
                    if (item is Dictionary<string, object> nodeDict)
                    {
                        var shapeStr = GetString(nodeDict, "shape", "TaskCard");
                        Enum.TryParse(shapeStr, true, out ProcessNodeShape shape);

                        var statusStr = GetString(nodeDict, "status", "Normal");
                        Enum.TryParse(statusStr, true, out ProcessNodeStatus status);

                        var node = new ProcessFlowNode
                        {
                            Id = GetString(nodeDict, "id", Guid.NewGuid().ToString("N")),
                            Title = GetString(nodeDict, "title", "Node"),
                            Subtitle = GetString(nodeDict, "subtitle", ""),
                            Shape = shape,
                            Status = status,
                            X = GetDouble(nodeDict, "x", 0),
                            Y = GetDouble(nodeDict, "y", 0),
                            Width = GetDouble(nodeDict, "width", 220),
                            Height = GetDouble(nodeDict, "height", 80),
                            HeaderColorHex = GetString(nodeDict, "headerColor", "#3B82F6"),
                            BorderColorHex = GetString(nodeDict, "borderColor", "#CBD5E1"),
                            IconGlyph = GetString(nodeDict, "icon", "📄"),
                            ActionKey = GetString(nodeDict, "actionKey", ""),
                            LaneId = GetString(nodeDict, "laneId", "")
                        };
                        def.Nodes.Add(node);
                    }
                }
            }

            // Connections
            if (root.TryGetValue("connections", out var connObj) && connObj is List<object> connList)
            {
                foreach (var item in connList)
                {
                    if (item is Dictionary<string, object> connDict)
                    {
                        var srcPortStr = GetString(connDict, "sourcePort", "Bottom");
                        Enum.TryParse(srcPortStr, true, out ProcessPortPosition srcPort);

                        var tgtPortStr = GetString(connDict, "targetPort", "Top");
                        Enum.TryParse(tgtPortStr, true, out ProcessPortPosition tgtPort);

                        var routingStr = GetString(connDict, "routing", "Orthogonal");
                        Enum.TryParse(routingStr, true, out ProcessRoutingMode routing);

                        var conn = new ProcessFlowConnection
                        {
                            Id = GetString(connDict, "id", Guid.NewGuid().ToString("N")),
                            SourceNodeId = GetString(connDict, "source", ""),
                            TargetNodeId = GetString(connDict, "target", ""),
                            SourcePort = srcPort,
                            TargetPort = tgtPort,
                            Label = GetString(connDict, "label", ""),
                            StrokeColorHex = GetString(connDict, "color", "#0EA5E9"),
                            StrokeThickness = GetDouble(connDict, "thickness", 2.0),
                            IsDashed = GetBool(connDict, "dashed", false),
                            RoutingMode = routing
                        };
                        def.Connections.Add(conn);
                    }
                }
            }

            return def;
        }

        #region Helpers & Mini Parser

        private static string Escape(string val)
        {
            if (string.IsNullOrEmpty(val)) return "";
            return val.Replace("\\", "\\\\")
                      .Replace("\"", "\\\"")
                      .Replace("\r", "\\r")
                      .Replace("\n", "\\n")
                      .Replace("\t", "\\t");
        }

        private static string GetString(Dictionary<string, object> dict, string key, string fallback)
        {
            return dict.TryGetValue(key, out var val) && val != null ? (val.ToString() ?? fallback) : fallback;
        }

        private static double GetDouble(Dictionary<string, object> dict, string key, double fallback)
        {
            if (dict.TryGetValue(key, out var val) && val != null)
            {
                if (val is double d) return d;
                if (val is long l) return l;
                if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed))
                    return parsed;
            }
            return fallback;
        }

        private static bool GetBool(Dictionary<string, object> dict, string key, bool fallback)
        {
            if (dict.TryGetValue(key, out var val) && val != null)
            {
                if (val is bool b) return b;
                if (bool.TryParse(val.ToString(), out bool parsed))
                    return parsed;
            }
            return fallback;
        }

        private static Dictionary<string, object> ParseObject(string json)
        {
            int index = 0;
            var obj = ParseValue(json, ref index) as Dictionary<string, object>;
            return obj ?? new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        private static object? ParseValue(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length) return null;

            char c = json[index];
            if (c == '{') return ParseObjectInternal(json, ref index);
            if (c == '[') return ParseArrayInternal(json, ref index);
            if (c == '"') return ParseStringInternal(json, ref index);
            if (c == 't' || c == 'f') return ParseBoolInternal(json, ref index);
            if (c == 'n') return ParseNullInternal(json, ref index);
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumberInternal(json, ref index);

            return null;
        }

        private static Dictionary<string, object> ParseObjectInternal(string json, ref int index)
        {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            index++; // skip '{'

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

                string key = ParseStringInternal(json, ref index);
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

        private static List<object> ParseArrayInternal(string json, ref int index)
        {
            var list = new List<object>();
            index++; // skip '['

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

        private static string ParseStringInternal(string json, ref int index)
        {
            SkipWhitespace(json, ref index);
            if (index >= json.Length || json[index] != '"') return "";

            index++; // skip opening '"'
            var sb = new StringBuilder();
            while (index < json.Length)
            {
                char c = json[index++];
                if (c == '"') break;
                if (c == '\\' && index < json.Length)
                {
                    char esc = json[index++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(esc); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private static bool ParseBoolInternal(string json, ref int index)
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
            return false;
        }

        private static object? ParseNullInternal(string json, ref int index)
        {
            if (json.Substring(index).StartsWith("null", StringComparison.OrdinalIgnoreCase))
            {
                index += 4;
            }
            return null;
        }

        private static double ParseNumberInternal(string json, ref int index)
        {
            int start = index;
            while (index < json.Length && (char.IsDigit(json[index]) || json[index] == '.' || json[index] == '-' || json[index] == '+'))
            {
                index++;
            }
            string numStr = json.Substring(start, index - start);
            if (double.TryParse(numStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }
            return 0;
        }

        private static void SkipWhitespace(string json, ref int index)
        {
            while (index < json.Length && char.IsWhiteSpace(json[index]))
            {
                index++;
            }
        }

        #endregion
    }
}
