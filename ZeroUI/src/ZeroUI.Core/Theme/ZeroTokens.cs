using System;

namespace ZeroUI.Core.Theme
{
    /// <summary>
    /// Design token palette definition containing hex color codes.
    /// Single Source of Truth for ZeroUI cross-platform theme styling (WPF, WinForms).
    /// </summary>
    public sealed class ZeroPaletteTokens
    {
        // Surface & Backgrounds
        public string BgPrimary { get; set; } = string.Empty;
        public string BgCard { get; set; } = string.Empty;
        public string BgInput { get; set; } = string.Empty;
        public string BgHover { get; set; } = string.Empty;
        public string BgActive { get; set; } = string.Empty;
        public string BgDisabled { get; set; } = string.Empty;

        // Borders & Dividers
        public string BorderDefault { get; set; } = string.Empty;
        public string BorderSubtle { get; set; } = string.Empty;
        public string BorderFocus { get; set; } = string.Empty;

        // Typography & Text
        public string TextPrimary { get; set; } = string.Empty;
        public string TextSecondary { get; set; } = string.Empty;
        public string TextMuted { get; set; } = string.Empty;

        // Brand & Accents
        public string PrimaryAccent { get; set; } = string.Empty;
        public string PrimaryAccentDark { get; set; } = string.Empty;
        public string SecondaryAccent { get; set; } = string.Empty;

        // Feedback & Semantic Statuses
        public string Success { get; set; } = string.Empty;
        public string Warning { get; set; } = string.Empty;
        public string Danger { get; set; } = string.Empty;
        public string Info { get; set; } = string.Empty;

        // High-Contrast Selection (WCAG AAA >8:1)
        public string SelectionBackground { get; set; } = string.Empty;
        public string SelectionForeground { get; set; } = string.Empty;

        // ScrollBar Tokens
        public string ScrollThumb { get; set; } = string.Empty;
        public string ScrollThumbHover { get; set; } = string.Empty;
        public string ScrollTrack { get; set; } = string.Empty;
    }

    /// <summary>
    /// Master token definitions for Dark and Light modes across all ZeroUI components.
    /// </summary>
    public static class ZeroTokens
    {
        public static readonly ZeroPaletteTokens Dark = new ZeroPaletteTokens
        {
            // Obsidian Dark surfaces
            BgPrimary = "#11131F",
            BgCard = "#181A28",
            BgInput = "#1D2034",
            BgHover = "#262A44",
            BgActive = "#32385C",
            BgDisabled = "#151824",

            // Borders
            BorderDefault = "#2E344E",
            BorderSubtle = "#23273C",
            BorderFocus = "#818CF8",

            // Text
            TextPrimary = "#F1F5F9",
            TextSecondary = "#94A3B8",
            TextMuted = "#64748B",

            // Accents
            PrimaryAccent = "#818CF8",
            PrimaryAccentDark = "#6366F1",
            SecondaryAccent = "#A78BFA",

            // Statuses
            Success = "#A6E3A1",
            Warning = "#F9E2AF",
            Danger = "#F38BA8",
            Info = "#89DCEB",

            // High-Contrast Selection (WCAG AAA)
            SelectionBackground = "#1E3A8A", // Royal Blue
            SelectionForeground = "#FFFFFF", // Crisp White

            // Slim ScrollBar
            ScrollThumb = "#334155",
            ScrollThumbHover = "#64748B",
            ScrollTrack = "Transparent"
        };

        public static readonly ZeroPaletteTokens Light = new ZeroPaletteTokens
        {
            // Clean Slate surfaces
            BgPrimary = "#F8F9FC",
            BgCard = "#FFFFFF",
            BgInput = "#F1F3F9",
            BgHover = "#E8EDF8",
            BgActive = "#D9E2F5",
            BgDisabled = "#E2E8F0",

            // Borders
            BorderDefault = "#DCE1EE",
            BorderSubtle = "#EAEFF8",
            BorderFocus = "#4F46E5",

            // Text
            TextPrimary = "#0F172A",
            TextSecondary = "#475569",
            TextMuted = "#94A3B8",

            // Accents
            PrimaryAccent = "#4F46E5",
            PrimaryAccentDark = "#4338CA",
            SecondaryAccent = "#7C3AED",

            // Statuses
            Success = "#16A34A",
            Warning = "#D97706",
            Danger = "#DC2626",
            Info = "#0284C7",

            // High-Contrast Selection (WCAG AAA)
            SelectionBackground = "#DBEAFE", // Soft Blue
            SelectionForeground = "#1E3A8A", // Deep Navy

            // Slim ScrollBar
            ScrollThumb = "#CBD5E1",
            ScrollThumbHover = "#94A3B8",
            ScrollTrack = "Transparent"
        };
    }
}
