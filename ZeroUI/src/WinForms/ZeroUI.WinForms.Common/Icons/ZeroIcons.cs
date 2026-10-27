using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using ZeroUI.Core.Icons;
using ZeroUI.WinForms.Theme;

namespace ZeroUI.WinForms.Icons
{
    /// <summary>
    /// Marker class used by <see cref="System.Drawing.ToolboxBitmapAttribute"/> to locate embedded Toolbox icon bitmaps.
    /// Retained for backward compatibility with component designers.
    /// </summary>
    public static class ZeroIcons
    {
    }

    /// <summary>
    /// Enterprise-grade, centralized Icon System for ZeroUI WinForms.
    /// Acts as a Single Source of Truth for icons with built-in crisp High-DPI vector rendering,
    /// dynamic theme-reactive color tinting, zero-leak caching, and runtime icon pack override capabilities.
    /// </summary>
    public static class ZeroIcon
    {
        private static readonly ConcurrentDictionary<string, Image> _customImages = new ConcurrentDictionary<string, Image>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, Action<Graphics, RectangleF, Color>> _customPainters = new ConcurrentDictionary<string, Action<Graphics, RectangleF, Color>>(StringComparer.OrdinalIgnoreCase);
        private static IZeroIconProvider? _customProvider;

        #region Public Retrieval Methods

        /// <summary>
        /// Retrieves an icon as an <see cref="Image"/> scaled to the specified size (default 16x16).
        /// Cached automatically in memory for instant reuse.
        /// </summary>
        public static Image Get(IconKey key, int size = 16, Color? color = null)
        {
            return GetBitmap(key.ToKeyString(), size, color);
        }

        /// <summary>
        /// Retrieves an icon by key name as an <see cref="Image"/> scaled to the specified size.
        /// Supports both standard and custom registered keys.
        /// </summary>
        public static Image Get(string key, int size = 16, Color? color = null)
        {
            return GetBitmap(key, size, color);
        }

        /// <summary>
        /// Retrieves an icon as a strongly typed <see cref="Bitmap"/> scaled to the specified size.
        /// </summary>
        public static Bitmap GetBitmap(IconKey key, int size = 16, Color? color = null)
        {
            return GetBitmap(key.ToKeyString(), size, color);
        }

        /// <summary>
        /// Retrieves an icon by key name as a strongly typed <see cref="Bitmap"/> scaled to the specified size.
        /// </summary>
        public static Bitmap GetBitmap(string key, int size = 16, Color? color = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                key = IconKey.Document.ToString();
            }

            int validSize = Math.Max(8, Math.Min(512, size));
            Color tintColor = color ?? ZeroTheme.Colors.TextPrimary;

            return ZeroIconCache.GetOrCreate(key, validSize, tintColor, () => GenerateBitmap(key, validSize, tintColor));
        }

