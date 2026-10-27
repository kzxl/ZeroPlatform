using System;
using System.Drawing;
using ZeroUI.Core.Theme;

namespace ZeroUI.WinForms.Theme
{
    public enum ZeroThemeMode
    {
        Light,
        Dark
    }

    public class ZeroThemePalette
    {
        public Color Background { get; set; }
        public Color Surface { get; set; }
        public Color CardBackground { get; set; }
        public Color TextPrimary { get; set; }
        public Color TextSecondary { get; set; }
        public Color Border { get; set; }
        public Color Primary { get; set; }
        public Color PrimaryHover { get; set; }
        public Color Success { get; set; }
        public Color Warning { get; set; }
        public Color Danger { get; set; }
        public Color Info { get; set; }
        public Color Hover { get; set; }
        public Color HeaderBackground { get; set; }

        // Token aliases aligned with Core/WPF design token names
        public Color PrimaryAccent => Primary;
        public Color PrimaryAccentDark => PrimaryHover;
        public Color BorderDefault => Border;
        public Color BorderSubtle => Border;
        public Color BorderFocused => Primary;
        public Color BgCard => CardBackground;
        public Color BgInput => Surface;
        public Color BgPrimary => Background;
        public Color TextMuted => TextSecondary;
    }

    /// <summary>
    /// Unified Design Token & Theme Engine for ZeroUI WinForms.
    /// Synchronized with ZeroUI.Core.Theme.ZeroSkinManager for cross-platform skinning.
    /// </summary>
    public static class ZeroTheme
    {
        private static ZeroThemeMode _currentMode = ZeroThemeMode.Light;
        private static ZeroThemePalette _currentPalette;

        public static event EventHandler? ThemeChanged;

        public static readonly ZeroThemePalette Light = new ZeroThemePalette
        {
            Background = Color.FromArgb(248, 249, 252),
            Surface = Color.FromArgb(255, 255, 255),
            CardBackground = Color.FromArgb(255, 255, 255),
            TextPrimary = Color.FromArgb(15, 23, 42),
            TextSecondary = Color.FromArgb(71, 85, 105),
            Border = Color.FromArgb(220, 225, 238),
            Primary = Color.FromArgb(79, 70, 229),
            PrimaryHover = Color.FromArgb(67, 56, 202),
            Success = Color.FromArgb(22, 163, 74),
            Warning = Color.FromArgb(217, 119, 6),
            Danger = Color.FromArgb(220, 38, 38),
            Info = Color.FromArgb(2, 132, 199),
            Hover = Color.FromArgb(232, 237, 248),
            HeaderBackground = Color.FromArgb(241, 243, 249)
        };

        public static readonly ZeroThemePalette Dark = new ZeroThemePalette
        {
            Background = Color.FromArgb(17, 19, 31),
            Surface = Color.FromArgb(24, 26, 40),
            CardBackground = Color.FromArgb(24, 26, 40),
            TextPrimary = Color.FromArgb(241, 245, 249),
            TextSecondary = Color.FromArgb(148, 163, 184),
            Border = Color.FromArgb(46, 52, 78),
            Primary = Color.FromArgb(129, 140, 248),
            PrimaryHover = Color.FromArgb(99, 102, 241),
            Success = Color.FromArgb(166, 227, 161),
            Warning = Color.FromArgb(249, 226, 175),
            Danger = Color.FromArgb(243, 139, 168),
            Info = Color.FromArgb(137, 220, 235),
            Hover = Color.FromArgb(38, 42, 68),
            HeaderBackground = Color.FromArgb(29, 32, 52)
        };

        static ZeroTheme()
        {
            _currentPalette = Light;

            // Automatically synchronize with central ZeroSkinManager
            ZeroSkinManager.SkinChanged += skin =>
            {
                ApplyPalette(skin.Tokens, skin.IsDark);
            };
        }

        public static ZeroThemeMode CurrentMode
        {
            get => _currentMode;
            set
            {
                if (_currentMode != value)
                {
                    _currentMode = value;
                    _currentPalette = _currentMode == ZeroThemeMode.Dark ? Dark : Light;
                    ThemeChanged?.Invoke(null, EventArgs.Empty);
                }
            }
        }

        public static ZeroThemePalette Colors => _currentPalette;
        public static ZeroThemePalette Palette => _currentPalette;

        public static bool IsDark => _currentMode == ZeroThemeMode.Dark;
        public static ZeroSkin CurrentSkin => ZeroSkinManager.CurrentSkin;

        public static void ApplySkin(string skinName) => ZeroSkinManager.ApplySkin(skinName);
        public static void ApplySkin(ZeroSkin skin) => ZeroSkinManager.ApplySkin(skin);

        public static void ApplyPalette(ZeroPaletteTokens tokens, bool isDark)
        {
            if (tokens == null) throw new ArgumentNullException(nameof(tokens));
            _currentMode = isDark ? ZeroThemeMode.Dark : ZeroThemeMode.Light;
            _currentPalette = CreatePaletteFromTokens(tokens, isDark);
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }

        public static ZeroThemePalette CreatePaletteFromTokens(ZeroPaletteTokens tokens, bool isDark)
        {
            if (tokens == null) throw new ArgumentNullException(nameof(tokens));
            return new ZeroThemePalette
            {
                Background = SafeColor(tokens.BgPrimary, isDark ? Dark.Background : Light.Background),
                Surface = SafeColor(tokens.BgCard, isDark ? Dark.Surface : Light.Surface),
                CardBackground = SafeColor(tokens.BgCard, isDark ? Dark.CardBackground : Light.CardBackground),
                TextPrimary = SafeColor(tokens.TextPrimary, isDark ? Dark.TextPrimary : Light.TextPrimary),
                TextSecondary = SafeColor(tokens.TextSecondary, isDark ? Dark.TextSecondary : Light.TextSecondary),
                Border = SafeColor(tokens.BorderDefault, isDark ? Dark.Border : Light.Border),
                Primary = SafeColor(tokens.PrimaryAccent, isDark ? Dark.Primary : Light.Primary),
                PrimaryHover = SafeColor(tokens.PrimaryAccentDark, isDark ? Dark.PrimaryHover : Light.PrimaryHover),
                Success = SafeColor(tokens.Success, isDark ? Dark.Success : Light.Success),
                Warning = SafeColor(tokens.Warning, isDark ? Dark.Warning : Light.Warning),
                Danger = SafeColor(tokens.Danger, isDark ? Dark.Danger : Light.Danger),
                Info = SafeColor(tokens.Info, isDark ? Dark.Info : Light.Info),
                Hover = SafeColor(tokens.BgHover, isDark ? Dark.Hover : Light.Hover),
                HeaderBackground = SafeColor(tokens.BgInput, isDark ? Dark.HeaderBackground : Light.HeaderBackground)
            };
        }

        public static ZeroThemePalette CreatePaletteFromSkin(ZeroSkin skin)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));
            return CreatePaletteFromTokens(skin.Tokens, skin.IsDark);
        }

        private static Color SafeColor(string hex, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(hex)) return fallback;
            try
            {
                var rgb = ZeroColorUtils.ParseHex(hex);
                return Color.FromArgb(rgb.R, rgb.G, rgb.B);
            }
            catch
            {
                return fallback;
            }
        }

        public static void ToggleTheme()
        {
            if (IsDark)
            {
                ApplySkin("clean_light");
            }
            else
            {
                ApplySkin("obsidian_dark");
            }
        }
    }
}
