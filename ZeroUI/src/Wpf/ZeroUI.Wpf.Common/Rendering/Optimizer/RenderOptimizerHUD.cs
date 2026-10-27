using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ZeroUI.Core.Rendering.Optimizer;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Rendering.Optimizer
{
    /// <summary>
    /// Real-time diagnostic Heads-Up Display (HUD) for ZeroUI's Automatic Render Optimizer.
    /// Floats over any application window to display frame time telemetry, 16.6ms budget compliance,
    /// GPU hardware tier, active fidelity tier, and 9-slice atlas hit rates.
    /// </summary>
    public class RenderOptimizerHUD : Control
    {
        private readonly DispatcherTimer _telemetryTimer;
        private Border? _rootBorder;
        private TextBlock? _txtFps;
        private TextBlock? _txtFrameTime;
        private TextBlock? _txtFidelity;
        private TextBlock? _txtGpu;
        private TextBlock? _txtAtlas;
        private FrameworkElement? _expandedPanel;
        private Button? _btnToggle;

        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(RenderOptimizerHUD),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public RenderOptimizerHUD()
        {
            SnapsToDevicePixels = true;
            HorizontalAlignment = HorizontalAlignment.Right;
            VerticalAlignment = VerticalAlignment.Top;
            Margin = new Thickness(16);

            _telemetryTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _telemetryTimer.Tick += OnTelemetryTick;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_rootBorder == null)
            {
                BuildVisualTree();
            }
            _telemetryTimer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _telemetryTimer.Stop();
        }

        private void BuildVisualTree()
        {
            if (_rootBorder != null) return;

            var root = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 11, 15, 25)), // Translucent dark glass
                BorderBrush = new SolidColorBrush(Color.FromArgb(160, 56, 189, 248)), // Cyan accent
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                MinWidth = 240
            };
            _rootBorder = root;

            var mainStack = new StackPanel();

            // Header row with toggle button
            var headerDock = new DockPanel { LastChildFill = true };
            var titleBlock = new TextBlock
            {
                Text = "⚡ RENDER OPTIMIZER HUD",
                FontWeight = FontWeights.Bold,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(titleBlock, Dock.Left);
            headerDock.Children.Add(titleBlock);

            _btnToggle = new Button
            {
                Content = IsExpanded ? "▲" : "▼",
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                FontSize = 9,
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            _btnToggle.Click += (s, e) =>
            {
                IsExpanded = !IsExpanded;
                _btnToggle.Content = IsExpanded ? "▲" : "▼";
                if (_expandedPanel != null)
                {
                    _expandedPanel.Visibility = IsExpanded ? Visibility.Visible : Visibility.Collapsed;
                }
            };
            DockPanel.SetDock(_btnToggle, Dock.Right);
            headerDock.Children.Add(_btnToggle);
            mainStack.Children.Add(headerDock);

            // FPS & Frame Time Big Indicators
            var statsDock = new DockPanel { Margin = new Thickness(0, 6, 0, 6) };
            _txtFps = new TextBlock
            {
                Text = "60.0 FPS",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128)) // Emerald green
            };
            DockPanel.SetDock(_txtFps, Dock.Left);
            statsDock.Children.Add(_txtFps);

            _txtFrameTime = new TextBlock
            {
                Text = "8.2 ms",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_txtFrameTime, Dock.Right);
            statsDock.Children.Add(_txtFrameTime);
            mainStack.Children.Add(statsDock);

            // Expandable details panel
            var detailsStack = new StackPanel
            {
                Visibility = IsExpanded ? Visibility.Visible : Visibility.Collapsed
            };
            _expandedPanel = detailsStack;

            // Separator
            detailsStack.Children.Add(new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                Margin = new Thickness(0, 4, 0, 8)
            });

            // Fidelity Tier Row
            _txtFidelity = new TextBlock
            {
                Text = "Tier: ULTRA (144 FPS Budget)",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                Margin = new Thickness(0, 0, 0, 4),
                FontWeight = FontWeights.SemiBold
            };
            detailsStack.Children.Add(_txtFidelity);

            // GPU Adapter Row
            _txtGpu = new TextBlock
            {
                Text = "GPU: Direct3D 11 Dedicated (4096MB)",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(167, 139, 250)),
                Margin = new Thickness(0, 0, 0, 4),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            detailsStack.Children.Add(_txtGpu);

            // Atlas Hit Rate Row
            _txtAtlas = new TextBlock
            {
                Text = "Atlas: 100.0% Hit | 95% Saved",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184))
            };
            detailsStack.Children.Add(_txtAtlas);

            mainStack.Children.Add(detailsStack);
            root.Child = mainStack;
            AddLogicalChild(root);
            AddVisualChild(root);
        }

        private void OnTelemetryTick(object? sender, EventArgs e)
        {
            var monitor = ZeroWpfRenderMonitor.Instance;
            double fps = monitor.CurrentFps;
            double ms = monitor.RollingAverageFrameTimeMs;
            var tier = monitor.CurrentFidelity;

            if (_txtFps != null)
            {
                _txtFps.Text = $"{fps:F1} FPS";
                _txtFps.Foreground = fps >= 58.0
                    ? new SolidColorBrush(Color.FromRgb(74, 222, 128)) // Green
                    : (fps >= 30.0 ? new SolidColorBrush(Color.FromRgb(251, 191, 36)) : new SolidColorBrush(Color.FromRgb(239, 68, 68))); // Yellow / Red
            }

            if (_txtFrameTime != null)
            {
                _txtFrameTime.Text = $"{ms:F1} ms ({(ms <= 18.0 ? "OK" : "OVER")})";
                _txtFrameTime.Foreground = ms <= 18.0
                    ? new SolidColorBrush(Color.FromRgb(226, 232, 240))
                    : new SolidColorBrush(Color.FromRgb(239, 68, 68));
            }

            if (_txtFidelity != null)
            {
                string tierStr = tier switch
                {
                    RenderFidelityTier.Ultra => "Tier: ULTRA (144 FPS Target)",
                    RenderFidelityTier.Balanced => "Tier: BALANCED (60 FPS Target)",
                    _ => "Tier: POWER SAVER (Throttled)"
                };
                _txtFidelity.Text = tierStr;
                _txtFidelity.Foreground = tier == RenderFidelityTier.Ultra
                    ? new SolidColorBrush(Color.FromRgb(56, 189, 248))
                    : (tier == RenderFidelityTier.Balanced ? new SolidColorBrush(Color.FromRgb(74, 222, 128)) : new SolidColorBrush(Color.FromRgb(248, 113, 113)));
            }

            if (_txtGpu != null)
            {
                _txtGpu.Text = $"GPU: {ZeroGpuCapabilities.AdapterName} ({ZeroGpuCapabilities.DedicatedVramMb:F0}MB)";
            }

            if (_txtAtlas != null)
            {
                long hits = ZeroShadowAtlas.CacheHits;
                double rate = ZeroShadowAtlas.HitRatePercentage;
                _txtAtlas.Text = $"Atlas: {rate:F1}% Hit | {ZeroShadowAtlas.CachedPatchCount} Patches | {hits} Calls Saved";
            }
        }

        protected override System.Collections.IEnumerator LogicalChildren
        {
            get
            {
                if (_rootBorder != null)
                    yield return _rootBorder;
            }
        }

        protected override int VisualChildrenCount => _rootBorder != null ? 1 : 0;

        protected override Visual GetVisualChild(int index)
        {
            if (index != 0 || _rootBorder == null) throw new ArgumentOutOfRangeException(nameof(index));
            return _rootBorder;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (_rootBorder != null)
            {
                _rootBorder.Measure(constraint);
                return _rootBorder.DesiredSize;
            }
            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            _rootBorder?.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }
    }

    /// <summary>
    /// Legacy alias for <see cref="RenderOptimizerHUD"/>.
    /// </summary>
    [Obsolete("ZeroRenderOptimizerHUD is deprecated. Use RenderOptimizerHUD instead.")]
    public class ZeroRenderOptimizerHUD : RenderOptimizerHUD
    {
    }
}
