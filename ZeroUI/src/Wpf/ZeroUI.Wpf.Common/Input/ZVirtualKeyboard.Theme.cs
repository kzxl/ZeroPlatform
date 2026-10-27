// <auto-refactored> Partial class file - Theme
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Input;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Base;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Input
{
    public partial class ZVirtualKeyboard
    {
        private void UpdateKeyTheme(Button btn)
        {
            if (btn.Tag is VirtualKeyType type)
            {
                if (type == VirtualKeyType.Enter)
                {
                    btn.Background = ZeroWpfTheme.PrimaryAccent ?? Brushes.DodgerBlue;
                    btn.Foreground = GetAccentTextBrush();
                    btn.BorderBrush = ZeroWpfTheme.BorderDefault ?? Brushes.Gray;
                }
                else if (type == VirtualKeyType.Character || type == VirtualKeyType.Space)
                {
                    btn.Background = ZeroWpfTheme.BgInput ?? Brushes.DarkSlateGray;
                    btn.Foreground = ZeroWpfTheme.TextPrimary ?? Brushes.White;
                    btn.BorderBrush = ZeroWpfTheme.BorderDefault ?? Brushes.Gray;
                }
                else
                {
                    btn.Background = ZeroWpfTheme.BgCard ?? Brushes.DarkSlateBlue;
                    btn.Foreground = ZeroWpfTheme.TextPrimary ?? Brushes.White;
                    btn.BorderBrush = ZeroWpfTheme.BorderDefault ?? Brushes.Gray;
                }
            }
        }

        public void ApplyTheme()
        {
            if (!Dispatcher.CheckAccess())
            {
                try
                {
                    Dispatcher.BeginInvoke(new Action(ApplyTheme));
                }
                catch (Exception) { }
                return;
            }

            Background = ZeroWpfTheme.BgCard ?? Brushes.Transparent;

            foreach (UIElement child in _mainGrid.Children)
            {
                if (child is Button b)
                {
                    UpdateKeyTheme(b);
                }
                else if (child is Grid rowGrid)
                {
                    foreach (UIElement rowChild in rowGrid.Children)
                    {
                        if (rowChild is Button rb)
                        {
                            UpdateKeyTheme(rb);
                        }
                    }
                }
            }
        }

        private static SolidColorBrush GetAccentTextBrush()
        {
            if (ZeroWpfTheme.PrimaryAccent == null) return Brushes.White;
            string hexAccent = ColorToHex(ZeroWpfTheme.PrimaryAccent.Color);

            if (ZeroWpfTheme.SelectionForeground != null)
            {
                string selHex = ColorToHex(ZeroWpfTheme.SelectionForeground.Color);
                if (ZeroColorUtils.GetContrastRatio(selHex, hexAccent) >= 4.5)
                    return ZeroWpfTheme.SelectionForeground;
            }

            string primaryTextHex = ZeroWpfTheme.TextPrimary != null ? ColorToHex(ZeroWpfTheme.TextPrimary.Color) : "#FFFFFF";
            string bgPrimaryHex = ZeroWpfTheme.BgPrimary != null ? ColorToHex(ZeroWpfTheme.BgPrimary.Color) : "#000000";

            double textContrast = ZeroColorUtils.GetContrastRatio(primaryTextHex, hexAccent);
            double bgContrast = ZeroColorUtils.GetContrastRatio(bgPrimaryHex, hexAccent);

            if (bgContrast >= 4.5 && bgContrast >= textContrast && ZeroWpfTheme.BgPrimary != null)
                return ZeroWpfTheme.BgPrimary;

            if (textContrast >= 4.5 && ZeroWpfTheme.TextPrimary != null)
                return ZeroWpfTheme.TextPrimary;

            return (bgContrast >= textContrast && ZeroWpfTheme.BgPrimary != null) ? ZeroWpfTheme.BgPrimary : (ZeroWpfTheme.TextPrimary ?? Brushes.White);
        }

        private static string ColorToHex(Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }
    }
}
