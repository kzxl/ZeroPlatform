// <auto-refactored> Partial class file - Input
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
        private void ProcessKey(string label, VirtualKeyType type)
        {
            switch (type)
            {
                case VirtualKeyType.Shift:
                    if (label == "Caps") _capsActive = !_capsActive;
                    else _shiftActive = !_shiftActive;
                    UpdateLabels();
                    return;

                case VirtualKeyType.Backspace:
                    ApplyBackspace();
                    KeyPressed?.Invoke(this, new VirtualKeyEventArgs("", VirtualKeyType.Backspace));
                    return;

                case VirtualKeyType.Clear:
                    ApplyClear();
                    KeyPressed?.Invoke(this, new VirtualKeyEventArgs("", VirtualKeyType.Clear));
                    return;

                case VirtualKeyType.Enter:
                    ApplyEnter();
                    KeyPressed?.Invoke(this, new VirtualKeyEventArgs("\r\n", VirtualKeyType.Enter));
                    EnterPressed?.Invoke(this, EventArgs.Empty);
                    return;

                case VirtualKeyType.Escape:
                    KeyPressed?.Invoke(this, new VirtualKeyEventArgs("", VirtualKeyType.Escape));
                    EscapePressed?.Invoke(this, EventArgs.Empty);
                    return;

                case VirtualKeyType.Space:
                    ApplyText(" ");
                    KeyPressed?.Invoke(this, new VirtualKeyEventArgs(" ", VirtualKeyType.Space));
                    return;

                case VirtualKeyType.Tab:
                    ApplyText("\t");
                    KeyPressed?.Invoke(this, new VirtualKeyEventArgs("\t", VirtualKeyType.Tab));
                    return;

                default:
                    string outText = label;
                    if (outText.Length == 1 && char.IsLetter(outText[0]))
                    {
                        bool upper = _capsActive ^ _shiftActive;
                        outText = upper ? outText.ToUpperInvariant() : outText.ToLowerInvariant();
                        if (_shiftActive) { _shiftActive = false; UpdateLabels(); }
                    }
                    ApplyText(outText);
                    KeyPressed?.Invoke(this, new VirtualKeyEventArgs(outText, type));
                    break;
            }
        }

        private void UpdateLabels()
        {
            // Update child button labels for Shift/Caps state
            foreach (UIElement child in _mainGrid.Children)
            {
                if (child is Button b && b.Content is string s && s.Length == 1 && char.IsLetter(s[0]))
                {
                    bool upper = _capsActive ^ _shiftActive;
                    b.Content = upper ? s.ToUpperInvariant() : s.ToLowerInvariant();
                }
                else if (child is Grid rowGrid)
                {
                    foreach (UIElement rowChild in rowGrid.Children)
                    {
                        if (rowChild is Button rb && rb.Content is string rs && rs.Length == 1 && char.IsLetter(rs[0]))
                        {
                            bool upper = _capsActive ^ _shiftActive;
                            rb.Content = upper ? rs.ToUpperInvariant() : rs.ToLowerInvariant();
                        }
                    }
                }
            }
        }

        private void ApplyText(string text)
        {
            if (TargetElement is TextBox tb)
            {
                int start = tb.SelectionStart;
                int len = tb.SelectionLength;
                tb.Text = tb.Text.Remove(start, len).Insert(start, text);
                tb.SelectionStart = start + text.Length;
                tb.SelectionLength = 0;
            }
            else if (TargetElement is PasswordBox pb)
            {
                pb.Password += text;
            }
        }

        private void ApplyBackspace()
        {
            if (TargetElement is TextBox tb)
            {
                int start = tb.SelectionStart;
                int len = tb.SelectionLength;
                if (len > 0)
                {
                    tb.Text = tb.Text.Remove(start, len);
                    tb.SelectionStart = start;
                }
                else if (start > 0)
                {
                    tb.Text = tb.Text.Remove(start - 1, 1);
                    tb.SelectionStart = start - 1;
                }
            }
            else if (TargetElement is PasswordBox pb && pb.Password.Length > 0)
            {
                pb.Password = pb.Password.Substring(0, pb.Password.Length - 1);
            }
        }

        private void ApplyClear()
        {
            if (TargetElement is TextBox tb) tb.Text = string.Empty;
            else if (TargetElement is PasswordBox pb) pb.Password = string.Empty;
        }

        private void ApplyEnter()
        {
            if (TargetElement is TextBox tb && tb.AcceptsReturn)
            {
                ApplyText("\r\n");
            }
        }

        /// <summary>
        /// Displays a floating touch-screen virtual keyboard popup.
        /// </summary>
        public static Popup ShowFloatingPopup(UIElement target, VirtualKeyboardLayout layout = VirtualKeyboardLayout.AlphaNumeric)
        {
            var popup = new Popup
            {
                PlacementTarget = target,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true
            };

            var border = new Border
            {
                Background = ZeroWpfTheme.BgCard ?? Brushes.Transparent,
                BorderBrush = ZeroWpfTheme.BorderDefault ?? Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6)
            };

            var kb = new ZVirtualKeyboard
            {
                LayoutMode = layout,
                TargetElement = target,
                Width = (layout == VirtualKeyboardLayout.Numpad) ? 340 : 700,
                Height = (layout == VirtualKeyboardLayout.Numpad) ? 260 : 240
            };

            kb.EscapePressed += (s, e) => popup.IsOpen = false;
            kb.EnterPressed += (s, e) => popup.IsOpen = false;

            border.Child = kb;
            popup.Child = border;

            Action themeHandler = () =>
            {
                border.Background = ZeroWpfTheme.BgCard ?? Brushes.Transparent;
                border.BorderBrush = ZeroWpfTheme.BorderDefault ?? Brushes.Gray;
            };
            ZeroWpfTheme.ThemeChanged += themeHandler;
            popup.Closed += (s, e) => ZeroWpfTheme.ThemeChanged -= themeHandler;

            popup.IsOpen = true;
            return popup;
        }
    }
}
