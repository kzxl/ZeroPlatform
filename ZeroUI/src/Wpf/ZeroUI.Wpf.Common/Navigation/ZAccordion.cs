using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Navigation
{
    public enum AccordionExpandMode
    {
        MultipleGroups,
        SingleGroup
    }

    public class AccordionItem
    {
        public string Text { get; set; } = string.Empty;
        public string? Glyph { get; set; }
        public string? BadgeText { get; set; }
        public object? Tag { get; set; }

        public event EventHandler? Click;

        public AccordionItem() { }

        public AccordionItem(string text, string? glyph = null, EventHandler? onClick = null, string? badge = null)
        {
            Text = text;
            Glyph = glyph;
            BadgeText = badge;
            if (onClick != null) Click += onClick;
        }

        internal void RaiseClick() => Click?.Invoke(this, EventArgs.Empty);
    }

    [Obsolete("ZeroAccordionItem is deprecated. Use AccordionItem instead.")]
    public class ZeroAccordionItem : AccordionItem
    {
        public ZeroAccordionItem() { }
        public ZeroAccordionItem(string text, string? glyph = null, EventHandler? onClick = null, string? badge = null)
            : base(text, glyph, onClick, badge) { }
    }

    public class AccordionGroup
    {
        public string Text { get; set; } = string.Empty;
        public string? Glyph { get; set; }
        public bool IsExpanded { get; set; } = true;
        public string? BadgeText { get; set; }
        public ObservableCollection<AccordionItem> Items { get; } = new ObservableCollection<AccordionItem>();
        public object? Tag { get; set; }

        public AccordionGroup() { }

        public AccordionGroup(string text, string? glyph = null, bool isExpanded = true)
        {
            Text = text;
            Glyph = glyph;
            IsExpanded = isExpanded;
        }

        public AccordionItem AddItem(string text, string? glyph = null, EventHandler? onClick = null, string? badge = null)
        {
            var item = new AccordionItem(text, glyph, onClick, badge);
            Items.Add(item);
            return item;
        }
    }

    [Obsolete("ZeroAccordionGroup is deprecated. Use AccordionGroup instead.")]
    public class ZeroAccordionGroup : AccordionGroup
    {
        public ZeroAccordionGroup() { }
        public ZeroAccordionGroup(string text, string? glyph = null, bool isExpanded = true)
            : base(text, glyph, isExpanded) { }
    }

    /// <summary>
    /// Modern Enterprise Accordion navigation container for ZeroUI WPF.
    /// Supports nested collapsible groups, glyph icons, notification badges,
    /// and SingleGroup or MultipleGroups expansion modes.
    /// </summary>
    public class ZAccordion : Control
    {
        private readonly ObservableCollection<AccordionGroup> _groups = new ObservableCollection<AccordionGroup>();
        private StackPanel? _groupsStack;
        private Border? _rootBorder;
        private AccordionExpandMode _expandMode = AccordionExpandMode.MultipleGroups;

        public ObservableCollection<AccordionGroup> Groups => _groups;

        public AccordionExpandMode ExpandMode
        {
            get => _expandMode;
            set { _expandMode = value; RebuildUI(); }
        }

        public event EventHandler<AccordionItem>? ItemClicked;
        public event EventHandler<AccordionGroup>? GroupToggled;

        static ZAccordion()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZAccordion), new FrameworkPropertyMetadata(typeof(ZAccordion)));
        }

        public ZAccordion()
        {
            Background = ZeroWpfTheme.BgCard;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            BorderThickness = new Thickness(1);
            Width = 260;

            _groups.CollectionChanged += (s, e) => RebuildUI();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            BuildVisualTemplate();
        }

        private void OnThemeChanged()
        {
            Background = ZeroWpfTheme.BgCard;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            if (_rootBorder != null)
            {
                _rootBorder.Background = Background;
                _rootBorder.BorderBrush = BorderBrush;
            }
            RebuildUI();
        }

        private void BuildVisualTemplate()
        {
            var border = new Border
            {
                Background = Background,
                BorderBrush = BorderBrush,
                BorderThickness = BorderThickness,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(4)
            };

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            _groupsStack = new StackPanel();
            scroll.Content = _groupsStack;

            border.Child = scroll;
            _rootBorder = border;
            AddVisualChild(border);
            AddLogicalChild(border);
        }

        protected override int VisualChildrenCount => _rootBorder != null ? 1 : 0;
        protected override Visual GetVisualChild(int index) => _rootBorder ?? throw new ArgumentOutOfRangeException(nameof(index));

        protected override Size MeasureOverride(Size constraint)
        {
            if (_rootBorder != null)
            {
                _rootBorder.Measure(constraint);
                return _rootBorder.DesiredSize;
            }
            return base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _rootBorder?.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        private void RebuildUI()
        {
            if (_groupsStack == null) return;
            _groupsStack.Children.Clear();

            for (int g = 0; g < _groups.Count; g++)
            {
                var group = _groups[g];
                var groupContainer = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };

                // Group Header
                var headerBorder = new Border
                {
                    Background = ZeroWpfTheme.BgInput,
                    BorderBrush = ZeroWpfTheme.BorderSubtle,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(10, 8, 10, 8),
                    Cursor = Cursors.Hand
                };

                var headerDock = new DockPanel();

                // Chevron toggle
                var chevron = new TextBlock
                {
                    Text = group.IsExpanded ? "▼" : "▶",
                    FontSize = 9.0,
                    Foreground = ZeroWpfTheme.TextMuted,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                DockPanel.SetDock(chevron, Dock.Left);
                headerDock.Children.Add(chevron);

                // Badge if present
                if (!string.IsNullOrEmpty(group.BadgeText))
                {
                    var badge = new Border
                    {
                        Background = ZeroWpfTheme.PrimaryAccent,
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(6, 1, 6, 1),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var badgeTxt = new TextBlock
                    {
                        Text = group.BadgeText,
                        FontSize = 10.0,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    };
                    badge.Child = badgeTxt;
                    DockPanel.SetDock(badge, Dock.Right);
                    headerDock.Children.Add(badge);
                }

                // Group title
                var titleStack = new StackPanel { Orientation = Orientation.Horizontal };
                if (!string.IsNullOrEmpty(group.Glyph))
                {
                    var glyphTxt = new TextBlock
                    {
                        Text = group.Glyph,
                        FontSize = 13.0,
                        Foreground = ZeroWpfTheme.PrimaryAccent,
                        Margin = new Thickness(0, 0, 8, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    titleStack.Children.Add(glyphTxt);
                }
                var titleTxt = new TextBlock
                {
                    Text = group.Text,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 12.5,
                    Foreground = ZeroWpfTheme.TextPrimary,
                    VerticalAlignment = VerticalAlignment.Center
                };
                titleStack.Children.Add(titleTxt);
                headerDock.Children.Add(titleStack);

                headerBorder.Child = headerDock;

                // Items list
                var itemsPanel = new StackPanel
                {
                    Margin = new Thickness(16, 2, 0, 2),
                    Visibility = group.IsExpanded ? Visibility.Visible : Visibility.Collapsed
                };

                for (int i = 0; i < group.Items.Count; i++)
                {
                    var item = group.Items[i];
                    var itemBorder = new Border
                    {
                        Background = Brushes.Transparent,
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8, 6, 8, 6),
                        Margin = new Thickness(0, 1, 0, 1),
                        Cursor = Cursors.Hand
                    };

                    itemBorder.MouseEnter += (s, e) => itemBorder.Background = ZeroWpfTheme.BgInput;
                    itemBorder.MouseLeave += (s, e) => itemBorder.Background = Brushes.Transparent;

                    var itemDock = new DockPanel();

                    if (!string.IsNullOrEmpty(item.BadgeText))
                    {
                        var ibadge = new Border
                        {
                            Background = ZeroWpfTheme.BgInput,
                            BorderBrush = ZeroWpfTheme.BorderSubtle,
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(5, 0, 5, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        var ibadgeTxt = new TextBlock
                        {
                            Text = item.BadgeText,
                            FontSize = 9.5,
                            Foreground = ZeroWpfTheme.TextSecondary
                        };
                        ibadge.Child = ibadgeTxt;
                        DockPanel.SetDock(ibadge, Dock.Right);
                        itemDock.Children.Add(ibadge);
                    }

                    var ititleStack = new StackPanel { Orientation = Orientation.Horizontal };
                    if (!string.IsNullOrEmpty(item.Glyph))
                    {
                        var iglyph = new TextBlock
                        {
                            Text = item.Glyph,
                            FontSize = 12.0,
                            Foreground = ZeroWpfTheme.TextSecondary,
                            Margin = new Thickness(0, 0, 6, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        ititleStack.Children.Add(iglyph);
                    }
                    var itxt = new TextBlock
                    {
                        Text = item.Text,
                        FontSize = 12.0,
                        Foreground = ZeroWpfTheme.TextSecondary,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    ititleStack.Children.Add(itxt);
                    itemDock.Children.Add(ititleStack);

                    itemBorder.Child = itemDock;
                    itemBorder.MouseDown += (s, e) =>
                    {
                        item.RaiseClick();
                        ItemClicked?.Invoke(this, item);
                    };

                    itemsPanel.Children.Add(itemBorder);
                }

                headerBorder.MouseDown += (s, e) =>
                {
                    if (ExpandMode == AccordionExpandMode.SingleGroup && !group.IsExpanded)
                    {
                        for (int k = 0; k < _groups.Count; k++)
                        {
                            if (_groups[k] != group && _groups[k].IsExpanded)
                            {
                                _groups[k].IsExpanded = false;
                            }
                        }
                    }

                    group.IsExpanded = !group.IsExpanded;
                    chevron.Text = group.IsExpanded ? "▼" : "▶";
                    itemsPanel.Visibility = group.IsExpanded ? Visibility.Visible : Visibility.Collapsed;
                    GroupToggled?.Invoke(this, group);
                };

                groupContainer.Children.Add(headerBorder);
                groupContainer.Children.Add(itemsPanel);
                _groupsStack.Children.Add(groupContainer);
            }
        }

        public AccordionGroup AddGroup(string text, string? glyph = null, bool isExpanded = true)
        {
            var grp = new AccordionGroup(text, glyph, isExpanded);
            _groups.Add(grp);
            return grp;
        }
    
    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged += OnThemeChanged;
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        ZeroWpfTheme.ThemeChanged -= OnThemeChanged;
    }
}

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZAccordion"/>.
    /// </summary>
    [Obsolete("AccordionControl is deprecated and will be removed in 5 release cycles. Please migrate to ZAccordion instead.")]
    public class AccordionControl : ZAccordion
    {
        static AccordionControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(AccordionControl), new FrameworkPropertyMetadata(typeof(ZAccordion)));
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZAccordion"/>.
    /// </summary>
    [Obsolete("ZeroAccordion is deprecated and will be removed in 5 release cycles. Please migrate to ZAccordion instead.")]
    public class ZeroAccordion : ZAccordion
    {
        static ZeroAccordion()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZeroAccordion), new FrameworkPropertyMetadata(typeof(ZAccordion)));
        }
    }
}
