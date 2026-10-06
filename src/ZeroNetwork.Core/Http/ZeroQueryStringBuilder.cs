using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace ZeroNetwork.Http
{
    /// <summary>
    /// Culture-invariant query-string builder used by <see cref="ZeroApiClient.AppendQueryParams"/>.
    /// </summary>
    internal static class ZeroQueryStringBuilder
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> s_propertyCache = new ConcurrentDictionary<Type, PropertyInfo[]>();

        public static string Append(string url, object? queryParams)
        {
            if (queryParams == null) return url; // unchanged legacy contract, even for a null url
            if (url == null) throw new ArgumentNullException(nameof(url));

            // The fragment must stay at the very end of the URL.
            int hashIndex = url.IndexOf('#');
            string fragment = hashIndex >= 0 ? url.Substring(hashIndex) : string.Empty;
            string path = hashIndex >= 0 ? url.Substring(0, hashIndex) : url;

            var sb = new StringBuilder(path, path.Length + 64);
            char separator = path.IndexOf('?') < 0 ? '?' : (path.EndsWith("?", StringComparison.Ordinal) || path.EndsWith("&", StringComparison.Ordinal) ? '\0' : '&');

            if (queryParams is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    string? key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                    if (string.IsNullOrEmpty(key)) continue;
                    AppendValue(sb, ref separator, key!, entry.Value);
                }
            }
            else
            {
                PropertyInfo[] properties = s_propertyCache.GetOrAdd(queryParams.GetType(), GetReadableProperties);
                for (int i = 0; i < properties.Length; i++)
                {
                    AppendValue(sb, ref separator, properties[i].Name, properties[i].GetValue(queryParams, null));
                }
            }

            sb.Append(fragment);
            return sb.ToString();
        }

        internal static string FormatValue(object value)
        {
            switch (value)
            {
                case string s:
                    return s;
                case bool b:
                    return b ? "true" : "false";
                case DateTime dateTime:
                    return dateTime.ToString("O", CultureInfo.InvariantCulture);
                case DateTimeOffset dateTimeOffset:
                    return dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);
                case Enum e:
                    return e.ToString();
                case double d:
                    return d.ToString("R", CultureInfo.InvariantCulture); // "G" is not round-trip on .NET Framework
                case float f:
                    return f.ToString("R", CultureInfo.InvariantCulture);
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return value.ToString() ?? string.Empty;
            }
        }

        private static void AppendValue(StringBuilder sb, ref char separator, string name, object? value)
        {
            if (value == null) return;

            if (!(value is string) && value is IEnumerable sequence)
            {
                // Repeated keys (ids=1&ids=2) — the convention understood by ASP.NET Core and most REST frameworks.
                foreach (object? item in sequence)
                {
                    if (item != null) AppendPair(sb, ref separator, name, FormatValue(item));
                }
                return;
            }

            AppendPair(sb, ref separator, name, FormatValue(value));
        }

        private static void AppendPair(StringBuilder sb, ref char separator, string name, string value)
        {
            if (separator != '\0') sb.Append(separator);
            separator = '&';
            sb.Append(Uri.EscapeDataString(name));
            sb.Append('=');
            sb.Append(Uri.EscapeDataString(value));
        }

        private static PropertyInfo[] GetReadableProperties(Type type)
        {
            PropertyInfo[] all = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var result = new System.Collections.Generic.List<PropertyInfo>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].CanRead && all[i].GetIndexParameters().Length == 0) result.Add(all[i]);
            }
            return result.ToArray();
        }
    }
}
