using System;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Scaling algorithm applied to image rendering within bounding containers.
    /// </summary>
    public enum ImageScaleMode
    {
        Cover = 0,
        Contain = 1,
        Center = 2,
        Stretch = 3,
        Zoom = 1,
        Clip = 2,
        Squeeze = 3
    }

    /// <summary>
    /// Operator and user activity presence status indicators for avatar controls.
    /// </summary>
    public enum AvatarStatus
    {
        None,
        Online,
        Busy,
        Away,
        Offline
    }

    /// <summary>
    /// Helper utilities for avatar initials extraction, deterministic background palette selection,
    /// and operator presence status indicator styling.
    /// </summary>
    public static class AvatarHelper
    {
        /// <summary>
        /// Extracts a 1- or 2-letter uppercase initials monogram from a given full name or username.
        /// Handles single names ("Admin" -> "AD"), compound names ("John Doe" -> "JD"), and hyphenated names ("Anna-Marie" -> "AM").
        /// </summary>
        public static string ExtractInitials(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var words = text!.Trim().Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return string.Empty;
            if (words.Length == 1)
            {
                return words[0].Length >= 2 ? words[0].Substring(0, 2).ToUpperInvariant() : words[0].ToUpperInvariant();
            }
            return (words[0].Substring(0, 1) + words[words.Length - 1].Substring(0, 1)).ToUpperInvariant();
        }

        /// <summary>
        /// Deterministically maps an arbitrary seed string (e.g. username, employee ID) to a vibrant enterprise accent color hex.
        /// </summary>
        public static string GetDeterministicColorHex(string seed)
        {
            if (string.IsNullOrEmpty(seed)) return "#4F46E5";
            int hash = Math.Abs(seed.GetHashCode());
            string[] colors = new[]
            {
                "#4F46E5", // Indigo
                "#10B981", // Emerald
                "#0EA5E9", // Sky
                "#F59E0B", // Amber
                "#EC4899", // Pink
                "#8B5CF6", // Purple
                "#06B6D4"  // Cyan
            };
            return colors[hash % colors.Length];
        }

        /// <summary>
        /// Returns the standard semantic color hex associated with an <see cref="AvatarStatus"/>.
        /// </summary>
        public static string GetStatusColorHex(AvatarStatus status) => status switch
        {
            AvatarStatus.Online => "#10B981",
            AvatarStatus.Busy => "#EF4444",
            AvatarStatus.Away => "#F59E0B",
            _ => "#64748B" // Slate Offline
        };
    }

    /// <summary>
    /// Zero-allocation 2D viewport scaling and aspect ratio calculation engine.
    /// </summary>
    public static class PictureScaleMath
    {
        /// <summary>
        /// Calculates the destination viewport rectangle for an image of dimensions (imgWidth x imgHeight)
        /// rendered within target rectangle (targetX, targetY, targetWidth, targetHeight) under the specified <see cref="ImageScaleMode"/>.
        /// </summary>
        public static void CalculateDestination(
            double targetX, double targetY, double targetWidth, double targetHeight,
            double imgWidth, double imgHeight, ImageScaleMode mode,
            out double destX, out double destY, out double destW, out double destH)
        {
            if (imgWidth <= 0 || imgHeight <= 0 || targetWidth <= 0 || targetHeight <= 0)
            {
                destX = targetX;
                destY = targetY;
                destW = targetWidth;
                destH = targetHeight;
                return;
            }

            switch (mode)
            {
                case ImageScaleMode.Stretch:
                    destX = targetX;
                    destY = targetY;
                    destW = targetWidth;
                    destH = targetHeight;
                    break;

                case ImageScaleMode.Center:
                    destW = imgWidth;
                    destH = imgHeight;
                    destX = targetX + (targetWidth - imgWidth) / 2.0;
                    destY = targetY + (targetHeight - imgHeight) / 2.0;
                    break;

                case ImageScaleMode.Contain:
                    double scaleFit = Math.Min(targetWidth / imgWidth, targetHeight / imgHeight);
                    destW = imgWidth * scaleFit;
                    destH = imgHeight * scaleFit;
                    destX = targetX + (targetWidth - destW) / 2.0;
                    destY = targetY + (targetHeight - destH) / 2.0;
                    break;

                case ImageScaleMode.Cover:
                default:
                    double scaleFill = Math.Max(targetWidth / imgWidth, targetHeight / imgHeight);
                    destW = imgWidth * scaleFill;
                    destH = imgHeight * scaleFill;
                    destX = targetX + (targetWidth - destW) / 2.0;
                    destY = targetY + (targetHeight - destH) / 2.0;
                    break;
            }
        }
    }
}
