using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Overlays
{
    public class ZCommandPalette : Window
    {
        private static readonly List<CommandItem> _registeredCommands = new List<CommandItem>();
        private readonly List<CommandItem> _commands;
        private List<CommandItem> _filteredCommands;
        private int _selectedIndex = 0;

        private TextBox _searchBox;
        private ListBox _listBox;
        private Action _closeWithResult;

        protected ZCommandPalette(IEnumerable<CommandItem> commands)
        {
            _commands = commands.ToList();
            _filteredCommands = CommandFilterHelper.Filter(_commands, "");

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;

            InitializeComponents();
        }

        private void InitializeComponents()
        {
            var grid = new Grid();
            
            // Background overlay
            var overlay = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            grid.Children.Add(overlay);

            var card = new Border
            {
                Background = ZeroWpfTheme.BgCard,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Width = 500,
                MaxHeight = 400,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(20)
            };

            var cardGrid = new Grid();
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cardGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            _searchBox = new TextBox
            {
                Background = ZeroWpfTheme.BgCard,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = ZeroWpfTheme.BorderDefault,
                FontSize = 16,
                Padding = new Thickness(16, 12, 16, 12),
                FocusVisualStyle = null
            };
            _searchBox.TextChanged += SearchBox_TextChanged;
            _searchBox.PreviewKeyDown += SearchBox_PreviewKeyDown;

            Grid.SetRow(_searchBox, 0);
            cardGrid.Children.Add(_searchBox);

            _listBox = new ListBox
            {
                Background = ZeroWpfTheme.BgCard,
                BorderThickness = new Thickness(0),
                ItemsSource = _filteredCommands,
                ItemTemplate = CreateItemTemplate(),
                ItemContainerStyle = CreateItemContainerStyle(),
                Padding = new Thickness(0, 4, 0, 4)
            };
            ScrollViewer.SetHorizontalScrollBarVisibility(_listBox, ScrollBarVisibility.Disabled);
            
            _listBox.MouseDoubleClick += ListBox_MouseDoubleClick;

            Grid.SetRow(_listBox, 1);
            cardGrid.Children.Add(_listBox);

            card.Child = cardGrid;
            grid.Children.Add(card);

            Content = grid;

            Loaded += (s, e) => 
            {
                if (Owner != null)
                {
                    Width = Owner.ActualWidth;
                    Height = Owner.ActualHeight;
                }
                _searchBox.Focus();
                if (_listBox.Items.Count > 0)
                {
                    _listBox.SelectedIndex = 0;
                }
            };
        }

        private DataTemplate CreateItemTemplate()
        {
            var template = new DataTemplate(typeof(CommandItem));
            var factory = new FrameworkElementFactory(typeof(Grid));
            factory.SetValue(Grid.MarginProperty, new Thickness(16, 8, 16, 8));
            
            var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
            factory.AppendChild(col1);

            var col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
            factory.AppendChild(col2);

            var col3 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col3.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
            factory.AppendChild(col3);

            var col4 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col4.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            factory.AppendChild(col4);

            var col5 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col5.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
            factory.AppendChild(col5);

            // Glyph
            var glyph = new FrameworkElementFactory(typeof(TextBlock));
            glyph.SetValue(Grid.ColumnProperty, 0);
            glyph.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI Emoji"));
            glyph.SetValue(TextBlock.FontSizeProperty, 14.0);
            glyph.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextSecondary);
            glyph.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 12, 0));
            glyph.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            glyph.SetBinding(TextBlock.TextProperty, new Binding("Glyph"));
            
            var glyphVisibility = new Binding("Glyph") { Converter = new NullToVisibilityConverter() };
            glyph.SetBinding(TextBlock.VisibilityProperty, glyphVisibility);
            factory.AppendChild(glyph);

            // Label
            var label = new FrameworkElementFactory(typeof(TextBlock));
            label.SetValue(Grid.ColumnProperty, 1);
            label.SetValue(TextBlock.FontSizeProperty, 14.0);
            label.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextPrimary);
            label.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            label.SetBinding(TextBlock.TextProperty, new Binding("Label"));
            factory.AppendChild(label);

            // Category
            var categoryBorder = new FrameworkElementFactory(typeof(Border));
            categoryBorder.SetValue(Grid.ColumnProperty, 2);
            categoryBorder.SetValue(Border.BackgroundProperty, ZeroWpfTheme.BorderDefault);
            categoryBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
            categoryBorder.SetValue(Border.MarginProperty, new Thickness(12, 0, 0, 0));
            categoryBorder.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));
            categoryBorder.SetValue(Border.VerticalAlignmentProperty, VerticalAlignment.Center);
            
            var categoryVisibility = new Binding("Category") { Converter = new NullToVisibilityConverter() };
            categoryBorder.SetBinding(Border.VisibilityProperty, categoryVisibility);

            var categoryText = new FrameworkElementFactory(typeof(TextBlock));
            categoryText.SetValue(TextBlock.FontSizeProperty, 11.0);
            categoryText.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextSecondary);
            categoryText.SetBinding(TextBlock.TextProperty, new Binding("Category"));
            categoryBorder.AppendChild(categoryText);
            
            factory.AppendChild(categoryBorder);

            // Shortcut
            var shortcut = new FrameworkElementFactory(typeof(TextBlock));
            shortcut.SetValue(Grid.ColumnProperty, 4);
            shortcut.SetValue(TextBlock.FontSizeProperty, 12.0);
            shortcut.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextMuted);
            shortcut.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            shortcut.SetBinding(TextBlock.TextProperty, new Binding("ShortcutText"));
            
            var shortcutVisibility = new Binding("ShortcutText") { Converter = new NullToVisibilityConverter() };
            shortcut.SetBinding(TextBlock.VisibilityProperty, shortcutVisibility);
            factory.AppendChild(shortcut);

            template.VisualTree = factory;
            return template;
        }

        private Style CreateItemContainerStyle()
        {
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            style.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
            style.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
            style.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            
            var trigger = new Trigger { Property = ListBoxItem.IsSelectedProperty, Value = true };
            trigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, ZeroWpfTheme.BgHover));
            style.Triggers.Add(trigger);

            var hoverTrigger = new Trigger { Property = ListBoxItem.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, ZeroWpfTheme.BgHover));
            style.Triggers.Add(hoverTrigger);

            return style;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _filteredCommands = CommandFilterHelper.Filter(_commands, _searchBox.Text);
            _listBox.ItemsSource = _filteredCommands;
            if (_filteredCommands.Count > 0)
            {
                _listBox.SelectedIndex = 0;
            }
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                if (_listBox.SelectedIndex < _filteredCommands.Count - 1)
                {
                    _listBox.SelectedIndex++;
                    _listBox.ScrollIntoView(_listBox.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (_listBox.SelectedIndex > 0)
                {
                    _listBox.SelectedIndex--;
                    _listBox.ScrollIntoView(_listBox.SelectedItem);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                ExecuteSelected();
                e.Handled = true;
            }
        }

        private void ListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ExecuteSelected();
        }

        private void ExecuteSelected()
        {
            if (_listBox.SelectedItem is CommandItem cmd && cmd.IsEnabled)
            {
                Close();
                cmd.Execute?.Invoke();
            }
        }

        public static void Show(Window owner, IEnumerable<CommandItem> commands)
        {
            var combined = _registeredCommands.Concat(commands).ToList();
            var palette = new ZCommandPalette(combined)
            {
                Owner = owner
            };
            palette.ShowDialog();
        }

        public static void Register(string id, string label, string? category, string? shortcut, Action execute)
        {
            Unregister(id);
            _registeredCommands.Add(new CommandItem
            {
                Id = id,
                Label = label,
                Category = category,
                ShortcutText = shortcut,
                Execute = execute
            });
        }

        public static void Unregister(string id)
        {
            _registeredCommands.RemoveAll(x => x.Id == id);
        }
    }

    [Obsolete("ZeroCommandPalette is deprecated. Please migrate to ZCommandPalette instead.")]
    public class ZeroCommandPalette : ZCommandPalette
    {
        protected ZeroCommandPalette(IEnumerable<CommandItem> commands) : base(commands)
        {
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
