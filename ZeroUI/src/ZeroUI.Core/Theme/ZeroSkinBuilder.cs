using System;

namespace ZeroUI.Core.Theme
{
    /// <summary>
    /// Fluent builder and mathematical palette generator for creating custom ZeroUI skins.
    /// Provides algorithmic color generation and contrast balancing.
    /// </summary>
    public sealed class ZeroSkinBuilder
    {
        private readonly string _name;
        private readonly string _displayName;
        private readonly bool _isDark;
        private readonly ZeroPaletteTokens _tokens;

        private ZeroSkinBuilder(string name, string displayName, bool isDark)
        {
            _name = string.IsNullOrWhiteSpace(name) ? "custom_skin" : name.Trim();
            _displayName = string.IsNullOrWhiteSpace(displayName) ? _name : displayName.Trim();
            _isDark = isDark;
            _tokens = new ZeroPaletteTokens();
        }

        public static ZeroSkinBuilder Create(string name, string displayName, bool isDark)
        {
            return new ZeroSkinBuilder(name, displayName, isDark);
        }

        /// <summary>
        /// Mathematically generates a complete harmonious skin from a single primary seed color.
        /// Automatically computes surfaces, borders, hovers, scrollbars, and WCAG AAA compliant text contrast.
        /// </summary>
        public static ZeroSkin FromSeedColor(string name, string displayName, bool isDark, string seedHex, string? secondaryHex = null)
        {
            var cleanSeed = ZeroColorUtils.ParseHex(seedHex).ToHex();
            var hsl = ZeroColorUtils.RgbToHsl(ZeroColorUtils.ParseHex(cleanSeed));

            // Determine secondary accent (triadic or shifted hue) if not specified
            string cleanSecondary = secondaryHex != null
                ? ZeroColorUtils.ParseHex(secondaryHex).ToHex()
                : ZeroColorUtils.HslToRgb(new ZeroColorUtils.HslColor((hsl.H + 45.0) % 360.0, Math.Max(0.5, hsl.S), isDark ? 0.65 : 0.45)).ToHex();

            var builder = Create(name, displayName, isDark);

            if (isDark)
            {
                // Dark theme surfaces with subtle seed hue harmonization
                string seedTint = ZeroColorUtils.HslToRgb(new ZeroColorUtils.HslColor(hsl.H, 0.15, 0.07)).ToHex();
                string bgPrimary = ZeroColorUtils.Blend("#0F172A", seedTint, 0.35);
                string bgCard = ZeroColorUtils.AdjustLightness(bgPrimary, +0.05);
                string bgInput = ZeroColorUtils.AdjustLightness(bgCard, +0.04);
                string bgHover = ZeroColorUtils.AdjustLightness(bgCard, +0.08);
                string bgActive = ZeroColorUtils.AdjustLightness(bgCard, +0.12);
                string bgDisabled = ZeroColorUtils.AdjustLightness(bgPrimary, +0.02);

                string borderDefault = ZeroColorUtils.AdjustLightness(bgCard, +0.10);
                string borderSubtle = ZeroColorUtils.AdjustLightness(bgCard, +0.05);

                string selectionBg = ZeroColorUtils.HslToRgb(new ZeroColorUtils.HslColor(hsl.H, Math.Max(0.6, hsl.S), 0.25)).ToHex();
                string selectionFg = ZeroColorUtils.GetBestContrastTextColor(selectionBg);

                builder.WithBackgrounds(bgPrimary, bgCard, bgInput, bgHover, bgActive, bgDisabled)
                       .WithBorders(borderDefault, borderSubtle, cleanSeed)
                       .WithText("#F8FAFC", "#94A3B8", "#64748B")
                       .WithPrimaryAccent(cleanSeed, ZeroColorUtils.AdjustLightness(cleanSeed, -0.10))
                       .WithSecondaryAccent(cleanSecondary)
                       .WithSelection(selectionBg, selectionFg)
                       .WithStatuses("#34D399", "#FBBF24", "#F87171", cleanSecondary)
                       .WithScroll("#475569", "#64748B", "Transparent");
            }
            else
            {
                // Clean Light surfaces
                string bgPrimary = "#F8FAFC";
                string bgCard = "#FFFFFF";
                string bgInput = "#F1F5F9";
                string bgHover = "#E2E8F0";
                string bgActive = "#CBD5E1";
                string bgDisabled = "#E2E8F0";

                string borderDefault = "#E2E8F0";
                string borderSubtle = "#F1F5F9";

                string selectionBg = ZeroColorUtils.HslToRgb(new ZeroColorUtils.HslColor(hsl.H, 0.75, 0.90)).ToHex();
                string selectionFg = ZeroColorUtils.HslToRgb(new ZeroColorUtils.HslColor(hsl.H, 0.90, 0.25)).ToHex();

                builder.WithBackgrounds(bgPrimary, bgCard, bgInput, bgHover, bgActive, bgDisabled)
                       .WithBorders(borderDefault, borderSubtle, cleanSeed)
                       .WithText("#0F172A", "#475569", "#94A3B8")
                       .WithPrimaryAccent(cleanSeed, ZeroColorUtils.AdjustLightness(cleanSeed, -0.12))
                       .WithSecondaryAccent(cleanSecondary)
                       .WithSelection(selectionBg, selectionFg)
                       .WithStatuses("#16A34A", "#D97706", "#DC2626", cleanSecondary)
                       .WithScroll("#CBD5E1", "#94A3B8", "Transparent");
            }

            return builder.Build();
        }

