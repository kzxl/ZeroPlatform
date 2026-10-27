using System;
using System.Diagnostics;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Supported hyperlink target classifications.
    /// </summary>
    public enum HyperlinkEditKind
    {
        Web,
        Email,
        File,
        Custom
    }

    /// <summary>
    /// Event arguments raised before or during hyperlink navigation.
    /// </summary>
    public class HyperlinkNavigateEventArgs : EventArgs
    {
        public string Target { get; }
        public Uri? Uri { get; }
        public bool Cancel { get; set; }
        public bool Handled { get; set; }

        public HyperlinkNavigateEventArgs(string target, Uri? uri = null)
        {
            Target = target ?? string.Empty;
            Uri = uri;
        }
    }

    /// <summary>
    /// Helper utilities for URL detection, scheme validation, and cross-platform process launching.
    /// </summary>
    public static class HyperlinkHelper
    {
        public static HyperlinkEditKind DetectKind(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return HyperlinkEditKind.Web;

            var trimmed = url!.Trim();
            if (trimmed.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) || trimmed.Contains("@") && !trimmed.Contains("://"))
                return HyperlinkEditKind.Email;

            if (trimmed.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("\\\\", StringComparison.Ordinal) ||
                (trimmed.Length > 2 && trimmed[1] == ':' && (trimmed[2] == '\\' || trimmed[2] == '/')))
                return HyperlinkEditKind.File;

            return HyperlinkEditKind.Web;
        }

        public static bool TryCreateUri(string? text, out Uri? uri)
        {
            uri = null;
            if (string.IsNullOrWhiteSpace(text)) return false;

            text = text!.Trim();

            if (Uri.TryCreate(text, UriKind.Absolute, out uri))
                return true;

            // Prepend https:// if pure domain like "zeroui.io" or "google.com"
            if (!text.Contains("://") && !text.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.TryCreate("https://" + text, UriKind.Absolute, out uri);
            }

            return false;
        }

        /// <summary>
        /// Safely opens the specified target URL or file path in the OS default shell.
        /// Works consistently across .NET Framework 4.6.2 and .NET 8.0 without throwing Win32 exceptions.
        /// </summary>
        public static bool OpenTarget(string? target)
        {
            if (string.IsNullOrWhiteSpace(target)) return false;

            try
            {
                string url = target!.Trim();
                if (!url.Contains("://") && !url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("\\\\"))
                {
                    if (url.Contains("@"))
                        url = "mailto:" + url;
                    else
                        url = "https://" + url;
                }

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
