using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace ZeroUI.Core.Localization
{
    /// <summary>
    /// Centralized high-performance Internationalization and Localization Coordinator for ZeroUniverse.
    /// Provides lock-free O(1) thread-safe lookups, atomic pointer swapping on runtime language changes,
    /// self-healing multi-tier fallbacks, zero-dependency flat JSON scanning, and automatic missing key collection.
    /// Fully compatible across .NET Framework 4.6.2, .NET Standard 2.0, and .NET 8+.
    /// </summary>
    public static class LocalizationManager
    {
        private static readonly ConcurrentDictionary<string, LocalizationTable> Tables =
            new ConcurrentDictionary<string, LocalizationTable>(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, byte> MissingKeys =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);

        private static volatile LocalizationTable _activeTable;
        private static volatile LocalizationTable _fallbackTable;
        private static volatile LocalizationTable? _neutralFallbackTable;

        private static string _currentCultureCode = "en-US";
        private static string _fallbackCultureCode = "en-US";

        public static event EventHandler? CultureChanged;

        static LocalizationManager()
        {
            // Seed baseline English and Vietnamese dictionary from ZeroLocalizer to guarantee built-in readiness
            var defaultEn = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Common.Ok"] = "OK",
                ["Common.Cancel"] = "Cancel",
                ["Common.Apply"] = "Apply",
                ["Common.Close"] = "Close",
                ["Common.Clear"] = "Clear",
                ["Common.Reset"] = "Reset",
                ["Common.Save"] = "Save",
                ["Common.Search"] = "Search...",
                ["Common.Loading"] = "Loading...",
                ["Common.Refresh"] = "Refresh"
            };

            var defaultVi = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Common.Ok"] = "Đồng ý",
                ["Common.Cancel"] = "Hủy",
                ["Common.Apply"] = "Áp dụng",
                ["Common.Close"] = "Đóng",
                ["Common.Clear"] = "Xóa",
                ["Common.Reset"] = "Đặt lại",
                ["Common.Save"] = "Lưu",
                ["Common.Search"] = "Tìm kiếm...",
                ["Common.Loading"] = "Đang tải...",
                ["Common.Refresh"] = "Làm mới"
            };

            _fallbackTable = new LocalizationTable("en-US", defaultEn);
            Tables["en-US"] = _fallbackTable;
            Tables["en"] = _fallbackTable;

            var viTable = new LocalizationTable("vi-VN", defaultVi);
            Tables["vi-VN"] = viTable;
            Tables["vi"] = viTable;

            _activeTable = _fallbackTable;
        }

        #region Properties

        public static string CurrentCultureCode => _currentCultureCode;

        public static string FallbackCultureCode
        {
            get => _fallbackCultureCode;
            set
            {
                _fallbackCultureCode = value ?? "en-US";
                if (Tables.TryGetValue(_fallbackCultureCode, out var table))
                {
                    _fallbackTable = table;
                }
            }
        }

        public static CultureInfo CurrentCulture
        {
            get
            {
                try
                {
                    return CultureInfo.GetCultureInfo(_currentCultureCode);
                }
                catch
                {
                    return CultureInfo.InvariantCulture;
                }
            }
        }

        #endregion

        #region Language Loading & Registration

        /// <summary>
        /// Registers or updates a culture dictionary directly from memory.
        /// </summary>
        public static void RegisterTable(string cultureCode, IDictionary<string, string> dictionary)
        {
            if (string.IsNullOrWhiteSpace(cultureCode) || dictionary == null) return;

            string normalizedCode = NormalizeCultureCode(cultureCode);
            var table = new LocalizationTable(normalizedCode, dictionary);
            Tables[normalizedCode] = table;

            // Also map 2-letter ISO name if available
            int dashIndex = normalizedCode.IndexOf('-');
            if (dashIndex > 0)
            {
                string neutral = normalizedCode.Substring(0, dashIndex);
                Tables.TryAdd(neutral, table);
            }

            if (string.Equals(_currentCultureCode, normalizedCode, StringComparison.OrdinalIgnoreCase))
            {
                _activeTable = table;
                CultureChanged?.Invoke(null, EventArgs.Empty);
            }
            if (string.Equals(_fallbackCultureCode, normalizedCode, StringComparison.OrdinalIgnoreCase))
            {
                _fallbackTable = table;
            }
        }

        /// <summary>
        /// Parses and registers flat JSON text for the specified culture.
        /// </summary>
        public static void LoadLanguageJson(string cultureCode, string jsonText)
        {
            if (string.IsNullOrWhiteSpace(cultureCode) || string.IsNullOrWhiteSpace(jsonText)) return;

            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            JsonScanner.Parse(jsonText, dict);
            RegisterTable(cultureCode, dict);
        }

        /// <summary>
        /// Loads all JSON files in the specified directory (e.g. "en-US.json", "vi-VN.json", "ja.json").
        /// </summary>
        public static int LoadFromDirectory(string directoryPath, string searchPattern = "*.json")
        {
            if (!Directory.Exists(directoryPath)) return 0;

            int count = 0;
            string[] files = Directory.GetFiles(directoryPath, searchPattern);
            foreach (var file in files)
            {
                try
                {
                    string cultureCode = Path.GetFileNameWithoutExtension(file);
                    string json = File.ReadAllText(file, Encoding.UTF8);
                    LoadLanguageJson(cultureCode, json);
                    count++;
                }
                catch
                {
                    // Fault-tolerant: Skip corrupted file without crashing host application
                }
            }
            return count;
        }

        /// <summary>
        /// Loads an embedded JSON resource from the target assembly.
        /// </summary>
        public static bool LoadEmbeddedJson(Assembly assembly, string resourceName, string cultureCode)
        {
            if (assembly == null || string.IsNullOrWhiteSpace(resourceName)) return false;

            try
            {
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return false;
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        LoadLanguageJson(cultureCode, json);
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Language Switching

        /// <summary>
        /// Switches active language at runtime with atomic pointer swap and real-time event dispatch.
        /// </summary>
        public static void SetLanguage(string cultureCode)
        {
            if (string.IsNullOrWhiteSpace(cultureCode)) return;

            string normalized = NormalizeCultureCode(cultureCode);
            if (string.Equals(_currentCultureCode, normalized, StringComparison.OrdinalIgnoreCase)) return;

            _currentCultureCode = normalized;

            // Resolve active table
            if (Tables.TryGetValue(normalized, out var table))
            {
                _activeTable = table;
            }
            else
            {
                // Fallback to neutral 2-letter culture if specific regional code not found
                int dash = normalized.IndexOf('-');
                if (dash > 0 && Tables.TryGetValue(normalized.Substring(0, dash), out var neutralTable))
                {
                    _activeTable = neutralTable;
                }
                else
                {
                    _activeTable = _fallbackTable;
                }
            }

            // Resolve neutral fallback table
            int dashIndex = _currentCultureCode.IndexOf('-');
            if (dashIndex > 0)
            {
                string neutral = _currentCultureCode.Substring(0, dashIndex);
                if (Tables.TryGetValue(neutral, out var neutralTable))
                {
                    _neutralFallbackTable = neutralTable;
                }
                else
                {
                    _neutralFallbackTable = null;
                }
            }
            else
            {
                _neutralFallbackTable = null;
            }

            // Update thread culture
            try
            {
                var ci = CultureInfo.GetCultureInfo(_currentCultureCode);
                CultureInfo.CurrentUICulture = ci;
            }
            catch { }

            // Fire real-time notification to all subscribed UI forms/views
            CultureChanged?.Invoke(null, EventArgs.Empty);
        }

        #endregion

        #region String Lookup & Format

        /// <summary>
        /// Retrieves the localized string for the specified key using lock-free multi-tier fallback.
        /// </summary>
        public static string Get(string key, string? defaultValue = null)
        {
            if (key == null) return string.Empty;

            // Tier 1: Active Culture Table
            var active = _activeTable;
            if (active != null && active.TryGetValue(key, out string val))
            {
                return val;
            }

            // Tier 2: Neutral Culture Table (e.g. "vi" when current is "vi-VN")
            var neutral = _neutralFallbackTable;
            if (neutral != null && neutral.TryGetValue(key, out val))
            {
                active?.Memoize(key, val);
                return val;
            }

            // Tier 3: Global Base Fallback Table (e.g. "en-US")
            var fallback = _fallbackTable;
            if (fallback != null && fallback.TryGetValue(key, out val))
            {
                active?.Memoize(key, val);
                return val;
            }

            // Tier 4: Caller-provided default value
            if (defaultValue != null)
            {
                return defaultValue;
            }

            // Tier 5: Record as missing key
            MissingKeys.TryAdd(key, 0);

#if DEBUG
            return "[!" + key + "!]";
#else
            return key;
#endif
        }

        /// <summary>
        /// Retrieves a localized string formatted with positional arguments.
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            string format = Get(key);
            if (args == null || args.Length == 0) return format;

            try
            {
                return string.Format(CurrentCulture, format, args);
            }
            catch
            {
                return format;
            }
        }

        /// <summary>
        /// Retrieves localized string using StringId enum.
        /// </summary>
        public static string GetString(StringId id)
        {
            string key = id.ToString();
            var active = _activeTable;
            if (active != null && active.TryGetValue(key, out string val))
            {
                return val;
            }
            return Localizer.GetString(id);
        }

        /// <summary>
        /// Retrieves localized string using legacy ZeroStringId enum for backward compatibility.
        /// </summary>
        [Obsolete("Use StringId overload instead.")]
        public static string GetString(ZeroStringId id) => GetString((StringId)id);

        #endregion

        #region Missing Keys Harvester

        /// <summary>
        /// Returns a snapshot of all keys that were requested at runtime but missing from translations.
        /// </summary>
        public static ICollection<string> GetMissingKeys()
        {
            return MissingKeys.Keys;
        }

        /// <summary>
        /// Exports recorded missing keys to a JSON file template for translators.
        /// </summary>
        public static void ExportMissingKeys(string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath) || MissingKeys.IsEmpty) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                bool first = true;
                foreach (var key in MissingKeys.Keys)
                {
                    if (!first) sb.AppendLine(",");
                    sb.Append("  \"").Append(key).Append("\": \"\"");
                    first = false;
                }
                sb.AppendLine();
                sb.AppendLine("}");

                File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        #endregion

        private static string NormalizeCultureCode(string code)
        {
            return code.Trim().Replace('_', '-');
        }
    }
}
