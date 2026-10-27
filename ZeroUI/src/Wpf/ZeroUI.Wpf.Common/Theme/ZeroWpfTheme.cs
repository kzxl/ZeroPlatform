using System;
using System.Windows.Media;
using ZeroUI.Core.Theme;

namespace ZeroUI.Wpf.Theme
{
    /// <summary>
    /// Design token management and frozen brushes according to AgentOption WPF Standards.
    /// Provides zero-overhead GDI/WPF color management for ZeroUI controls.
    /// </summary>
    public static class ZeroWpfTheme
    {
        public static bool IsDark { get; private set; } = true;

        public static event Action? ThemeChanged;

        // Frozen Brushes
        public static SolidColorBrush BgPrimary { get; private set; } = null!;
        public static SolidColorBrush BgCard { get; private set; } = null!;
        public static SolidColorBrush BgInput { get; private set; } = null!;
        public static SolidColorBrush BgHover { get; private set; } = null!;
        public static SolidColorBrush BgActive { get; private set; } = null!;
        public static SolidColorBrush BgDisabled { get; private set; } = null!;
        public static SolidColorBrush BorderDefault { get; private set; } = null!;
        public static SolidColorBrush BorderSubtle { get; private set; } = null!;
        public static SolidColorBrush BorderFocus { get; private set; } = null!;
        public static SolidColorBrush PrimaryAccent { get; private set; } = null!;
        public static SolidColorBrush PrimaryAccentDark { get; private set; } = null!;
        public static SolidColorBrush PrimaryAccentHover => PrimaryAccentDark;
        public static SolidColorBrush SecondaryAccent { get; private set; } = null!;
        public static SolidColorBrush TextPrimary { get; private set; } = null!;
        public static SolidColorBrush TextSecondary { get; private set; } = null!;
        public static SolidColorBrush TextMuted { get; private set; } = null!;
        public static SolidColorBrush SuccessAccent { get; private set; } = null!;
        public static SolidColorBrush DangerAccent { get; private set; } = null!;
        public static SolidColorBrush WarningAccent { get; private set; } = null!;
        public static SolidColorBrush InfoAccent { get; private set; } = null!;
        public static SolidColorBrush SelectionBackground { get; private set; } = null!;
        public static SolidColorBrush SelectionForeground { get; private set; } = null!;
        public static SolidColorBrush ScrollThumb { get; private set; } = null!;
        public static SolidColorBrush ScrollThumbHover { get; private set; } = null!;
        public static Brush ScrollTrack { get; private set; } = null!;

        // Frozen Pens
        public static Pen GridLinePen { get; private set; } = null!;
        public static Pen BorderPen { get; private set; } = null!;
        public static Pen AccentPen { get; private set; } = null!;
        public static Pen SelectionPen { get; private set; } = null!;

