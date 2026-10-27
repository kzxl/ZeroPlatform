// <auto-refactored> Partial class file - Layout
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
        private void RebuildLayout()
        {
            _mainGrid.Children.Clear();
            _mainGrid.RowDefinitions.Clear();
            _mainGrid.ColumnDefinitions.Clear();
            _mainGrid.Margin = new Thickness(KeySpacing);

            if (LayoutMode == VirtualKeyboardLayout.Numpad)
            {
                BuildNumpad();
            }
            else
            {
                BuildAlphaNumeric();
            }
        }

        private void BuildNumpad()
        {
            int rows = 4;
            int cols = 4;

            for (int r = 0; r < rows; r++)
                _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            for (int c = 0; c < cols; c++)
                _mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            string[,] matrix = new string[,]
            {
                { "7", "8", "9", "Bksp" },
                { "4", "5", "6", "Clear" },
                { "1", "2", "3", "+/-" },
                { "0", ".", "Esc", "Enter" }
            };

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    string label = matrix[r, c];
                    var btn = CreateKeyButton(label, GetKeyType(label));
                    Grid.SetRow(btn, r);
                    Grid.SetColumn(btn, c);
                    _mainGrid.Children.Add(btn);
                }
            }
        }

        private void BuildAlphaNumeric()
        {
            string[][] rows = new string[][]
            {
                new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "Bksp" },
                new[] { "Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]" },
                new[] { "Caps", "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "Enter" },
                new[] { "Shift", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/", "Shift" },
                new[] { "?123", "Clear", "Space", "Esc" }
            };

            for (int r = 0; r < rows.Length; r++)
                _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            for (int r = 0; r < rows.Length; r++)
            {
                var rowGrid = new Grid();
                Grid.SetRow(rowGrid, r);

                var items = rows[r];
                foreach (var item in items)
                {
                    double weight = 1.0;
                    if (item == "Space") weight = 4.5;
                    else if (item == "Enter" || item == "Shift") weight = 1.6;
                    else if (item == "Bksp" || item == "Caps" || item == "Clear" || item == "?123" || item == "Esc") weight = 1.3;

                    rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weight, GridUnitType.Star) });
                }

                for (int c = 0; c < items.Length; c++)
                {
                    string label = items[c];
                    var btn = CreateKeyButton(label, GetKeyType(label));
                    Grid.SetColumn(btn, c);
                    rowGrid.Children.Add(btn);
                }

                _mainGrid.Children.Add(rowGrid);
            }
        }

        private static VirtualKeyType GetKeyType(string label)
        {
            if (label == "Bksp") return VirtualKeyType.Backspace;
            if (label == "Clear") return VirtualKeyType.Clear;
            if (label == "Enter") return VirtualKeyType.Enter;
            if (label == "Esc") return VirtualKeyType.Escape;
            if (label == "Space") return VirtualKeyType.Space;
            if (label == "Shift" || label == "Caps") return VirtualKeyType.Shift;
            if (label == "Tab") return VirtualKeyType.Tab;
            if (label == "?123") return VirtualKeyType.SymbolToggle;
            return VirtualKeyType.Character;
        }

        private Button CreateKeyButton(string label, VirtualKeyType type)
        {
            var btn = new Button
            {
                Content = label,
                Tag = type,
                Margin = new Thickness(KeySpacing / 2),
                Focusable = false,
                FontSize = (type != VirtualKeyType.Character && label.Length > 2) ? 12 : 16,
                FontWeight = (type != VirtualKeyType.Character) ? FontWeights.Bold : FontWeights.Normal,
                FontFamily = new FontFamily("Segoe UI, Arial, sans-serif"),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.Name = "bd";
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            borderFactory.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(Button.Background)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            borderFactory.SetBinding(Border.BorderBrushProperty, new System.Windows.Data.Binding(nameof(Button.BorderBrush)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            borderFactory.SetBinding(Border.BorderThicknessProperty, new System.Windows.Data.Binding(nameof(Button.BorderThickness)) { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });

            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;

            // Hover trigger
            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, ZeroWpfTheme.BgHover ?? Brushes.DarkGray, "bd"));
            hoverTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, ZeroWpfTheme.BorderFocus ?? Brushes.LightGray, "bd"));
            template.Triggers.Add(hoverTrigger);

            // Pressed trigger
            var pressedTrigger = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, ZeroWpfTheme.PrimaryAccent ?? Brushes.DodgerBlue, "bd"));
            pressedTrigger.Setters.Add(new Setter(Button.ForegroundProperty, GetAccentTextBrush()));
            template.Triggers.Add(pressedTrigger);

            btn.Template = template;
            UpdateKeyTheme(btn);

            btn.Click += (s, e) => ProcessKey(label, type);
            return btn;
        }
    }
}
