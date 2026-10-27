using System;
using System.Collections.Generic;
using ZeroPrompt.Core.Templating;

namespace ZeroAgent.Dialog.Generator
{
    /// <summary>
    /// Generates structured, diverse, and contextual responses using ZeroPrompt templates.
    /// Eliminates rigid robot-like repetitive answers via template alternation.
    /// </summary>
    public sealed class DialogueResponseGenerator
    {
        private int _turnCounter = 0;

        /// <summary>
        /// Formats response using one of the available intent templates.
        /// </summary>
        public string FormatResponse(IReadOnlyList<string> templates, IReadOnlyDictionary<string, string> slots, string fallback)
        {
            if (templates == null || templates.Count == 0)
            {
                return fallback;
            }

            int index = Math.Abs(_turnCounter++) % templates.Count;
            string selectedTemplate = templates[index];

            var template = new PromptTemplate(selectedTemplate);
            var ctx = new PromptContext();
            foreach (var kvp in slots)
            {
                ctx.Set(kvp.Key, kvp.Value);
            }

            return template.Render(ctx);
        }

        public string FormatClarification(string slotPrompt)
        {
            return $"ℹ️ {slotPrompt}";
        }

        public string FormatPermissionDenied(string requiredPermission)
        {
            return $"⛔ Truy cập bị từ chối: Thao tác này yêu cầu quyền [{requiredPermission}]. Tài khoản hiện tại không có quyền can thiệp.";
        }

        public string FormatGuestLoginRequired(string requiredPermission, string? intentName = null)
        {
            string intentDesc = string.IsNullOrEmpty(intentName) ? "Thao tác này" : $"Thao tác '{intentName}'";
            return $"🔒 Yêu cầu đăng nhập: Bạn đang ở chế độ Khách (Guest / Chưa đăng nhập). {intentDesc} yêu cầu xác thực tài khoản có quyền [{requiredPermission}]. Quý khách vui lòng đăng nhập để tiếp tục tra cứu hoặc thực hiện tác vụ này.";
        }

        /// <summary>
        /// Renders tabular data (headers and rows) into a clean GitHub Flavored Markdown table.
        /// </summary>
        public string FormatMarkdownTable(IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<string>> rows)
        {
            if (headers == null || headers.Count == 0) return string.Empty;

            var sb = new System.Text.StringBuilder();
            // Header row
            sb.Append("| ");
            for (int i = 0; i < headers.Count; i++)
            {
                sb.Append(headers[i]).Append(" | ");
            }
            sb.AppendLine();

            // Separator row
            sb.Append("| ");
            for (int i = 0; i < headers.Count; i++)
            {
                sb.Append("--- | ");
            }
            sb.AppendLine();

            // Data rows
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    sb.Append("| ");
                    for (int i = 0; i < headers.Count; i++)
                    {
                        string val = i < row.Count ? row[i] : string.Empty;
                        sb.Append(val).Append(" | ");
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Converts a JSON array of record objects into a readable Markdown table.
        /// </summary>
        public string TryFormatJsonAsTable(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("["))
                return json;

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                    return json;

                var array = doc.RootElement;
                if (array.GetArrayLength() == 0) return "*(Bảng rỗng - Không có bản ghi nào)*";

                var headers = new List<string>();
                var first = array[0];
                if (first.ValueKind != System.Text.Json.JsonValueKind.Object)
                    return json;

                foreach (var prop in first.EnumerateObject())
                {
                    headers.Add(prop.Name);
                }

                var rows = new List<IReadOnlyList<string>>();
                foreach (var item in array.EnumerateArray())
                {
                    var row = new List<string>();
                    foreach (var h in headers)
                    {
                        if (item.TryGetProperty(h, out var val))
                        {
                            row.Add(val.ToString());
                        }
                        else
                        {
                            row.Add(string.Empty);
                        }
                    }
                    rows.Add(row);
                }

                return FormatMarkdownTable(headers, rows);
            }
            catch
            {
                return json;
            }
        }
    }
}
