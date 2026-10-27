using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Curated enterprise color palettes for <c>ColorPickEdit</c>.
    /// </summary>
    public static class ColorPalette
    {
        /// <summary>
        /// Curated 24 modern enterprise colors suitable for dark and light industrial interfaces.
        /// </summary>
        public static IReadOnlyList<string> StandardPalette { get; } = new[]
        {
            "#0F172A", // Slate 900
            "#334155", // Slate 700
            "#64748B", // Slate 500
            "#94A3B8", // Slate 400
            "#CBD5E1", // Slate 300
            "#FFFFFF", // Pure White

            "#EF4444", // Red 500 (Danger / Alarm)
            "#F97316", // Orange 500 (High Warning)
            "#F59E0B", // Amber 500 (Warning)
            "#EAB308", // Yellow 500 (Attention)
            "#84CC16", // Lime 500
            "#10B981", // Emerald 500 (Success / Normal)

            "#14B8A6", // Teal 500
            "#06B6D4", // Cyan 500
            "#0EA5E9", // Sky 500
            "#3B82F6", // Blue 500 (Primary / Link)
            "#6366F1", // Indigo 500
            "#8B5CF6", // Violet 500

            "#A855F7", // Purple 500
            "#D946EF", // Fuchsia 500
            "#EC4899", // Pink 500
            "#F43F5E", // Rose 500
            "#475569", // Slate 600
            "#1E293B"  // Slate 800
        };

        /// <summary>
        /// ISA-101 and industrial SCADA compliant alarm and status palette.
        /// </summary>
        public static IReadOnlyList<string> IndustrialStatusPalette { get; } = new[]
        {
            "#10B981", // Running / Normal (Green)
            "#3B82F6", // Standby / Manual (Blue)
            "#F59E0B", // Low Priority Warning (Amber)
            "#F97316", // Medium Priority Warning (Orange)
            "#EF4444", // High Priority Critical Alarm (Red)
            "#6B7280", // Offline / Unpowered (Gray)
            "#8B5CF6", // Maintenance / Overridden (Purple)
            "#06B6D4"  // Calibrating / Testing (Cyan)
        };
    }
}
