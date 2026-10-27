using System;
using System.Windows;
using System.Windows.Media;
using ZeroUI.Core.Theme;

namespace ZeroUI.Wpf.Theme
{
    /// <summary>
    /// Attached properties enabling container-level skin scoping in ZeroUI WPF.
    /// Allows embedding dark SCADA / monitoring views inside light windows without cross-contamination.
    /// </summary>
    public static class ZeroSkinProps
    {
        public static readonly DependencyProperty SkinNameProperty =
            DependencyProperty.RegisterAttached(
                "SkinName",
                typeof(string),
                typeof(ZeroSkinProps),
                new PropertyMetadata(null, OnSkinNameChanged));

        public static readonly DependencyProperty CustomSkinProperty =
            DependencyProperty.RegisterAttached(
                "CustomSkin",
                typeof(ZeroSkin),
                typeof(ZeroSkinProps),
                new PropertyMetadata(null, OnCustomSkinChanged));

        public static string? GetSkinName(DependencyObject obj) => (string?)obj.GetValue(SkinNameProperty);
        public static void SetSkinName(DependencyObject obj, string? value) => obj.SetValue(SkinNameProperty, value);

        public static ZeroSkin? GetCustomSkin(DependencyObject obj) => (ZeroSkin?)obj.GetValue(CustomSkinProperty);
        public static void SetCustomSkin(DependencyObject obj, ZeroSkin? value) => obj.SetValue(CustomSkinProperty, value);

        private static void OnSkinNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && e.NewValue is string skinName && !string.IsNullOrWhiteSpace(skinName))
            {
                var skin = ZeroSkinManager.GetSkin(skinName);
                if (skin != null)
                {
                    ApplyScopedSkinDictionary(element, skin);
                }
            }
        }

        private static void OnCustomSkinChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element && e.NewValue is ZeroSkin skin)
            {
                ApplyScopedSkinDictionary(element, skin);
            }
        }

        /// <summary>
        /// Injects a scoped ResourceDictionary into the target element's resource dictionary tree.
        /// </summary>
        public static void ApplyScopedSkinDictionary(FrameworkElement element, ZeroSkin skin)
        {
            if (element == null || skin == null) return;

            var dict = new ResourceDictionary();
            var tokens = skin.Tokens;

            AddFrozenBrush(dict, "ZeroUI.BgPrimary", tokens.BgPrimary);
            AddFrozenBrush(dict, "ZeroUI.BgCard", tokens.BgCard);
            AddFrozenBrush(dict, "ZeroUI.BgInput", tokens.BgInput);
            AddFrozenBrush(dict, "ZeroUI.BgHover", tokens.BgHover);
            AddFrozenBrush(dict, "ZeroUI.BgActive", tokens.BgActive);
            AddFrozenBrush(dict, "ZeroUI.BgDisabled", tokens.BgDisabled);

            AddFrozenBrush(dict, "ZeroUI.BorderDefault", tokens.BorderDefault);
            AddFrozenBrush(dict, "ZeroUI.BorderSubtle", tokens.BorderSubtle);
            AddFrozenBrush(dict, "ZeroUI.BorderFocus", string.IsNullOrEmpty(tokens.BorderFocus) ? tokens.PrimaryAccent : tokens.BorderFocus);

            AddFrozenBrush(dict, "ZeroUI.PrimaryAccent", tokens.PrimaryAccent);
            AddFrozenBrush(dict, "ZeroUI.PrimaryAccentDark", tokens.PrimaryAccentDark);
            AddFrozenBrush(dict, "ZeroUI.SecondaryAccent", tokens.SecondaryAccent);

            AddFrozenBrush(dict, "ZeroUI.TextPrimary", tokens.TextPrimary);
            AddFrozenBrush(dict, "ZeroUI.TextSecondary", tokens.TextSecondary);
            AddFrozenBrush(dict, "ZeroUI.TextMuted", tokens.TextMuted);

            AddFrozenBrush(dict, "ZeroUI.SuccessAccent", tokens.Success);
            AddFrozenBrush(dict, "ZeroUI.WarningAccent", tokens.Warning);
            AddFrozenBrush(dict, "ZeroUI.DangerAccent", tokens.Danger);
            AddFrozenBrush(dict, "ZeroUI.Success", tokens.Success);
            AddFrozenBrush(dict, "ZeroUI.Warning", tokens.Warning);
            AddFrozenBrush(dict, "ZeroUI.Danger", tokens.Danger);
            AddFrozenBrush(dict, "ZeroUI.InfoAccent", string.IsNullOrEmpty(tokens.Info) ? tokens.PrimaryAccent : tokens.Info);

            AddFrozenBrush(dict, "ZeroUI.SelectionBackground", tokens.SelectionBackground);
            AddFrozenBrush(dict, "ZeroUI.SelectionForeground", tokens.SelectionForeground);

            // Replace or append to local element resources
            element.Resources.MergedDictionaries.Add(dict);
        }

        private static void AddFrozenBrush(ResourceDictionary dict, string key, string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                var brush = new SolidColorBrush(color);
                brush.Freeze();
                dict[key] = brush;
            }
            catch
            {
                // Fallback safe ignore
            }
        }
    }
}
