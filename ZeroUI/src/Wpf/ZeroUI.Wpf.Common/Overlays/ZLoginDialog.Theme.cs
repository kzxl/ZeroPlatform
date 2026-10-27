// <auto-refactored> Partial class file - Theme </auto-refactored>
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Input;
using ZeroUI.Core.Security;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Input;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Overlays
{
    public partial class ZLoginDialog
    {
        private void SwitchTab(int index)
        {
            _currentTabIndex = index;
            _panelPassword.Visibility = (index == 0) ? Visibility.Visible : Visibility.Collapsed;
            _panelPin.Visibility = (index == 1) ? Visibility.Visible : Visibility.Collapsed;
            _panelBadge.Visibility = (index == 2) ? Visibility.Visible : Visibility.Collapsed;

            UpdateTabButton(_tabPass, index == 0);
            UpdateTabButton(_tabPin, index == 1);
            UpdateTabButton(_tabBadge, index == 2);

            SetStatus(index == 2 ? "Waiting for badge swipe..." : "Ready", StatusState.Ready);
        }

        private static void UpdateTabButton(Button btn, bool isActive)
        {
            btn.FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal;
            btn.Background = isActive ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.BgInput;
            btn.Foreground = isActive ? GetAccentTextBrush() : ZeroWpfTheme.TextSecondary;
            btn.BorderBrush = ZeroWpfTheme.BorderDefault;
        }

        private static string ColorToHex(Color c) => string.Format("#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);

        public static Brush GetAccentTextBrush()
        {
            var accent = ZeroWpfTheme.PrimaryAccent.Color;
            string hexAccent = ColorToHex(accent);

            string bgHex = ColorToHex(ZeroWpfTheme.BgPrimary.Color);
            double bgContrast = ZeroColorUtils.GetContrastRatio(bgHex, hexAccent);

            string textHex = ColorToHex(ZeroWpfTheme.TextPrimary.Color);
            double textContrast = ZeroColorUtils.GetContrastRatio(textHex, hexAccent);

            if (bgContrast >= 4.5 && bgContrast >= textContrast)
            {
                return ZeroWpfTheme.BgPrimary;
            }

            if (textContrast >= 4.5)
            {
                return ZeroWpfTheme.TextPrimary;
            }

            if (ZeroWpfTheme.SelectionForeground != null)
            {
                string selHex = ColorToHex(ZeroWpfTheme.SelectionForeground.Color);
                double selContrast = ZeroColorUtils.GetContrastRatio(selHex, hexAccent);
                if (selContrast >= 4.5)
                {
                    return ZeroWpfTheme.SelectionForeground;
                }
            }

            if (ZeroWpfTheme.BgCard != null)
            {
                string cardHex = ColorToHex(ZeroWpfTheme.BgCard.Color);
                double cardContrast = ZeroColorUtils.GetContrastRatio(cardHex, hexAccent);
                if (cardContrast >= 4.5)
                {
                    return ZeroWpfTheme.BgCard;
                }
            }

            return bgContrast >= textContrast ? (Brush)ZeroWpfTheme.BgPrimary : ZeroWpfTheme.TextPrimary;
        }

        private void SetStatus(string text, StatusState state)
        {
            _lblStatus.Text = text;
            _currentStatusState = state;
            UpdateStatusColor();
        }

        private void UpdateStatusColor()
        {
            if (_lblStatus == null) return;
            _lblStatus.Foreground = _currentStatusState switch
            {
                StatusState.Authenticating => ZeroWpfTheme.PrimaryAccent,
                StatusState.Error => ZeroWpfTheme.DangerAccent,
                _ => ZeroWpfTheme.TextSecondary
            };
        }

        public void ApplyTheme()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(ApplyTheme));
                return;
            }

            Background = ZeroWpfTheme.BgPrimary;

            if (_rootBorder != null)
            {
                _rootBorder.BorderBrush = ZeroWpfTheme.BorderDefault;
            }

            if (_lblTitle != null)
            {
                _lblTitle.Foreground = ZeroWpfTheme.TextPrimary;
            }

            if (_lblSubtitle != null)
            {
                _lblSubtitle.Foreground = ZeroWpfTheme.TextSecondary;
            }

            if (_btnClose != null)
            {
                _btnClose.Background = Brushes.Transparent;
                _btnClose.Foreground = ZeroWpfTheme.TextSecondary;
            }

            if (_tabPass != null) UpdateTabButton(_tabPass, _currentTabIndex == 0);
            if (_tabPin != null) UpdateTabButton(_tabPin, _currentTabIndex == 1);
            if (_tabBadge != null) UpdateTabButton(_tabBadge, _currentTabIndex == 2);

            if (_lblUsername != null) _lblUsername.Foreground = ZeroWpfTheme.TextSecondary;
            if (_lblPassword != null) _lblPassword.Foreground = ZeroWpfTheme.TextSecondary;

            if (_txtUsername != null)
            {
                _txtUsername.Background = ZeroWpfTheme.BgInput;
                _txtUsername.Foreground = ZeroWpfTheme.TextPrimary;
                _txtUsername.BorderBrush = ZeroWpfTheme.BorderDefault;
                _txtUsername.CaretBrush = ZeroWpfTheme.TextPrimary;
            }

            if (_txtPassword != null)
            {
                _txtPassword.Background = ZeroWpfTheme.BgInput;
                _txtPassword.Foreground = ZeroWpfTheme.TextPrimary;
                _txtPassword.BorderBrush = ZeroWpfTheme.BorderDefault;
                _txtPassword.CaretBrush = ZeroWpfTheme.TextPrimary;
            }

            if (_btnSignIn != null)
            {
                _btnSignIn.Background = ZeroWpfTheme.PrimaryAccent;
                _btnSignIn.Foreground = GetAccentTextBrush();
            }

            if (_btnCancel != null)
            {
                _btnCancel.Background = ZeroWpfTheme.BgInput;
                _btnCancel.Foreground = ZeroWpfTheme.TextPrimary;
                _btnCancel.BorderBrush = ZeroWpfTheme.BorderDefault;
            }

            if (_txtPin != null)
            {
                _txtPin.Background = ZeroWpfTheme.BgInput;
                _txtPin.Foreground = ZeroWpfTheme.TextPrimary;
                _txtPin.BorderBrush = ZeroWpfTheme.BorderDefault;
                _txtPin.CaretBrush = ZeroWpfTheme.TextPrimary;
            }

            if (_lblBadgePrompt != null)
            {
                _lblBadgePrompt.Foreground = ZeroWpfTheme.PrimaryAccent;
            }

            if (_numpad != null)
            {
                _numpad.ApplyTheme();
            }

            UpdateStatusColor();
        }
    }
}