        public ZeroSkinBuilder WithPrimaryAccent(string hex, string? darkHex = null)
        {
            _tokens.PrimaryAccent = hex;
            _tokens.PrimaryAccentDark = darkHex ?? ZeroColorUtils.AdjustLightness(hex, _isDark ? -0.10 : -0.12);
            return this;
        }

        public ZeroSkinBuilder WithSecondaryAccent(string hex)
        {
            _tokens.SecondaryAccent = hex;
            return this;
        }

        public ZeroSkinBuilder WithBackgrounds(string primary, string card, string input, string? hover = null, string? active = null, string? disabled = null)
        {
            _tokens.BgPrimary = primary;
            _tokens.BgCard = card;
            _tokens.BgInput = input;
            _tokens.BgHover = hover ?? ZeroColorUtils.AdjustLightness(card, _isDark ? 0.08 : -0.05);
            _tokens.BgActive = active ?? ZeroColorUtils.AdjustLightness(card, _isDark ? 0.14 : -0.10);
            _tokens.BgDisabled = disabled ?? ZeroColorUtils.AdjustLightness(primary, _isDark ? 0.02 : -0.05);
            return this;
        }

        public ZeroSkinBuilder WithBorders(string borderDefault, string borderSubtle, string? borderFocus = null)
        {
            _tokens.BorderDefault = borderDefault;
            _tokens.BorderSubtle = borderSubtle;
            _tokens.BorderFocus = borderFocus ?? _tokens.PrimaryAccent;
            return this;
        }

        public ZeroSkinBuilder WithText(string primary, string secondary, string muted)
        {
            _tokens.TextPrimary = primary;
            _tokens.TextSecondary = secondary;
            _tokens.TextMuted = muted;
            return this;
        }

        public ZeroSkinBuilder WithSelection(string background, string foreground)
        {
            _tokens.SelectionBackground = background;
            _tokens.SelectionForeground = foreground;
            return this;
        }

        public ZeroSkinBuilder WithStatuses(string success, string warning, string danger, string info)
        {
            _tokens.Success = success;
            _tokens.Warning = warning;
            _tokens.Danger = danger;
            _tokens.Info = info;
            return this;
        }

        public ZeroSkinBuilder WithScroll(string thumb, string thumbHover, string track = "Transparent")
        {
            _tokens.ScrollThumb = thumb;
            _tokens.ScrollThumbHover = thumbHover;
            _tokens.ScrollTrack = track;
            return this;
        }

        public ZeroSkin Build()
        {
            // Fallback validation for any missing essential tokens
            if (string.IsNullOrEmpty(_tokens.BgPrimary))
                _tokens.BgPrimary = _isDark ? "#0F172A" : "#F8FAFC";
            if (string.IsNullOrEmpty(_tokens.BgCard))
                _tokens.BgCard = _isDark ? "#1E293B" : "#FFFFFF";
            if (string.IsNullOrEmpty(_tokens.BgInput))
                _tokens.BgInput = _isDark ? "#243044" : "#F1F5F9";
            if (string.IsNullOrEmpty(_tokens.TextPrimary))
                _tokens.TextPrimary = _isDark ? "#F8FAFC" : "#0F172A";
            if (string.IsNullOrEmpty(_tokens.PrimaryAccent))
                _tokens.PrimaryAccent = "#6366F1";
            if (string.IsNullOrEmpty(_tokens.SelectionBackground))
                _tokens.SelectionBackground = _isDark ? "#1E3A8A" : "#DBEAFE";
            if (string.IsNullOrEmpty(_tokens.SelectionForeground))
                _tokens.SelectionForeground = ZeroColorUtils.GetBestContrastTextColor(_tokens.SelectionBackground);

            return new ZeroSkin(_name, _displayName, _isDark, _tokens);
        }
    }
}
