using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ZeroUI.Core.Localization;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Navigation
{
    public class WizardPageValidatingEventArgs : CancelEventArgs
    {
        public int PageIndex { get; }
        public ZWizardPage Page { get; }
        public string? ErrorMessage { get; set; }

        public WizardPageValidatingEventArgs(int pageIndex, ZWizardPage page)
        {
            PageIndex = pageIndex;
            Page = page;
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="WizardPageValidatingEventArgs"/>.
    /// </summary>
    public class WpfWizardPageValidatingEventArgs : WizardPageValidatingEventArgs
    {
        public WpfWizardPageValidatingEventArgs(int pageIndex, ZWizardPage page) : base(pageIndex, page) { }
    }

    /// <summary>
    /// Represents an individual step/page container in a Wizard sequence for WPF.
    /// </summary>
    public class ZWizardPage : ContentControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(nameof(Title), typeof(string), typeof(ZWizardPage), new PropertyMetadata("Step Title"));

        public static readonly DependencyProperty SubtitleProperty =
            DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(ZWizardPage), new PropertyMetadata("Configure this step."));

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(string), typeof(ZWizardPage), new PropertyMetadata("📋"));

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Subtitle
        {
            get => (string)GetValue(SubtitleProperty);
            set => SetValue(SubtitleProperty, value);
        }

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public event EventHandler<WizardPageValidatingEventArgs>? ValidatingStep;

        internal bool ValidatePage(int index, out string? error)
        {
            error = null;
            if (ValidatingStep != null)
            {
                var args = new WizardPageValidatingEventArgs(index, this);
                ValidatingStep(this, args);
                if (args.Cancel)
                {
                    error = args.ErrorMessage;
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZWizardPage"/>.
    /// </summary>
    [Obsolete("WizardPage is deprecated and will be removed in 5 release cycles. Please migrate to ZWizardPage instead.")]
    public class WizardPage : ZWizardPage
    {
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="ZWizardPage"/>.
    /// </summary>
    [Obsolete("ZeroWizardPage is deprecated and will be removed in 5 release cycles. Please migrate to ZWizardPage instead.")]
    public class ZeroWizardPage : ZWizardPage
    {
    }

    /// <summary>
    /// Modern Enterprise Multi-Step Wizard container for ZeroUI WPF.
    /// Guides users through sequential configuration steps with step indicators,
    /// per-step validation, back/next/finish navigation, and dark/light theming.
    /// </summary>
    public class ZWizard : Control
    {
        private readonly ObservableCollection<ZWizardPage> _pages = new ObservableCollection<ZWizardPage>();
        private int _currentStep = 0;

        private TextBlock? _titleBlock;
        private TextBlock? _subBlock;
        private StackPanel? _stepsIndicatorStack;
        private ContentControl? _pageContentHost;

        private Button? _btnBack;
        private Button? _btnNext;
        private Button? _btnFinish;
        private Button? _btnCancel;

        public ObservableCollection<ZWizardPage> Pages => _pages;

        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                if (value >= 0 && value < _pages.Count && _currentStep != value)
                {
                    _currentStep = value;
                    UpdateWizardView();
                }
            }
        }

        public event EventHandler? StepChanged;
        public event EventHandler? Finished;
        public event EventHandler? Cancelled;

        private Border? _rootBorder;

        static ZWizard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZWizard), new FrameworkPropertyMetadata(typeof(ZWizard)));
        }

        public ZWizard()
        {
            Background = ZeroWpfTheme.BgCard;
            BorderBrush = ZeroWpfTheme.BorderDefault;
            BorderThickness = new Thickness(1);
            Width = 700;
            Height = 500;

            _pages.CollectionChanged += (s, e) =>
            {
                if (_pages.Count == 1) UpdateWizardView();
                else RebuildStepIndicators();
            };

            ZeroLocalizer.CultureChanged += (s, e) => UpdateLocalizedStrings();
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
            UpdateWizardView();
        }

        private void BuildVisualTemplate()
        {
            var rootBorder = new Border
            {
                Background = Background,
                BorderBrush = BorderBrush,
                BorderThickness = BorderThickness,
                CornerRadius = new CornerRadius(6)
            };
            _rootBorder = rootBorder;

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64, GridUnitType.Pixel) }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });   // Page Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56, GridUnitType.Pixel) }); // Footer

            // --- HEADER ---
            var headerBorder = new Border
            {
                Background = ZeroWpfTheme.BgInput,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(20, 10, 20, 10)
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerTitles = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _titleBlock = new TextBlock
            {
                FontSize = 16.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = ZeroWpfTheme.TextPrimary,
                Text = "Step Title"
            };
            _subBlock = new TextBlock
            {
                FontSize = 12.0,
                Foreground = ZeroWpfTheme.TextMuted,
                Text = "Configure step description.",
                Margin = new Thickness(0, 2, 0, 0)
            };
            headerTitles.Children.Add(_titleBlock);
            headerTitles.Children.Add(_subBlock);
            Grid.SetColumn(headerTitles, 0);
            headerGrid.Children.Add(headerTitles);

            _stepsIndicatorStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_stepsIndicatorStack, 1);
            headerGrid.Children.Add(_stepsIndicatorStack);

            headerBorder.Child = headerGrid;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // --- CONTENT HOST ---
            _pageContentHost = new ContentControl
            {
                Margin = new Thickness(24)
            };
            Grid.SetRow(_pageContentHost, 1);
            mainGrid.Children.Add(_pageContentHost);

            // --- FOOTER ---
            var footerBorder = new Border
            {
                Background = ZeroWpfTheme.BgInput,
                BorderBrush = ZeroWpfTheme.BorderDefault,
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 10, 20, 10)
            };
            var footerStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            _btnCancel = new Button
            {
                Content = "Cancel",
                Width = 84,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Background = Brushes.Transparent,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault
            };
            _btnCancel.Click += (s, e) => Cancelled?.Invoke(this, EventArgs.Empty);
            footerStack.Children.Add(_btnCancel);

            _btnBack = new Button
            {
                Content = "← Back",
                Width = 84,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Background = ZeroWpfTheme.BgCard,
                Foreground = ZeroWpfTheme.TextPrimary,
                BorderBrush = ZeroWpfTheme.BorderDefault
            };
            _btnBack.Click += BtnBack_Click;
            footerStack.Children.Add(_btnBack);

            _btnNext = new Button
            {
                Content = "Next →",
                Width = 84,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Background = ZeroWpfTheme.PrimaryAccent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            _btnNext.Click += BtnNext_Click;
            footerStack.Children.Add(_btnNext);

            _btnFinish = new Button
            {
                Content = "Finish ✓",
                Width = 84,
                Height = 32,
                Background = ZeroWpfTheme.SuccessAccent,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Visibility = Visibility.Collapsed
            };
            _btnFinish.Click += BtnFinish_Click;
            footerStack.Children.Add(_btnFinish);

            footerBorder.Child = footerStack;
            Grid.SetRow(footerBorder, 2);
            mainGrid.Children.Add(footerBorder);

            rootBorder.Child = mainGrid;
            AddVisualChild(rootBorder);
            AddLogicalChild(rootBorder);

            UpdateWizardView();
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

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 0)
            {
                _currentStep--;
                UpdateWizardView();
                StepChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < _pages.Count)
            {
                var cur = _pages[_currentStep];
                if (!cur.ValidatePage(_currentStep, out string? err))
                {
                    if (!string.IsNullOrEmpty(err))
                    {
                        MessageBox.Show(err, ZeroLocalizer.GetString(ZeroStringId.WizardValidationTitle), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }

                if (_currentStep < _pages.Count - 1)
                {
                    _currentStep++;
                    UpdateWizardView();
                    StepChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep < _pages.Count)
            {
                var cur = _pages[_currentStep];
                if (!cur.ValidatePage(_currentStep, out string? err))
                {
                    if (!string.IsNullOrEmpty(err))
                    {
                        MessageBox.Show(err, ZeroLocalizer.GetString(ZeroStringId.WizardValidationTitle), MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return;
                }
            }
            Finished?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateLocalizedStrings()
        {
            if (_btnBack != null) _btnBack.Content = ZeroLocalizer.GetString(ZeroStringId.WizardBack);
            if (_btnNext != null) _btnNext.Content = ZeroLocalizer.GetString(ZeroStringId.WizardNext);
            if (_btnFinish != null) _btnFinish.Content = ZeroLocalizer.GetString(ZeroStringId.WizardFinish);
            if (_btnCancel != null) _btnCancel.Content = ZeroLocalizer.GetString(ZeroStringId.WizardCancel);
        }

        private void UpdateWizardView()
        {
            UpdateLocalizedStrings();
            if (_pages.Count == 0)
            {
                if (_titleBlock != null) _titleBlock.Text = ZeroLocalizer.GetString(ZeroStringId.WizardNoPages);
                if (_subBlock != null) _subBlock.Text = ZeroLocalizer.GetString(ZeroStringId.WizardNoPagesDesc);
                if (_pageContentHost != null) _pageContentHost.Content = null;
                if (_btnBack != null) _btnBack.IsEnabled = false;
                if (_btnNext != null) _btnNext.IsEnabled = false;
                if (_btnFinish != null) _btnFinish.Visibility = Visibility.Collapsed;
                return;
            }

            var curPage = _pages[_currentStep];
            if (_titleBlock != null) _titleBlock.Text = curPage.Title;
            if (_subBlock != null) _subBlock.Text = curPage.Subtitle;
            if (_pageContentHost != null) _pageContentHost.Content = curPage;

            if (_btnBack != null) _btnBack.IsEnabled = _currentStep > 0;

            bool isLast = _currentStep == _pages.Count - 1;
            if (_btnNext != null) _btnNext.Visibility = isLast ? Visibility.Collapsed : Visibility.Visible;
            if (_btnFinish != null) _btnFinish.Visibility = isLast ? Visibility.Visible : Visibility.Collapsed;

            RebuildStepIndicators();
        }

        private void RebuildStepIndicators()
        {
            if (_stepsIndicatorStack == null) return;
            _stepsIndicatorStack.Children.Clear();

            for (int i = 0; i < _pages.Count; i++)
            {
                bool isDone = i < _currentStep;
                bool isCurrent = i == _currentStep;

                var dot = new Border
                {
                    Width = 24,
                    Height = 24,
                    CornerRadius = new CornerRadius(12),
                    Background = (isCurrent || isDone) ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.BgCard,
                    BorderBrush = (isCurrent || isDone) ? ZeroWpfTheme.PrimaryAccent : ZeroWpfTheme.BorderDefault,
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(4, 0, 4, 0)
                };

                var num = new TextBlock
                {
                    Text = isDone ? "✓" : (i + 1).ToString(),
                    FontSize = 10.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = (isCurrent || isDone) ? Brushes.White : ZeroWpfTheme.TextSecondary,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                dot.Child = num;
                _stepsIndicatorStack.Children.Add(dot);
            }
        }

        public void AddPage(ZWizardPage page)
        {
            _pages.Add(page);
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
    /// Backward-compatibility alias for <see cref="ZWizard"/>.
    /// </summary>
    [Obsolete("WizardControl is deprecated and will be removed in 5 release cycles. Please migrate to ZWizard instead.")]
    public class WizardControl : ZWizard
    {
        static WizardControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(WizardControl), new FrameworkPropertyMetadata(typeof(ZWizard)));
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="ZWizard"/>.
    /// </summary>
    [Obsolete("ZeroWizard is deprecated and will be removed in 5 release cycles. Please migrate to ZWizard instead.")]
    public class ZeroWizard : ZWizard
    {
        static ZeroWizard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZeroWizard), new FrameworkPropertyMetadata(typeof(ZWizard)));
        }
    }
}
