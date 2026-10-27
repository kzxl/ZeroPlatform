using System;
using System.Collections.Generic;
using System.Text;

namespace ZeroData.Core.Text;

/// <summary>
/// High-performance string template interpolator supporting {{variable}} and {{variable:fallback}} patterns.
/// Designed for API clients, dynamic configurations, and logging with zero external dependencies.
/// </summary>
public static class VariableInterpolator
{
    /// <summary>
    /// Interpolates variables within a template using a dictionary lookup.
    /// </summary>
    public static string Interpolate(string template, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template) || variables == null || variables.Count == 0)
            return template ?? string.Empty;

        return Interpolate(template, key => variables.TryGetValue(key, out var val) ? val : null);
    }

    /// <summary>
    /// Interpolates variables within a template using a custom resolver callback.
    /// Supports {{var}} and {{var:default_value}} syntax.
    /// </summary>
    public static string Interpolate(string template, Func<string, string?> resolver)
    {
        if (string.IsNullOrEmpty(template) || resolver == null)
            return template ?? string.Empty;

        int firstOpen = template.IndexOf("{{", StringComparison.Ordinal);
        if (firstOpen < 0) return template;

        var sb = new StringBuilder(template.Length + 64);
        int cursor = 0;

        while (cursor < template.Length)
        {
            int openIdx = template.IndexOf("{{", cursor, StringComparison.Ordinal);
            if (openIdx < 0)
            {
                sb.Append(template, cursor, template.Length - cursor);
                break;
            }

            // Append prefix text before {{
            sb.Append(template, cursor, openIdx - cursor);

            int closeIdx = template.IndexOf("}}", openIdx + 2, StringComparison.Ordinal);
            if (closeIdx < 0)
            {
                // Unclosed {{, append remainder as-is
                sb.Append(template, openIdx, template.Length - openIdx);
                break;
            }

            string token = template.Substring(openIdx + 2, closeIdx - openIdx - 2).Trim();
            string key = token;
            string? fallback = null;

            int colonIdx = token.IndexOf(':');
            if (colonIdx >= 0)
            {
                key = token.Substring(0, colonIdx).Trim();
                fallback = token.Substring(colonIdx + 1).Trim();
            }

            string? resolved = resolver(key);
            if (!string.IsNullOrEmpty(resolved))
            {
                sb.Append(resolved);
            }
            else if (fallback != null)
            {
                sb.Append(fallback);
            }
            else
            {
                // Keep raw {{token}} if unresolved and no fallback provided
                sb.Append("{{").Append(token).Append("}}");
            }

            cursor = closeIdx + 2;
        }

        return sb.ToString();
    }
}