        /// <summary>
        /// Draws the specified icon directly onto the destination graphics surface.
        /// Zero allocation when using built-in vector rendering or cached images.
        /// </summary>
        public static void Draw(Graphics g, IconKey key, Rectangle rect, Color? color = null)
        {
            Draw(g, key.ToKeyString(), new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), color);
        }

        /// <summary>
        /// Draws the specified icon directly onto the destination graphics surface with floating-point precision.
        /// </summary>
        public static void Draw(Graphics g, IconKey key, RectangleF rect, Color? color = null)
        {
            Draw(g, key.ToKeyString(), rect, color);
        }

        /// <summary>
        /// Draws the icon specified by key name directly onto the destination graphics surface.
        /// </summary>
        public static void Draw(Graphics g, string key, RectangleF rect, Color? color = null)
        {
            if (g == null || rect.Width <= 1 || rect.Height <= 1) return;

            Color tintColor = color ?? ZeroTheme.Colors.TextPrimary;

            // 1. Check custom painter
            if (_customPainters.TryGetValue(key, out var painter))
            {
                painter(g, rect, tintColor);
                return;
            }

            // 2. Check custom image
            if (_customImages.TryGetValue(key, out var img))
            {
                var oldInterpolation = g.InterpolationMode;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(img, rect);
                g.InterpolationMode = oldInterpolation;
                return;
            }

            // 3. Fallback to Vector Icon Renderer if valid key
            if (IconKeyExtensions.TryParseKey(key, out var enumKey))
            {
                ZeroVectorIcons.Draw(g, enumKey, rect, tintColor);
                return;
            }

            // 4. Default fallback
            ZeroVectorIcons.Draw(g, IconKey.Document, rect, tintColor);
        }

        #endregion

        #region Registration & Customization

        /// <summary>
        /// Registers a custom image override for a standard icon key.
        /// All future requests for this key will return this image.
        /// </summary>
        public static void Register(IconKey key, Image customImage)
        {
            Register(key.ToKeyString(), customImage);
        }

        /// <summary>
        /// Registers a custom image override for an icon key name.
        /// </summary>
        public static void Register(string key, Image customImage)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            if (customImage == null) throw new ArgumentNullException(nameof(customImage));

            _customImages[key] = customImage;
            ZeroIconCache.Clear();
        }

        /// <summary>
        /// Registers a custom programmatic vector drawing routine for an icon key.
        /// </summary>
        public static void Register(string key, Action<Graphics, RectangleF, Color> customPainter)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            if (customPainter == null) throw new ArgumentNullException(nameof(customPainter));

            _customPainters[key] = customPainter;
            ZeroIconCache.Clear();
        }

        /// <summary>
        /// Removes a custom registered icon override.
        /// </summary>
        public static bool Unregister(IconKey key)
        {
            return Unregister(key.ToKeyString());
        }

        /// <summary>
        /// Removes a custom registered icon override by key name.
        /// </summary>
        public static bool Unregister(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            bool removedImg = _customImages.TryRemove(key, out _);
            bool removedPainter = _customPainters.TryRemove(key, out _);
            if (removedImg || removedPainter)
            {
                ZeroIconCache.Clear();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Scans a local directory and automatically registers all image files matching icon key names.
        /// Supports PNG, BMP, ICO, and JPEG files (e.g. "Save.png", "align-left.png", "custom_sensor.ico").
        /// </summary>
        /// <param name="folderPath">Directory path containing the icon image files.</param>
        /// <param name="overwrite">Whether to overwrite existing custom registrations.</param>
        /// <returns>The total number of icon files successfully loaded and registered.</returns>
        public static int LoadFromFolder(string folderPath, bool overwrite = true)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return 0;
            }

            int loadedCount = 0;
            string[] supportedExtensions = { "*.png", "*.bmp", "*.ico", "*.jpg", "*.jpeg" };

            foreach (var ext in supportedExtensions)
            {
                var files = Directory.GetFiles(folderPath, ext, SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    try
                    {
                        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(file);
                        string keyName = fileNameWithoutExt;

                        if (IconKeyExtensions.TryParseKey(fileNameWithoutExt, out var parsedKey))
                        {
                            keyName = parsedKey.ToKeyString();
                        }

                        if (!overwrite && _customImages.ContainsKey(keyName))
                        {
                            continue;
                        }

                        // Load safely without locking file on disk
                        using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using var rawImg = Image.FromStream(stream);
                            var clonedImg = new Bitmap(rawImg);
                            _customImages[keyName] = clonedImg;
                            loadedCount++;
                        }
                    }
                    catch
                    {
                        // Skip corrupted or unreadable images
                    }
                }
            }

            if (loadedCount > 0)
            {
                ZeroIconCache.Clear();
            }

            return loadedCount;
        }

        /// <summary>
        /// Sets an external custom icon provider.
        /// </summary>
        public static void SetCustomProvider(IZeroIconProvider? provider)
        {
            _customProvider = provider;
            ZeroIconCache.Clear();
        }

        /// <summary>
        /// Disposes all cached bitmaps in memory.
        /// </summary>
        public static void ClearCache()
        {
            ZeroIconCache.Clear();
        }

        /// <summary>
        /// Resets the icon system back to default vector rendering, clearing all custom registrations and caches.
        /// </summary>
        public static void ResetToDefaults()
        {
            foreach (var kv in _customImages)
            {
                try { kv.Value?.Dispose(); } catch { }
            }
            _customImages.Clear();
            _customPainters.Clear();
            _customProvider = null;
            ZeroIconCache.Clear();
        }

        #endregion

        #region Internal Bitmap Generation

        private static Bitmap GenerateBitmap(string key, int size, Color tintColor)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.Clear(Color.Transparent);

                var bounds = new RectangleF(0, 0, size, size);

                // 1. Custom Image
                if (_customImages.TryGetValue(key, out var img))
                {
                    g.DrawImage(img, bounds);
                    return bmp;
                }

                // 2. Custom Painter
                if (_customPainters.TryGetValue(key, out var painter))
                {
                    painter(g, bounds, tintColor);
                    return bmp;
                }

                // 3. Vector Icon Drawing
                if (IconKeyExtensions.TryParseKey(key, out var enumKey))
                {
                    ZeroVectorIcons.Draw(g, enumKey, bounds, tintColor);
                    return bmp;
                }

                // 4. Default Fallback
                ZeroVectorIcons.Draw(g, IconKey.Document, bounds, tintColor);
            }

            return bmp;
        }

        #endregion
    }
}
