using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Theme
{
    /// <summary>
    /// Central Skin & Palette Manager for ZeroUI.
    /// Manages the catalog of skins, runtime skin registration, JSON persistence,
    /// and cross-platform notification dispatch (WPF, WinForms, Blazor).
    /// </summary>
    public static class ZeroSkinManager
    {
        private static readonly Dictionary<string, ZeroSkin> _skins = new Dictionary<string, ZeroSkin>(StringComparer.OrdinalIgnoreCase);
        private static ZeroSkin _currentSkin = ZeroSkinDefaults.CleanLight;
        private static readonly object _syncLock = new object();

        /// <summary>
        /// Fires when the active skin is changed.
        /// Subscribed by platform presentation layers (ZeroWpfTheme, ZeroTheme).
        /// </summary>
        public static event Action<ZeroSkin>? SkinChanged;

        /// <summary>
        /// Fires when skins are added, imported, or removed.
        /// Subscribed by UI Galleries / ComboBox selectors.
        /// </summary>
        public static event Action? RegistryChanged;

        public static IReadOnlyCollection<ZeroSkin> AvailableSkins
        {
            get
            {
                lock (_syncLock)
                {
                    var list = new List<ZeroSkin>(_skins.Values);
                    return list.AsReadOnly();
                }
            }
        }

        public static ZeroSkin CurrentSkin => _currentSkin;

        static ZeroSkinManager()
        {
            ResetToDefaults();
        }

        public static void ResetToDefaults()
        {
            lock (_syncLock)
            {
                _skins.Clear();
                foreach (var skin in ZeroSkinDefaults.GetAllDefaults())
                {
                    _skins[skin.Name] = skin;
                }
            }
            RegistryChanged?.Invoke();
        }

        public static void RegisterSkin(ZeroSkin skin)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));
            if (string.IsNullOrWhiteSpace(skin.Name)) throw new ArgumentException("Skin name cannot be empty.", nameof(skin));

            lock (_syncLock)
            {
                _skins[skin.Name] = skin;
            }
            RegistryChanged?.Invoke();
        }

        public static bool UnregisterSkin(string skinName)
        {
            if (string.IsNullOrWhiteSpace(skinName)) return false;

            bool removed;
            lock (_syncLock)
            {
                removed = _skins.Remove(skinName);
            }
            if (removed)
            {
                RegistryChanged?.Invoke();
            }
            return removed;
        }

        public static ZeroSkin? GetSkin(string skinName)
        {
            if (string.IsNullOrWhiteSpace(skinName)) return null;

            lock (_syncLock)
            {
                return _skins.TryGetValue(skinName, out var skin) ? skin : null;
            }
        }

        public static void ApplySkin(string skinName)
        {
            var skin = GetSkin(skinName);
            if (skin == null)
            {
                throw new KeyNotFoundException($"ZeroSkin '{skinName}' is not registered in ZeroSkinManager.");
            }
            ApplySkin(skin);
        }

        public static void ApplySkin(ZeroSkin skin)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));

            lock (_syncLock)
            {
                _currentSkin = skin;
                // Automatically ensure registered
                _skins[skin.Name] = skin;
            }

            SkinChanged?.Invoke(skin);
        }

        public static ZeroSkin LoadSkinFromFile(string filePath, bool autoApply = false)
        {
            var skin = ZeroSkinSerializer.LoadFromFile(filePath);
            RegisterSkin(skin);
            if (autoApply)
            {
                ApplySkin(skin);
            }
            return skin;
        }

        public static void ExportSkinToFile(string skinName, string filePath)
        {
            var skin = GetSkin(skinName);
            if (skin == null)
            {
                throw new KeyNotFoundException($"ZeroSkin '{skinName}' is not registered in ZeroSkinManager.");
            }
            ZeroSkinSerializer.SaveToFile(skin, filePath);
        }

        /// <summary>
        /// Resolves the effective skin for an <see cref="IZeroSkinnable"/> element,
        /// returning its custom skin if local override is enabled, or falling back to <see cref="CurrentSkin"/>.
        /// </summary>
        public static ZeroSkin ResolveSkin(IZeroSkinnable? skinnable)
        {
            if (skinnable != null && !skinnable.UseDefaultSkin && skinnable.CustomSkin != null)
            {
                return skinnable.CustomSkin;
            }
            return _currentSkin;
        }
    }
}
