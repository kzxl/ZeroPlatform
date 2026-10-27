using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ZeroUI.Core.Editors;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Media
{
    /// <summary>
    /// Represents a single metadata key-value item in <see cref="ZExifTelemetryCard"/>.
    /// </summary>
    public class ExifTelemetryItem : INotifyPropertyChanged
    {
        private string _name = "";
        private string _value = "";
        private string? _category;

        public ExifTelemetryItem() { }

        public ExifTelemetryItem(string name, string value, string? category = null)
        {
            _name = name ?? "";
            _value = value ?? "";
            _category = category;
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(nameof(Value)); }
        }

        public string? Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(nameof(Category)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

        public override string ToString() => $"{Name}: {Value}";
    }

    /// <summary>
    /// Event arguments when map is requested for GPS coordinates.
    /// </summary>
    public class GpsMapRequestedEventArgs : EventArgs
    {
        public double Latitude { get; }
        public double Longitude { get; }
        public bool Handled { get; set; }

        public GpsMapRequestedEventArgs(double lat, double lon)
        {
            Latitude = lat;
            Longitude = lon;
        }
    }

    /// <summary>
    /// Modern industrial HUD & Card control for inspecting Camera, Exposure, Lens,
    /// GPS telemetry, and raw EXIF metadata tags.
    /// </summary>
    public class ZExifTelemetryCard : Control, IZeroEditor
    {
        private readonly ObservableCollection<ExifTelemetryItem> _exifRows = new ObservableCollection<ExifTelemetryItem>();

        private TextBlock? _txtShutter;
        private TextBlock? _txtAperture;
        private TextBlock? _txtIso;
        private TextBlock? _txtFocal;
        private TextBlock? _txtCamera;
        private TextBlock? _txtLens;
        private TextBlock? _txtDateTime;
        private FrameworkElement? _hudGrid;
        private FrameworkElement? _gpsBorder;
        private TextBlock? _txtGps;
        private ItemsControl? _icExif;
        private FrameworkElement? _detailedExifContainer;
        private bool _isModified;

        #region Dependency Properties

        public static readonly DependencyProperty ShutterSpeedProperty =
            DependencyProperty.Register(nameof(ShutterSpeed), typeof(string), typeof(ZExifTelemetryCard),
                new PropertyMetadata("", OnTelemetryFieldChanged));

        public static readonly DependencyProperty ApertureProperty =
            DependencyProperty.Register(nameof(Aperture), typeof(string), typeof(ZExifTelemetryCard),
                new PropertyMetadata("", OnTelemetryFieldChanged));

        public static readonly DependencyProperty IsoProperty =
            DependencyProperty.Register(nameof(Iso), typeof(string), typeof(ZExifTelemetryCard),
                new PropertyMetadata("", OnTelemetryFieldChanged));

        public static readonly DependencyProperty FocalLengthProperty =
            DependencyProperty.Register(nameof(FocalLength), typeof(string), typeof(ZExifTelemetryCard),
                new PropertyMetadata("", OnTelemetryFieldChanged));

        public static readonly DependencyProperty CameraModelProperty =
            DependencyProperty.Register(nameof(CameraModel), typeof(string), typeof(ZExifTelemetryCard),
                new PropertyMetadata("", OnTelemetryFieldChanged));

        public static readonly DependencyProperty LensModelProperty =
            DependencyProperty.Register(nameof(LensModel), typeof(string), typeof(ZExifTelemetryCard),
                new PropertyMetadata("", OnTelemetryFieldChanged));

        public static readonly DependencyProperty CaptureDateTimeProperty =
            DependencyProperty.Register(nameof(CaptureDateTime), typeof(string), typeof(ZExifTelemetryCard),
                new PropertyMetadata("", OnTelemetryFieldChanged));

        public static readonly DependencyProperty HasGpsProperty =
            DependencyProperty.Register(nameof(HasGps), typeof(bool), typeof(ZExifTelemetryCard),
                new PropertyMetadata(false, OnGpsChanged));

        public static readonly DependencyProperty GpsLatitudeProperty =
            DependencyProperty.Register(nameof(GpsLatitude), typeof(double?), typeof(ZExifTelemetryCard),
                new PropertyMetadata(null, OnGpsChanged));

        public static readonly DependencyProperty GpsLongitudeProperty =
            DependencyProperty.Register(nameof(GpsLongitude), typeof(double?), typeof(ZExifTelemetryCard),
                new PropertyMetadata(null, OnGpsChanged));

        public static readonly DependencyProperty GpsTextProperty =
            DependencyProperty.Register(nameof(GpsText), typeof(string), typeof(ZExifTelemetryCard),
                new PropertyMetadata("", OnGpsChanged));

        public static readonly DependencyProperty AutoLaunchMapOnGpsClickProperty =
            DependencyProperty.Register(nameof(AutoLaunchMapOnGpsClick), typeof(bool), typeof(ZExifTelemetryCard),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ShowDetailedExifProperty =
            DependencyProperty.Register(nameof(ShowDetailedExif), typeof(bool), typeof(ZExifTelemetryCard),
                new PropertyMetadata(true, OnLayoutChanged));

        public static readonly DependencyProperty IsDetailedExifExpandedProperty =
            DependencyProperty.Register(nameof(IsDetailedExifExpanded), typeof(bool), typeof(ZExifTelemetryCard),
                new PropertyMetadata(true, OnLayoutChanged));

        public static readonly DependencyProperty DetailedExifHeaderProperty =
            DependencyProperty.Register(nameof(DetailedExifHeader), typeof(string), typeof(ZExifTelemetryCard),
                new PropertyMetadata("EXIF METADATA", OnLayoutChanged));

        public string ShutterSpeed
        {
            get => (string)GetValue(ShutterSpeedProperty);
            set => SetValue(ShutterSpeedProperty, value);
        }

        public string Aperture
        {
            get => (string)GetValue(ApertureProperty);
            set => SetValue(ApertureProperty, value);
        }

        public string Iso
        {
            get => (string)GetValue(IsoProperty);
            set => SetValue(IsoProperty, value);
        }

        public string FocalLength
        {
            get => (string)GetValue(FocalLengthProperty);
            set => SetValue(FocalLengthProperty, value);
        }

        public string CameraModel
        {
            get => (string)GetValue(CameraModelProperty);
            set => SetValue(CameraModelProperty, value);
        }

        public string LensModel
        {
            get => (string)GetValue(LensModelProperty);
            set => SetValue(LensModelProperty, value);
        }

        public string CaptureDateTime
        {
            get => (string)GetValue(CaptureDateTimeProperty);
            set => SetValue(CaptureDateTimeProperty, value);
        }

        public bool HasGps
        {
            get => (bool)GetValue(HasGpsProperty);
            set => SetValue(HasGpsProperty, value);
        }

        public double? GpsLatitude
        {
            get => (double?)GetValue(GpsLatitudeProperty);
            set => SetValue(GpsLatitudeProperty, value);
        }

        public double? GpsLongitude
        {
            get => (double?)GetValue(GpsLongitudeProperty);
            set => SetValue(GpsLongitudeProperty, value);
        }

        public string GpsText
        {
            get => (string)GetValue(GpsTextProperty);
            set => SetValue(GpsTextProperty, value);
        }

        public bool AutoLaunchMapOnGpsClick
        {
            get => (bool)GetValue(AutoLaunchMapOnGpsClickProperty);
            set => SetValue(AutoLaunchMapOnGpsClickProperty, value);
        }

        public bool ShowDetailedExif
        {
            get => (bool)GetValue(ShowDetailedExifProperty);
            set => SetValue(ShowDetailedExifProperty, value);
        }

        public bool IsDetailedExifExpanded
        {
            get => (bool)GetValue(IsDetailedExifExpandedProperty);
            set => SetValue(IsDetailedExifExpandedProperty, value);
        }

        public string DetailedExifHeader
        {
            get => (string)GetValue(DetailedExifHeaderProperty);
            set => SetValue(DetailedExifHeaderProperty, value);
        }

        public bool ReadOnly { get; set; }

        #endregion

        #region Events

        public event EventHandler<GpsMapRequestedEventArgs>? GpsMapRequested;
        public event EventHandler? EditValueChanged;

        #endregion

        #region IZeroEditor Implementation

        public object? EditValue
        {
            get => _exifRows;
            set
            {
                if (value is IEnumerable<ExifTelemetryItem> items)
                {
                    SetExifRows(items);
                }
                else if (value == null)
                {
                    Clear();
                }
            }
        }

        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        public void Reset()
        {
            Clear();
            _isModified = false;
        }

        #endregion

        public ObservableCollection<ExifTelemetryItem> ExifRows => _exifRows;

        static ZExifTelemetryCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZExifTelemetryCard),
                new FrameworkPropertyMetadata(typeof(ZExifTelemetryCard)));
        }

        public ZExifTelemetryCard()
        {
            Loaded += (s, e) => BuildVisualTree();
        }

        private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZExifTelemetryCard card) card.BuildVisualTree();
        }

        private static void OnTelemetryFieldChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZExifTelemetryCard card) card.UpdateTelemetryLabels();
        }

        private static void OnGpsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZExifTelemetryCard card) card.UpdateGpsSection();
        }

        public void SetTelemetry(string? camera = null, string? lens = null,
                                 string? shutter = null, string? aperture = null,
                                 string? iso = null, string? focalLength = null,
                                 string? captureDateTime = null)
        {
            CameraModel = camera ?? "";
            LensModel = lens ?? "";
            ShutterSpeed = shutter ?? "";
            Aperture = aperture ?? "";
            Iso = iso ?? "";
            FocalLength = focalLength ?? "";
            CaptureDateTime = captureDateTime ?? "";
            UpdateTelemetryLabels();
        }

        public void SetGps(double lat, double lon, string? formattedText = null)
        {
            GpsLatitude = lat;
            GpsLongitude = lon;
            HasGps = true;
            GpsText = !string.IsNullOrWhiteSpace(formattedText)
                ? formattedText!
                : $"{lat:0.0000}°, {lon:0.0000}°";
            UpdateGpsSection();
        }

        public void ClearGps()
        {
            HasGps = false;
            GpsLatitude = null;
            GpsLongitude = null;
            GpsText = "";
            UpdateGpsSection();
        }

        public void SetExifRows(IEnumerable<ExifTelemetryItem> rows)
        {
            _exifRows.Clear();
            if (rows != null)
            {
                foreach (var r in rows) _exifRows.Add(r);
            }
            _isModified = true;
            EditValueChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            _exifRows.Clear();
            CameraModel = "";
            LensModel = "";
            ShutterSpeed = "";
            Aperture = "";
            Iso = "";
            FocalLength = "";
            CaptureDateTime = "";
            ClearGps();
            UpdateTelemetryLabels();
        }

        #region Visual Tree Construction

        private Visual? _rootVisual;

        protected override int VisualChildrenCount => _rootVisual != null ? 1 : 0;

        protected override Visual GetVisualChild(int index)
        {
            if (_rootVisual == null || index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return _rootVisual;
        }

        private void AttachVisualChild(Visual visual)
        {
            if (_rootVisual != null)
            {
                RemoveVisualChild(_rootVisual);
                RemoveLogicalChild(_rootVisual);
            }
            _rootVisual = visual;
            if (visual != null)
            {
                AddVisualChild(visual);
                AddLogicalChild(visual);
            }
        }

        protected override Size MeasureOverride(Size constraint)
        {
            if (_rootVisual is UIElement elem)
            {
                elem.Measure(constraint);
                return elem.DesiredSize;
            }
            return base.MeasureOverride(constraint);
        }

        protected override Size ArrangeOverride(Size arrangeBounds)
        {
            if (_rootVisual is UIElement elem)
            {
                elem.Arrange(new Rect(arrangeBounds));
                return arrangeBounds;
            }
            return base.ArrangeOverride(arrangeBounds);
        }

        private void BuildVisualTree()
        {
            var root = new StackPanel();

            // 1. Exposure HUD Badges (Shutter, Aperture, ISO, Focal)
            var hudBorder = new Border
            {
                Margin = new Thickness(6, 4, 6, 4),
                Background = ZeroWpfTheme.BgCard,
                BorderBrush = ZeroWpfTheme.BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 5, 6, 5)
            };

            var grid = new UniformGrid { Columns = 4 };

            grid.Children.Add(CreateHudGauge("SHUTTER", out _txtShutter));
            grid.Children.Add(CreateHudGauge("APERTURE", out _txtAperture));
            grid.Children.Add(CreateHudGauge("ISO", out _txtIso));
            grid.Children.Add(CreateHudGauge("FOCAL", out _txtFocal));

            hudBorder.Child = grid;
            _hudGrid = hudBorder;
            root.Children.Add(hudBorder);

            // 2. Camera & Lens Banner
            var devBorder = new Border
            {
                Margin = new Thickness(6, 0, 6, 4),
                Padding = new Thickness(4, 2, 4, 2)
            };
            var devStack = new StackPanel();

            _txtCamera = new TextBlock
            {
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = ZeroWpfTheme.TextPrimary,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            devStack.Children.Add(_txtCamera);

            _txtLens = new TextBlock
            {
                FontSize = 10,
                Foreground = ZeroWpfTheme.TextSecondary,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0)
            };
            devStack.Children.Add(_txtLens);

            _txtDateTime = new TextBlock
            {
                FontSize = 9.5,
                Foreground = ZeroWpfTheme.TextMuted,
                Margin = new Thickness(0, 2, 0, 0)
            };
            devStack.Children.Add(_txtDateTime);

            devBorder.Child = devStack;
            root.Children.Add(devBorder);

            // 3. GPS Location Section
            var gpsBorder = new Border
            {
                Background = ZeroWpfTheme.BgCard,
                BorderBrush = ZeroWpfTheme.BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(6, 2, 6, 4),
                Padding = new Thickness(8, 4, 8, 4),
                Visibility = HasGps ? Visibility.Visible : Visibility.Collapsed
            };

            var gpsDock = new DockPanel();

            var btnMap = new Button
            {
                Content = "🗺 Open Map",
                FontSize = 10,
                Padding = new Thickness(6, 1, 6, 1),
                Background = ZeroWpfTheme.BgHover,
                Foreground = ZeroWpfTheme.PrimaryAccent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "Open coordinates in map viewer"
            };
            DockPanel.SetDock(btnMap, Dock.Right);
            btnMap.Click += BtnMap_Click;
            gpsDock.Children.Add(btnMap);

            _txtGps = new TextBlock
            {
                Text = GpsText,
                FontSize = 10,
                Foreground = ZeroWpfTheme.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            gpsDock.Children.Add(_txtGps);

            gpsBorder.Child = gpsDock;
            _gpsBorder = gpsBorder;
            root.Children.Add(gpsBorder);

            // 4. Detailed EXIF Section
            if (ShowDetailedExif)
            {
                var exifStack = new StackPanel();

                // Section header
                var headerBorder = new Border
                {
                    Background = ZeroWpfTheme.BgCard,
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(0, 4, 0, 2),
                    Cursor = Cursors.Hand
                };
                var headerDock = new DockPanel();
                var headerToggle = new TextBlock
                {
                    Text = IsDetailedExifExpanded ? "▾" : "▸",
                    FontSize = 10,
                    Foreground = ZeroWpfTheme.TextMuted,
                    Margin = new Thickness(0, 0, 6, 0)
                };
                DockPanel.SetDock(headerToggle, Dock.Left);
                headerDock.Children.Add(headerToggle);

                var headerText = new TextBlock
                {
                    Text = DetailedExifHeader,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = ZeroWpfTheme.TextSecondary
                };
                headerDock.Children.Add(headerText);
                headerBorder.Child = headerDock;

                headerBorder.MouseLeftButtonUp += (s, e) =>
                {
                    IsDetailedExifExpanded = !IsDetailedExifExpanded;
                    headerToggle.Text = IsDetailedExifExpanded ? "▾" : "▸";
                    if (_detailedExifContainer != null)
                    {
                        _detailedExifContainer.Visibility = IsDetailedExifExpanded ? Visibility.Visible : Visibility.Collapsed;
                    }
                };
                exifStack.Children.Add(headerBorder);

                // Table ItemsControl
                _icExif = new ItemsControl
                {
                    Margin = new Thickness(6, 4, 6, 4),
                    ItemsSource = _exifRows
                };

                // Item template for Name / Value
                var factory = new FrameworkElementFactory(typeof(Grid));
                factory.SetValue(Grid.MarginProperty, new Thickness(0, 2, 0, 2));

                var colDef1 = new FrameworkElementFactory(typeof(ColumnDefinition));
                colDef1.SetValue(ColumnDefinition.WidthProperty, new GridLength(100));
                factory.AppendChild(colDef1);

                var colDef2 = new FrameworkElementFactory(typeof(ColumnDefinition));
                colDef2.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
                factory.AppendChild(colDef2);

                var nameBlock = new FrameworkElementFactory(typeof(TextBlock));
                nameBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(ExifTelemetryItem.Name)));
                nameBlock.SetValue(TextBlock.FontSizeProperty, 10.0);
                nameBlock.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextSecondary);
                nameBlock.SetValue(Grid.ColumnProperty, 0);
                factory.AppendChild(nameBlock);

                var valBlock = new FrameworkElementFactory(typeof(TextBlock));
                valBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(nameof(ExifTelemetryItem.Value)));
                valBlock.SetValue(TextBlock.FontSizeProperty, 10.0);
                valBlock.SetValue(TextBlock.ForegroundProperty, ZeroWpfTheme.TextPrimary);
                valBlock.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
                valBlock.SetValue(Grid.ColumnProperty, 1);
                factory.AppendChild(valBlock);

                _icExif.ItemTemplate = new DataTemplate { VisualTree = factory };

                _detailedExifContainer = _icExif;
                _detailedExifContainer.Visibility = IsDetailedExifExpanded ? Visibility.Visible : Visibility.Collapsed;
                exifStack.Children.Add(_icExif);

                root.Children.Add(exifStack);
            }

            AttachVisualChild(root);
            UpdateTelemetryLabels();
            UpdateGpsSection();
        }

        private FrameworkElement CreateHudGauge(string caption, out TextBlock valBlock)
        {
            var border = new Border
            {
                Margin = new Thickness(2),
                Padding = new Thickness(4, 3, 4, 3),
                Background = ZeroWpfTheme.BgCard,
                CornerRadius = new CornerRadius(3),
                BorderBrush = ZeroWpfTheme.BorderSubtle,
                BorderThickness = new Thickness(0.5)
            };

            var stack = new StackPanel();

            var capBlock = new TextBlock
            {
                Text = caption,
                FontSize = 8,
                FontWeight = FontWeights.Medium,
                Foreground = ZeroWpfTheme.TextMuted,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stack.Children.Add(capBlock);

            valBlock = new TextBlock
            {
                Text = "--",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = ZeroWpfTheme.PrimaryAccent,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            };
            stack.Children.Add(valBlock);

            border.Child = stack;
            return border;
        }

        private void UpdateTelemetryLabels()
        {
            if (_txtShutter != null) _txtShutter.Text = !string.IsNullOrWhiteSpace(ShutterSpeed) ? ShutterSpeed : "--";
            if (_txtAperture != null) _txtAperture.Text = !string.IsNullOrWhiteSpace(Aperture) ? Aperture : "--";
            if (_txtIso != null) _txtIso.Text = !string.IsNullOrWhiteSpace(Iso) ? Iso : "--";
            if (_txtFocal != null) _txtFocal.Text = !string.IsNullOrWhiteSpace(FocalLength) ? FocalLength : "--";

            if (_txtCamera != null)
            {
                _txtCamera.Text = CameraModel;
                _txtCamera.Visibility = !string.IsNullOrWhiteSpace(CameraModel) ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_txtLens != null)
            {
                _txtLens.Text = LensModel;
                _txtLens.Visibility = !string.IsNullOrWhiteSpace(LensModel) ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_txtDateTime != null)
            {
                _txtDateTime.Text = CaptureDateTime;
                _txtDateTime.Visibility = !string.IsNullOrWhiteSpace(CaptureDateTime) ? Visibility.Visible : Visibility.Collapsed;
            }

            if (_hudGrid != null)
            {
                bool hasAnyHud = !string.IsNullOrWhiteSpace(ShutterSpeed) ||
                                 !string.IsNullOrWhiteSpace(Aperture) ||
                                 !string.IsNullOrWhiteSpace(Iso) ||
                                 !string.IsNullOrWhiteSpace(FocalLength);
                _hudGrid.Visibility = hasAnyHud ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateGpsSection()
        {
            if (_gpsBorder != null)
            {
                _gpsBorder.Visibility = HasGps ? Visibility.Visible : Visibility.Collapsed;
            }
            if (_txtGps != null)
            {
                _txtGps.Text = GpsText;
            }
        }

        private void BtnMap_Click(object sender, RoutedEventArgs e)
        {
            if (!HasGps || !GpsLatitude.HasValue || !GpsLongitude.HasValue) return;

            var lat = GpsLatitude.Value;
            var lon = GpsLongitude.Value;

            var args = new GpsMapRequestedEventArgs(lat, lon);
            GpsMapRequested?.Invoke(this, args);

            if (!args.Handled && AutoLaunchMapOnGpsClick)
            {
                try
                {
                    var url = $"https://www.google.com/maps?q={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                }
                catch { /* Ignored */ }
            }
        }

        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZExifTelemetryCard"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ExifTelemetryCard is deprecated. Use ZExifTelemetryCard instead.")]
    public class ExifTelemetryCard : ZExifTelemetryCard
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZExifTelemetryCard"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroExifTelemetryCard is deprecated. Use ZExifTelemetryCard instead.")]
    public class ZeroExifTelemetryCard : ZExifTelemetryCard
    {
    }
    #endregion
}