        // Common Typefaces
        public static Typeface RegularTypeface { get; } = new Typeface(new FontFamily("Segoe UI"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Normal, System.Windows.FontStretches.Normal);
        public static Typeface MediumTypeface { get; } = new Typeface(new FontFamily("Segoe UI"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.Medium, System.Windows.FontStretches.Normal);
        public static Typeface BoldTypeface { get; } = new Typeface(new FontFamily("Segoe UI"), System.Windows.FontStyles.Normal, System.Windows.FontWeights.SemiBold, System.Windows.FontStretches.Normal);

        static ZeroWpfTheme()
        {
            ApplyPalette(ZeroUI.Core.Theme.ZeroSkinDefaults.CleanLight.Tokens, false);

            // Automatically synchronize with central ZeroSkinManager
            ZeroUI.Core.Theme.ZeroSkinManager.SkinChanged += skin =>
            {
                ApplyPalette(skin.Tokens, skin.IsDark);
            };
        }

        public static void SetTheme(bool isDark)
        {
            if (IsDark == isDark) return;
            var tokens = isDark ? ZeroUI.Core.Theme.ZeroSkinDefaults.ObsidianDark.Tokens : ZeroUI.Core.Theme.ZeroSkinDefaults.CleanLight.Tokens;
            ApplyPalette(tokens, isDark);
        }

        public static void ApplyPalette(ZeroUI.Core.Theme.ZeroPaletteTokens tokens, bool isDark)
        {
            IsDark = isDark;

            BgPrimary = CreateFrozen(tokens.BgPrimary);
            BgCard = CreateFrozen(tokens.BgCard);
            BgInput = CreateFrozen(tokens.BgInput);
            BgHover = CreateFrozen(tokens.BgHover);
            BgActive = CreateFrozen(tokens.BgActive);
            BgDisabled = CreateFrozen(tokens.BgDisabled);

            BorderDefault = CreateFrozen(tokens.BorderDefault);
            BorderSubtle = CreateFrozen(tokens.BorderSubtle);
            BorderFocus = CreateFrozen(string.IsNullOrEmpty(tokens.BorderFocus) ? tokens.PrimaryAccent : tokens.BorderFocus);

            string primaryHex = tokens.PrimaryAccent;
            if (ZeroColorUtils.GetContrastRatio(primaryHex, tokens.BgPrimary) < 4.5 
                && !string.IsNullOrEmpty(tokens.SecondaryAccent) 
                && ZeroColorUtils.GetContrastRatio(tokens.SecondaryAccent, tokens.BgPrimary) >= 4.5)
            {
                primaryHex = tokens.SecondaryAccent;
            }
            PrimaryAccent = CreateFrozen(primaryHex);
            PrimaryAccentDark = CreateFrozen(tokens.PrimaryAccentDark);
            SecondaryAccent = CreateFrozen(tokens.SecondaryAccent);

            TextPrimary = CreateFrozen(tokens.TextPrimary);
            TextSecondary = CreateFrozen(tokens.TextSecondary);
            TextMuted = CreateFrozen(tokens.TextMuted);

            SuccessAccent = CreateFrozen(tokens.Success);
            WarningAccent = CreateFrozen(tokens.Warning);
            DangerAccent = CreateFrozen(tokens.Danger);
            InfoAccent = CreateFrozen(string.IsNullOrEmpty(tokens.Info) ? tokens.PrimaryAccent : tokens.Info);

            SelectionBackground = CreateFrozen(tokens.SelectionBackground);
            SelectionForeground = CreateFrozen(tokens.SelectionForeground);

            ScrollThumb = CreateFrozen(tokens.ScrollThumb);
            ScrollThumbHover = CreateFrozen(tokens.ScrollThumbHover);
            ScrollTrack = tokens.ScrollTrack.Equals("Transparent", StringComparison.OrdinalIgnoreCase)
                ? Brushes.Transparent
                : CreateFrozen(tokens.ScrollTrack);

            GridLinePen = CreateFrozenPen(BorderSubtle, 1.0);
            BorderPen = CreateFrozenPen(BorderDefault, 1.0);
            AccentPen = CreateFrozenPen(PrimaryAccent, 1.5);
            SelectionPen = CreateFrozenPen(PrimaryAccent, 1.0);

            UpdateApplicationResources();
            if (ThemeChanged != null)
            {
                foreach (Action handler in ThemeChanged.GetInvocationList())
                {
                    try
                    {
                        if (handler.Target is System.Windows.Threading.DispatcherObject dispObj)
                        {
                            if (!dispObj.Dispatcher.HasShutdownStarted && !dispObj.Dispatcher.HasShutdownFinished)
                            {
                                if (dispObj.Dispatcher.CheckAccess())
                                {
                                    handler();
                                }
                                else
                                {
                                    dispObj.Dispatcher.BeginInvoke(handler);
                                }
                            }
                        }
                        else
                        {
                            handler();
                        }
                    }
                    catch
                    {
                        // Ignore exceptions from obsolete/disposed subscribers
                    }
                }
            }
        }

        private static SolidColorBrush CreateFrozen(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private static Pen CreateFrozenPen(Brush brush, double thickness = 1.0)
        {
            var pen = new Pen(brush, thickness);
            pen.Freeze();
            return pen;
        }

        public static void UpdateApplicationResources()
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app != null)
                {
                    var res = app.Resources;
                    res["ZeroUI.BgPrimary"] = BgPrimary;
                    res["ZeroUI.BgCard"] = BgCard;
                    res["ZeroUI.BgInput"] = BgInput;
                    res["ZeroUI.BgHover"] = BgHover;
                    res["ZeroUI.BgActive"] = BgActive;
                    res["ZeroUI.BgDisabled"] = BgDisabled;
                    res["ZeroUI.BorderDefault"] = BorderDefault;
                    res["ZeroUI.BorderSubtle"] = BorderSubtle;
                    res["ZeroUI.BorderFocus"] = BorderFocus;
                    res["ZeroUI.PrimaryAccent"] = PrimaryAccent;
                    res["ZeroUI.PrimaryAccentDark"] = PrimaryAccentDark;
                    res["ZeroUI.SecondaryAccent"] = SecondaryAccent;
                    res["ZeroUI.TextPrimary"] = TextPrimary;
                    res["ZeroUI.TextSecondary"] = TextSecondary;
                    res["ZeroUI.TextMuted"] = TextMuted;
                    res["ZeroUI.SuccessAccent"] = SuccessAccent;
                    res["ZeroUI.DangerAccent"] = DangerAccent;
                    res["ZeroUI.WarningAccent"] = WarningAccent;
                    res["ZeroUI.Success"] = SuccessAccent;
                    res["ZeroUI.Danger"] = DangerAccent;
                    res["ZeroUI.Warning"] = WarningAccent;
                    res["ZeroUI.InfoAccent"] = InfoAccent;
                    res["ZeroUI.SelectionBackground"] = SelectionBackground;
                    res["ZeroUI.SelectionForeground"] = SelectionForeground;
                    res["ZeroUI.ScrollThumb"] = ScrollThumb;
                    res["ZeroUI.ScrollThumbHover"] = ScrollThumbHover;
                    res["ZeroUI.ScrollTrack"] = ScrollTrack;
                }
            }
            catch
            {
                // Standalone or non-WPF host fallback
            }
        }
    }
}
