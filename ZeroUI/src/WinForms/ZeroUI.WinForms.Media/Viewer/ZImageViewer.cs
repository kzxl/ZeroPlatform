using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Media;
using ZeroUI.WinForms.Base;

namespace ZeroUI.WinForms.Media
{
    /// <summary>
    /// Enterprise GPU-accelerated interactive image viewer for Windows Forms.
    /// Delivers sub-pixel smooth pan, cursor-anchored zoom, forensic pixel grid,
    /// interactive MiniMap navigator, and telemetry color probe HUD.
    /// Supports GDI+ <see cref="Image"/>, <see cref="Bitmap"/>, files, and ZeroGraphics <c>ImageBuffer</c>.
    /// </summary>
    [ToolboxItem(true)]
    [Category("ZeroUI - Media")]
    [DefaultProperty("Image")]
    [Description("Enterprise high-performance interactive image viewer control")]
    [Designer("ZeroUI.WinForms.Design.Media.ZImageViewerDesigner, ZeroUI.WinForms.Design")]
    public partial class ZImageViewer : ControlBase, IZeroEditor
    {
        private Image? _image;
        private string? _filePath;
        private ImageViewMode _viewMode = ImageViewMode.FitToWindow;
        private float _zoomFactor = 1.0f;
        private float _minZoom = 0.01f;
        private float _maxZoom = 64.0f;
        private PointF _panOffset = PointF.Empty;
        private ImageRotationAngle _rotation = ImageRotationAngle.Rotate0;
        private ImageFlipMode _flip = ImageFlipMode.None;
        private ImageInterpolationQuality _interpolation = ImageInterpolationQuality.HighQuality;
        private bool _showPixelGrid = true;
        private float _pixelGridThreshold = 8.0f;
        private bool _showMiniMap = true;
        private bool _showToolbar = true;
        private bool _showStatusBar = true;
        private Color _canvasColor = Color.FromArgb(15, 18, 24);

        private bool _isPanning;
        private Point _panStartMouse;
        private PointF _panStartOffset;
        private bool _isDraggingMiniMap;
        private Point _currentMousePosition = new Point(-1, -1);
        private Point _currentPixelCoord = new Point(-1, -1);
        private Color _currentHoverColor = Color.Transparent;
        private bool _isModified;
        private bool _isReadOnly;

        public ZImageViewer()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);

