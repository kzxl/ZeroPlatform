using System;
using System.Text.Json;

namespace ZeroData.Core.Text;

/// <summary>
/// Lightweight dot-notation JSON path query engine built directly on top of System.Text.Json.JsonElement.
/// Supports property traversal ("data.user.id") and array indexing ("items[0].name") with zero allocations.
/// </summary>
public static class DotPathQuery
{
    /// <summary>
    /// Traverses a JSON document along a dot-delimited path (e.g. "data.users[0].name").
    /// </summary>
    public static JsonElement? SelectPath(this JsonDocument document, string path)
    {
        if (document == null) return null;
        return document.RootElement.SelectPath(path);
    }

    /// <summary>
    /// Traverses a JSON element along a dot-delimited path (e.g. "data.users[0].name").
    /// Returns null if any segment along the path does not exist.
    /// </summary>
    public static JsonElement? SelectPath(this JsonElement element, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return element;

        var current = element;
        var parts = path.Split('.');

        foreach (var rawPart in parts)
        {
            var part = rawPart.Trim();
            if (string.IsNullOrEmpty(part)) continue;

            int bracketOpen = part.IndexOf('[');
            if (bracketOpen >= 0)
            {
                // Has array index: e.g. "items[0]"
                string propName = part.Substring(0, bracketOpen);
                if (!string.IsNullOrEmpty(propName))
                {
                    if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(propName, out var child))
                    {
                        return null;
                    }
                    current = child;
                }

                // Process index brackets: could be multi-dimensional like [0][1]
                int curIdx = bracketOpen;
                while (curIdx >= 0 && curIdx < part.Length)
                {
                    int open = part.IndexOf('[', curIdx);
                    if (open < 0) break;
                    int close = part.IndexOf(']', open);
                    if (close < 0) return null;

                    string indexStr = part.Substring(open + 1, close - open - 1);
                    if (!int.TryParse(indexStr, out int index) || index < 0) return null;

                    if (current.ValueKind != JsonValueKind.Array) return null;

                    int counter = 0;
                    bool found = false;
                    foreach (var item in current.EnumerateArray())
                    {
                        if (counter == index)
                        {
                            current = item;
                            found = true;
                            break;
                        }
                        counter++;
                    }

                    if (!found) return null;
                    curIdx = close + 1;
                }
            }
            else
            {
                // Simple object property
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(part, out var child))
                {
                    return null;
                }
                current = child;
            }
        }

        return current;
    }
}
