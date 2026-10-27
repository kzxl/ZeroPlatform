using System;
using System.Globalization;

namespace ZeroUI.Core.Localization
{
    /// <summary>
    /// Ultra-ergonomic developer facade for real-time localization across ZeroUniverse.
    /// Usage:
    ///   string title = L.Get("Archive.Title");
    ///   string text  = L.T("Archive.Title");         // Short $t alias
    ///   string index = L.Text["Archive.Title"];     // Indexer syntax
    ///   string msg   = L.Fmt("Delete.Confirm", fileName, fileSize);
    ///   L.SetLanguage("vi-VN");
    /// </summary>
    public static class L
    {
        private sealed class IndexerProxy
        {
            public string this[string key] => LocalizationManager.Get(key);
        }

        private static readonly IndexerProxy _indexer = new IndexerProxy();

        /// <summary>
        /// Indexer proxy supporting L.Text["key"] syntax.
        /// </summary>
        public static dynamic Text => _indexer;

        /// <summary>
        /// Quick translation lookup alias (similar to $t in web i18n).
        /// </summary>
        public static string T(string key, string? defaultValue = null) => LocalizationManager.Get(key, defaultValue);

        /// <summary>
        /// Retrieves localized string with optional default fallback value.
        /// </summary>
        public static string Get(string key, string? defaultValue = null) => LocalizationManager.Get(key, defaultValue);

        /// <summary>
        /// Retrieves formatted localized string with positional arguments.
        /// </summary>
        public static string Format(string key, params object[] args) => LocalizationManager.Format(key, args);

        /// <summary>
        /// Concise alias for <see cref="Format"/>.
        /// </summary>
        public static string Fmt(string key, params object[] args) => LocalizationManager.Format(key, args);

        /// <summary>
        /// Gets current active CultureInfo.
        /// </summary>
        public static CultureInfo CurrentCulture => LocalizationManager.CurrentCulture;

        /// <summary>
        /// Gets current active culture code (e.g. "en-US", "vi-VN").
        /// </summary>
        public static string CurrentLanguage => LocalizationManager.CurrentCultureCode;

        /// <summary>
        /// Switches active language system-wide at runtime with real-time UI hot-reload.
        /// </summary>
        public static void SetLanguage(string cultureCode) => LocalizationManager.SetLanguage(cultureCode);
    }
}
