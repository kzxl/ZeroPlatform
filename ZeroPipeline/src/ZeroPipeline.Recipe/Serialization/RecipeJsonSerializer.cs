using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ZeroPipeline.Recipe.Models;

namespace ZeroPipeline.Recipe.Serialization
{
    /// <summary>
    /// Pure C# lightweight, zero-dependency JSON serializer and deserializer for RecipeModel.
    /// Provides fast, cross-platform persistence across .NET Standard 2.0, .NET 4.6.2, and .NET 8.0+.
    /// </summary>
    public static class RecipeJsonSerializer
    {
        public static string Serialize(RecipeModel recipe, bool pretty = true)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));

            var sb = new StringBuilder(1024);
            int indent = 0;

            void AppendIndent()
            {
                if (pretty) sb.Append(new string(' ', indent * 2));
            }

            void AppendLine()
            {
                if (pretty) sb.AppendLine();
            }

            sb.Append('{');
            AppendLine();
            indent++;

            AppendIndent();
            sb.Append("\"recipeId\": \"").Append(Escape(recipe.RecipeId)).Append("\",");
            AppendLine();

            AppendIndent();
            sb.Append("\"name\": \"").Append(Escape(recipe.Name)).Append("\",");
            AppendLine();

            AppendIndent();
            sb.Append("\"version\": \"").Append(Escape(recipe.Version)).Append("\",");
            AppendLine();

            AppendIndent();
            sb.Append("\"description\": \"").Append(Escape(recipe.Description)).Append("\",");
            AppendLine();

            AppendIndent();
            sb.Append("\"createdAtUtc\": \"").Append(recipe.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture)).Append("\",");
            AppendLine();

            // Nodes
            AppendIndent();
            sb.Append("\"nodes\": [");
            AppendLine();
            indent++;

            for (int i = 0; i < recipe.Nodes.Count; i++)
            {
                var n = recipe.Nodes[i];
                AppendIndent();
                sb.Append('{');
                AppendLine();
                indent++;

                AppendIndent();
                sb.Append("\"id\": \"").Append(Escape(n.Id)).Append("\",");
                AppendLine();

                AppendIndent();
                sb.Append("\"name\": \"").Append(Escape(n.Name)).Append("\",");
                AppendLine();

                AppendIndent();
                sb.Append("\"nodeType\": \"").Append(Escape(n.NodeType)).Append("\",");
                AppendLine();

                AppendIndent();
                sb.Append("\"parameters\": {");
                AppendLine();
                indent++;

                int paramIdx = 0;
                foreach (var kvp in n.Parameters)
                {
                    AppendIndent();
                    sb.Append('"').Append(Escape(kvp.Key)).Append("\": \"").Append(Escape(kvp.Value)).Append('"');
                    if (++paramIdx < n.Parameters.Count) sb.Append(',');
                    AppendLine();
                }

                indent--;
                AppendIndent();
                sb.Append('}');
                AppendLine();

                indent--;
                AppendIndent();
                sb.Append('}');
                if (i < recipe.Nodes.Count - 1) sb.Append(',');
                AppendLine();
            }

            indent--;
            AppendIndent();
            sb.Append("],");
            AppendLine();

            // Connections
            AppendIndent();
            sb.Append("\"connections\": [");
            AppendLine();
            indent++;

            for (int i = 0; i < recipe.Connections.Count; i++)
            {
                var c = recipe.Connections[i];
                AppendIndent();
                sb.Append('{');
                AppendLine();
                indent++;

                AppendIndent();
                sb.Append("\"sourceNodeId\": \"").Append(Escape(c.SourceNodeId)).Append("\",");
                AppendLine();

                AppendIndent();
                sb.Append("\"sourcePortName\": \"").Append(Escape(c.SourcePortName)).Append("\",");
                AppendLine();

                AppendIndent();
                sb.Append("\"targetNodeId\": \"").Append(Escape(c.TargetNodeId)).Append("\",");
                AppendLine();

                AppendIndent();
                sb.Append("\"targetPortName\": \"").Append(Escape(c.TargetPortName)).Append('"');
                AppendLine();

                indent--;
                AppendIndent();
                sb.Append('}');
                if (i < recipe.Connections.Count - 1) sb.Append(',');
                AppendLine();
            }

            indent--;
            AppendIndent();
            sb.Append(']');
            AppendLine();

            indent--;
            sb.Append('}');
            return sb.ToString();
        }

        public static RecipeModel Deserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON content cannot be null or empty.", nameof(json));

            var parser = new JsonParser(json);
            var root = parser.Parse() as Dictionary<string, object>;
            if (root == null)
                throw new FormatException("Expected root JSON object for RecipeModel.");

            var recipe = new RecipeModel();

            if (root.TryGetValue("recipeId", out var rid) && rid is string ridStr) recipe.RecipeId = ridStr;
            if (root.TryGetValue("name", out var n) && n is string nStr) recipe.Name = nStr;
            if (root.TryGetValue("version", out var v) && v is string vStr) recipe.Version = vStr;
            if (root.TryGetValue("description", out var d) && d is string dStr) recipe.Description = dStr;
            if (root.TryGetValue("createdAtUtc", out var ca) && ca is string caStr &&
                DateTime.TryParse(caStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                recipe.CreatedAtUtc = dt;
            }

            if (root.TryGetValue("nodes", out var nodesObj) && nodesObj is List<object> nodeList)
            {
                foreach (var item in nodeList)
                {
                    if (item is Dictionary<string, object> nodeDict)
                    {
                        var nodeModel = new NodeRecipeModel();
                        if (nodeDict.TryGetValue("id", out var nid) && nid is string nidStr) nodeModel.Id = nidStr;
                        if (nodeDict.TryGetValue("name", out var nname) && nname is string nnameStr) nodeModel.Name = nnameStr;
                        if (nodeDict.TryGetValue("nodeType", out var nt) && nt is string ntStr) nodeModel.NodeType = ntStr;

                        if (nodeDict.TryGetValue("parameters", out var pObj) && pObj is Dictionary<string, object> pDict)
                        {
                            foreach (var pKvp in pDict)
                            {
                                nodeModel.Parameters[pKvp.Key] = pKvp.Value?.ToString() ?? string.Empty;
                            }
                        }

                        recipe.Nodes.Add(nodeModel);
                    }
                }
            }

            if (root.TryGetValue("connections", out var connObj) && connObj is List<object> connList)
            {
                foreach (var item in connList)
                {
                    if (item is Dictionary<string, object> connDict)
                    {
                        var connModel = new ConnectionRecipeModel();
                        if (connDict.TryGetValue("sourceNodeId", out var snid) && snid is string snidStr) connModel.SourceNodeId = snidStr;
                        if (connDict.TryGetValue("sourcePortName", out var spn) && spn is string spnStr) connModel.SourcePortName = spnStr;
                        if (connDict.TryGetValue("targetNodeId", out var tnid) && tnid is string tnidStr) connModel.TargetNodeId = tnidStr;
                        if (connDict.TryGetValue("targetPortName", out var tpn) && tpn is string tpnStr) connModel.TargetPortName = tpnStr;

                        recipe.Connections.Add(connModel);
                    }
                }
            }

            return recipe;
        }

        private static string Escape(string? s)
        {
            if (s == null) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private sealed class JsonParser
        {
            private readonly string _json;
            private int _pos;

            public JsonParser(string json)
            {
                _json = json;
                _pos = 0;
            }

            public object? Parse()
            {
                SkipWhitespace();
                if (_pos >= _json.Length) return null;

                char c = _json[_pos];
                if (c == '{') return ParseObject();
                if (c == '[') return ParseArray();
                if (c == '"') return ParseString();
                if (char.IsDigit(c) || c == '-') return ParseNumber();
                if (c == 't' || c == 'f') return ParseBool();
                if (c == 'n') return ParseNull();

                throw new FormatException($"Unexpected character '{c}' at position {_pos}");
            }

            private Dictionary<string, object> ParseObject()
            {
                var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                _pos++; // skip '{'

                while (_pos < _json.Length)
                {
                    SkipWhitespace();
                    if (_pos >= _json.Length) break;
                    if (_json[_pos] == '}') { _pos++; return dict; }

                    string key = ParseString();
                    SkipWhitespace();
                    if (_pos < _json.Length && _json[_pos] == ':') _pos++;
                    SkipWhitespace();

                    object? val = Parse();
                    if (val != null) dict[key] = val;

                    SkipWhitespace();
                    if (_pos < _json.Length && _json[_pos] == ',') _pos++;
                }

                return dict;
            }

            private List<object> ParseArray()
            {
                var list = new List<object>();
                _pos++; // skip '['

                while (_pos < _json.Length)
                {
                    SkipWhitespace();
                    if (_pos >= _json.Length) break;
                    if (_json[_pos] == ']') { _pos++; return list; }

                    object? val = Parse();
                    if (val != null) list.Add(val);

                    SkipWhitespace();
                    if (_pos < _json.Length && _json[_pos] == ',') _pos++;
                }

                return list;
            }

            private string ParseString()
            {
                _pos++; // skip opening quote
                var sb = new StringBuilder();

                while (_pos < _json.Length)
                {
                    char c = _json[_pos++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\' && _pos < _json.Length)
                    {
                        char esc = _json[_pos++];
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

            private object ParseNumber()
            {
                int start = _pos;
                bool isFloating = false;

                while (_pos < _json.Length &&
                       (char.IsDigit(_json[_pos]) || _json[_pos] == '-' || _json[_pos] == '+' || _json[_pos] == '.' || _json[_pos] == 'e' || _json[_pos] == 'E'))
                {
                    if (_json[_pos] == '.' || _json[_pos] == 'e' || _json[_pos] == 'E') isFloating = true;
                    _pos++;
                }

                string str = _json.Substring(start, _pos - start);
                if (isFloating && double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out double dVal))
                    return dVal;
                if (long.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out long lVal))
                    return lVal;

                return str;
            }

            private bool ParseBool()
            {
                if (_json.Substring(_pos).StartsWith("true", StringComparison.OrdinalIgnoreCase))
                {
                    _pos += 4;
                    return true;
                }
                if (_json.Substring(_pos).StartsWith("false", StringComparison.OrdinalIgnoreCase))
                {
                    _pos += 5;
                    return false;
                }
                throw new FormatException($"Invalid boolean at position {_pos}");
            }

            private object? ParseNull()
            {
                if (_json.Substring(_pos).StartsWith("null", StringComparison.OrdinalIgnoreCase))
                {
                    _pos += 4;
                    return null;
                }
                throw new FormatException($"Invalid null at position {_pos}");
            }

            private void SkipWhitespace()
            {
                while (_pos < _json.Length && char.IsWhiteSpace(_json[_pos]))
                {
                    _pos++;
                }
            }
        }
    }
}
