using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ZeroAsset.Variants
{
    /// <summary>
    /// Utility for parsing, generating, and validating zero-byte asset variants and virtual copy suffixes.
    /// Suffix syntax is standardized as: {BaseName}#vc{Index}{Extension} (e.g. Photo#vc1.jpg).
    /// </summary>
    public static class VariantIdentifier
    {
        public const string VariantPrefix = "#vc";
        private static readonly Regex VariantRegex = new Regex(@"#vc(\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Determines whether the path corresponds to a virtual variant/copy based on its filename suffix.
        /// </summary>
        public static bool IsVariant(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string? fileNameWithoutExt = Path.GetFileNameWithoutExtension(path);
            return !string.IsNullOrEmpty(fileNameWithoutExt) && VariantRegex.IsMatch(fileNameWithoutExt);
        }

        /// <summary>
        /// Extracts the 1-based variant copy index from the path. Returns 0 if the path is a master asset.
        /// </summary>
        public static int GetVariantIndex(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return 0;
            string? fileNameWithoutExt = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(fileNameWithoutExt)) return 0;

            var match = VariantRegex.Match(fileNameWithoutExt);
            if (match.Success && int.TryParse(match.Groups[1].Value, out int idx))
            {
                return idx;
            }
            return 0;
        }

        /// <summary>
        /// Strips the variant suffix from the filename and returns the canonical physical master path on disk.
        /// </summary>
        public static string GetMasterPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string dir = Path.GetDirectoryName(path) ?? string.Empty;
            string ext = Path.GetExtension(path) ?? string.Empty;
            string name = Path.GetFileNameWithoutExtension(path) ?? string.Empty;

            var match = VariantRegex.Match(name);
            if (match.Success)
            {
                string baseName = name.Substring(0, match.Index);
                return Path.Combine(dir, baseName + ext);
            }

            return path!;
        }

        /// <summary>
        /// Generates a valid variant path for a master asset without nesting suffixes (e.g. Photo#vc1#vc2.jpg -> Photo#vc2.jpg).
        /// </summary>
        public static string BuildVariantPath(string? masterPath, int copyIndex)
        {
            if (string.IsNullOrWhiteSpace(masterPath)) throw new ArgumentException("Master path cannot be empty.", nameof(masterPath));
            if (copyIndex <= 0) return masterPath!;

            string canonicalMaster = GetMasterPath(masterPath);
            string dir = Path.GetDirectoryName(canonicalMaster) ?? string.Empty;
            string ext = Path.GetExtension(canonicalMaster) ?? string.Empty;
            string baseName = Path.GetFileNameWithoutExtension(canonicalMaster) ?? string.Empty;

            return Path.Combine(dir, $"{baseName}{VariantPrefix}{copyIndex}{ext}");
        }

        /// <summary>
        /// Formats user-facing display name with copy label (e.g. "DSC_1000.NEF (Copy 1)").
        /// </summary>
        public static string FormatDisplayName(string? originalName, int copyIndex)
        {
            if (string.IsNullOrWhiteSpace(originalName)) return string.Empty;
            if (copyIndex <= 0) return originalName!;
            return $"{originalName} (Copy {copyIndex})";
        }
    }
}