            DoubleBuffered = true;
            BackColor = _canvasColor;
            Size = new Size(400, 300);
        }

        #region Properties

        [Category("ZeroUI Media")]
        [DefaultValue(null)]
        public Image? Image
        {
            get => _image;
            set
            {
                if (_image != value)
                {
                    _image = value;
                    if (_viewMode == ImageViewMode.FitToWindow) FitToWindow();
                    else Invalidate();
                    ImageLoaded?.Invoke(this, EventArgs.Empty);
                    EditValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        [Category("ZeroUI Media")]
        [DefaultValue(null)]
        public string? FilePath
        {
            get => _filePath;
            set
            {
                if (_filePath != value)
                {
                    _filePath = value;
                    if (!string.IsNullOrEmpty(value) && File.Exists(value))
                    {
                        LoadFromFile(value!);
                    }
                }
            }
        }

        [Category("ZeroUI Media")]
        [DefaultValue(ImageViewMode.FitToWindow)]
        public ImageViewMode ViewMode
        {
            get => _viewMode;
            set
            {
                _viewMode = value;
                switch (value)
                {
                    case ImageViewMode.FitToWindow: FitToWindow(); break;
                    case ImageViewMode.FitWidth: FitWidth(); break;
                    case ImageViewMode.FitHeight: FitHeight(); break;
                    case ImageViewMode.OriginalSize: ActualSize(); break;
                    default: Invalidate(); break;
                }
            }
        }

        [Category("ZeroUI Media")]
        [DefaultValue(1.0f)]
        public float ZoomFactor
        {
            get => _zoomFactor;
            set
            {
                float clamped = Math.Max(_minZoom, Math.Min(_maxZoom, value));
                if (Math.Abs(_zoomFactor - clamped) > 0.0001f)
                {
                    _zoomFactor = clamped;
                    ZoomChanged?.Invoke(this, clamped);
                    Invalidate();
                }
            }
        }

        [Category("ZeroUI Media")]
        [DefaultValue(0.01f)]
        public float MinZoom
        {
            get => _minZoom;
            set => _minZoom = Math.Max(0.001f, value);
        }

        [Category("ZeroUI Media")]
        [DefaultValue(64.0f)]
        public float MaxZoom
        {
            get => _maxZoom;
            set => _maxZoom = Math.Max(_minZoom, value);
        }

        [Category("ZeroUI Media")]
        public PointF PanOffset
        {
            get => _panOffset;
            set
            {
                _panOffset = value;
                PanChanged?.Invoke(this, value);
                Invalidate();
            }
        }

        [Category("ZeroUI Media")]
        [DefaultValue(ImageRotationAngle.Rotate0)]
        public ImageRotationAngle Rotation
        {
            get => _rotation;
            set { _rotation = value; Invalidate(); }
        }

        [Category("ZeroUI Media")]
        [DefaultValue(ImageFlipMode.None)]
        public ImageFlipMode Flip
        {
            get => _flip;
            set { _flip = value; Invalidate(); }
        }

        [Category("ZeroUI Media")]
        [DefaultValue(ImageInterpolationQuality.HighQuality)]
        public ImageInterpolationQuality InterpolationQuality
        {
            get => _interpolation;
            set { _interpolation = value; Invalidate(); }
        }

        [Category("ZeroUI Media")]
        [DefaultValue(true)]
        public bool ShowPixelGrid
        {
            get => _showPixelGrid;
            set { _showPixelGrid = value; Invalidate(); }
        }

        [Category("ZeroUI Media")]
        [DefaultValue(8.0f)]
        public float PixelGridThreshold
        {
            get => _pixelGridThreshold;
            set { _pixelGridThreshold = value; Invalidate(); }
        }

        [Category("ZeroUI Media")]
        [DefaultValue(true)]
        public bool ShowMiniMap
        {
            get => _showMiniMap;
            set { _showMiniMap = value; Invalidate(); }
        }

        [Category("ZeroUI Media")]
        [DefaultValue(true)]
        public bool ShowToolbar
        {
            get => _showToolbar;
            set { _showToolbar = value; Invalidate(); }
        }

        [Category("ZeroUI Media")]
        [DefaultValue(true)]
        public bool ShowStatusBar
        {
            get => _showStatusBar;
            set { _showStatusBar = value; Invalidate(); }
        }

        [Category("ZeroUI Media")]
        public Color CanvasColor
        {
            get => _canvasColor;
            set { _canvasColor = value; BackColor = value; Invalidate(); }
        }

        public int PixelWidth => _image?.Width ?? 0;
        public int PixelHeight => _image?.Height ?? 0;

        #endregion

        #region Events

        public event EventHandler<float>? ZoomChanged;
        public event EventHandler<PointF>? PanChanged;
        public event EventHandler<(int X, int Y, Color Color)>? HoverPixelChanged;
        public event EventHandler? ImageLoaded;

        #endregion

        #region IZeroEditor Implementation

        [Browsable(false)]
        public object? EditValue
        {
            get => _image;
            set
            {
                if (value is Image img) Image = img;
                else if (value is string path && File.Exists(path)) LoadFromFile(path);
                else if (value == null) Image = null;
            }
        }

        public event EventHandler? EditValueChanged;

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool IsModified
        {
            get => _isModified;
            set => _isModified = value;
        }

        [Category("Behavior")]
        [DefaultValue(false)]
        public bool ReadOnly
        {
            get => _isReadOnly;
            set => _isReadOnly = value;
        }

        public void Reset()
        {
            ResetView();
            IsModified = false;
        }

        public void Clear()
        {
            Image?.Dispose();
            Image = null;
            FilePath = null;
            PanOffset = PointF.Empty;
            ZoomFactor = 1.0f;
            IsModified = false;
            Invalidate();
        }

        #endregion
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Legacy alias for <see cref="ZImageViewer"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ImageViewerControl is deprecated and will be removed in 5 release cycles. Please migrate to ZImageViewer instead.")]
    [ToolboxItem(false)]
    public class ImageViewerControl : ZImageViewer
    {
    }

    /// <summary>
    /// Legacy alias for <see cref="ZImageViewer"/>.
    /// </summary>
    [Obsolete("ZeroImageViewer is deprecated. Please use ZImageViewer instead.")]
    [ToolboxItem(false)]
    public class ZeroImageViewer : ZImageViewer
    {
    }

    #endregion
}
