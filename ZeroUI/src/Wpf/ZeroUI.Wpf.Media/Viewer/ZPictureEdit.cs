using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZeroUI.Core.Editors;

namespace ZeroUI.Wpf.Media
{

    /// <summary>
    /// Modern anti-aliased image and avatar control for ZeroUI.Wpf.
    /// Supports rounded borders, circular avatars, initials fallback, operator online/offline status dots,
    /// drag-drop file loading, clipboard copy/paste, and click-to-zoom Lightbox preview.
    /// </summary>
    public class ZPictureEdit : Control, IZeroEditor
    {
        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register(
                nameof(ImageSource),
                typeof(ImageSource),
                typeof(ZPictureEdit),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnImageSourceChanged));

        public static readonly DependencyProperty EditValueProperty =
            DependencyProperty.Register(
                nameof(EditValue),
                typeof(object),
                typeof(ZPictureEdit),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnEditValueChanged));

        public static readonly DependencyProperty ScaleModeProperty =
            DependencyProperty.Register(
                nameof(ScaleMode),
                typeof(ImageScaleMode),
                typeof(ZPictureEdit),
                new PropertyMetadata(ImageScaleMode.Cover));

        public static readonly DependencyProperty IsCircleProperty =
            DependencyProperty.Register(
                nameof(IsCircle),
                typeof(bool),
                typeof(ZPictureEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(ZPictureEdit),
                new PropertyMetadata(new CornerRadius(8)));

        public static readonly DependencyProperty FallbackTextProperty =
            DependencyProperty.Register(
                nameof(FallbackText),
                typeof(string),
                typeof(ZPictureEdit),
                new PropertyMetadata(null, OnFallbackTextChanged));

        private static void OnFallbackTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZPictureEdit pe && e.NewValue is string text && !string.IsNullOrWhiteSpace(text))
            {
                if (pe.FallbackBackground == null)
                {
                    try
                    {
                        string colorHex = AvatarHelper.GetDeterministicColorHex(text);
                        pe.SetCurrentValue(FallbackBackgroundProperty, (Brush)new BrushConverter().ConvertFromString(colorHex)!);
                    }
                    catch { }
                }
            }
        }

        public static readonly DependencyProperty FallbackBackgroundProperty =
            DependencyProperty.Register(
                nameof(FallbackBackground),
                typeof(Brush),
                typeof(ZPictureEdit),
                new PropertyMetadata(null));

        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(
                nameof(Status),
                typeof(AvatarStatus),
                typeof(ZPictureEdit),
                new PropertyMetadata(AvatarStatus.None));

        public static readonly DependencyProperty EnableZoomPreviewProperty =
            DependencyProperty.Register(
                nameof(EnableZoomPreview),
                typeof(bool),
                typeof(ZPictureEdit),
                new PropertyMetadata(true));

        public static readonly DependencyProperty IsModifiedProperty =
            DependencyProperty.Register(
                nameof(IsModified),
                typeof(bool),
                typeof(ZPictureEdit),
                new PropertyMetadata(false));

        public static readonly DependencyProperty ReadOnlyProperty =
            DependencyProperty.Register(
                nameof(ReadOnly),
                typeof(bool),
                typeof(ZPictureEdit),
                new PropertyMetadata(false));

        public event EventHandler? EditValueChanged;
        public event EventHandler? ImageClicked;

        public ImageSource? ImageSource
        {
            get => (ImageSource?)GetValue(ImageSourceProperty);
            set => SetValue(ImageSourceProperty, value);
        }

        public object? EditValue
        {
            get => GetValue(EditValueProperty);
            set => SetValue(EditValueProperty, value);
        }

        public ImageScaleMode ScaleMode
        {
            get => (ImageScaleMode)GetValue(ScaleModeProperty);
            set => SetValue(ScaleModeProperty, value);
        }

        public bool IsCircle
        {
            get => (bool)GetValue(IsCircleProperty);
            set => SetValue(IsCircleProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public string? FallbackText
        {
            get => (string?)GetValue(FallbackTextProperty);
            set => SetValue(FallbackTextProperty, value);
        }

        public Brush? FallbackBackground
        {
            get => (Brush?)GetValue(FallbackBackgroundProperty);
            set => SetValue(FallbackBackgroundProperty, value);
        }

        public AvatarStatus Status
        {
            get => (AvatarStatus)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public bool EnableZoomPreview
        {
            get => (bool)GetValue(EnableZoomPreviewProperty);
            set => SetValue(EnableZoomPreviewProperty, value);
        }

        public bool IsModified
        {
            get => (bool)GetValue(IsModifiedProperty);
            set => SetValue(IsModifiedProperty, value);
        }

        public bool ReadOnly
        {
            get => (bool)GetValue(ReadOnlyProperty);
            set => SetValue(ReadOnlyProperty, value);
        }

        static ZPictureEdit()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ZPictureEdit), new FrameworkPropertyMetadata(typeof(ZPictureEdit)));
        }

        public ZPictureEdit()
        {
            AllowDrop = true;
            Cursor = Cursors.Hand;

            Drop += OnDrop;
            MouseLeftButtonUp += OnMouseLeftButtonUp;

            CreateContextMenu();
        }

        private static void OnImageSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ZPictureEdit pe)
            {
                pe.EditValue = e.NewValue;
                pe.IsModified = true;
                pe.EditValueChanged?.Invoke(pe, EventArgs.Empty);
            }
        }

        private static void OnEditValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ZPictureEdit pe) return;

            if (e.NewValue is ImageSource img)
            {
                if (pe.ImageSource != img) pe.ImageSource = img;
            }
            else if (e.NewValue is string path && File.Exists(path))
            {
                pe.LoadImage(path);
            }
            else if (e.NewValue == null)
            {
                pe.ImageSource = null;
            }
        }

        public void LoadImage(string filePath)
        {
            if (!File.Exists(filePath)) return;

            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(filePath, UriKind.RelativeOrAbsolute);
                bmp.EndInit();
                bmp.Freeze();

                ImageSource = bmp;
            }
            catch
            {
                // Non-fatal image loading failure
            }
        }

        public void Clear()
        {
            ImageSource = null;
            EditValue = null;
            IsModified = false;
        }

        public void Reset()
        {
            Clear();
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (ReadOnly) return;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    LoadImage(files[0]);
                    e.Handled = true;
                }
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            ImageClicked?.Invoke(this, EventArgs.Empty);

            if (EnableZoomPreview && ImageSource != null)
            {
                ShowLightboxPreview();
            }
        }

        private void ShowLightboxPreview()
        {
            if (ImageSource == null) return;

            var window = new Window
            {
                Title = "Preview",
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Width = 720,
                Height = 540,
                Background = new SolidColorBrush(Color.FromArgb(235, 15, 23, 42)),
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true
            };

            var grid = new Grid();
            var img = new Image
            {
                Source = ImageSource,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(32)
            };

            var closeBtn = new Button
            {
                Content = "✕",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 16, 16, 0),
                Width = 32,
                Height = 32,
                Cursor = Cursors.Hand,
                Foreground = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                BorderThickness = new Thickness(0)
            };
            closeBtn.Click += (s, e) => window.Close();

            grid.Children.Add(img);
            grid.Children.Add(closeBtn);
            grid.MouseDown += (s, e) => { if (e.ChangedButton == MouseButton.Left) window.Close(); };

            window.Content = grid;
            window.ShowDialog();
        }

        private void CreateContextMenu()
        {
            var cm = new ContextMenu();

            var miLoad = new MenuItem { Header = "📂 Load Image..." };
            miLoad.Click += (s, e) =>
            {
                if (ReadOnly) return;
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Image Files (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All Files (*.*)|*.*",
                    Title = "Select Image"
                };
                if (dlg.ShowDialog() == true)
                {
                    LoadImage(dlg.FileName);
                }
            };

            var miSave = new MenuItem { Header = "💾 Save Image As..." };
            miSave.Click += (s, e) =>
            {
                if (ImageSource is not BitmapSource bs) return;
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg",
                    FileName = "image.png"
                };
                if (dlg.ShowDialog() == true)
                {
                    using var stream = File.OpenWrite(dlg.FileName);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bs));
                    encoder.Save(stream);
                }
            };

            var miCopy = new MenuItem { Header = "📋 Copy" };
            miCopy.Click += (s, e) =>
            {
                if (ImageSource is BitmapSource bs) Clipboard.SetImage(bs);
            };

            var miPaste = new MenuItem { Header = "📥 Paste" };
            miPaste.Click += (s, e) =>
            {
                if (ReadOnly) return;
                if (Clipboard.ContainsImage())
                {
                    ImageSource = Clipboard.GetImage();
                }
            };

            var miClear = new MenuItem { Header = "🗑️ Clear" };
            miClear.Click += (s, e) =>
            {
                if (!ReadOnly) Clear();
            };

            cm.Items.Add(miLoad);
            cm.Items.Add(miSave);
            cm.Items.Add(new Separator());
            cm.Items.Add(miCopy);
            cm.Items.Add(miPaste);
            cm.Items.Add(new Separator());
            cm.Items.Add(miClear);

            ContextMenu = cm;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZPictureEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("PictureEdit is deprecated. Use ZPictureEdit instead.")]
    public class PictureEdit : ZPictureEdit
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZPictureEdit"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroPictureEdit is deprecated. Use ZPictureEdit instead.")]
    public class ZeroPictureEdit : ZPictureEdit
    {
    }
    #endregion
}
