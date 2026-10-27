using System;

namespace ZeroUI.Core.Theme
{
    /// <summary>
    /// Contract for controls and visual elements that support dynamic, local, or global theme styling.
    /// Provides zero-overhead skin synchronization across WinForms and WPF presentation layers.
    /// </summary>
    public interface IZeroSkinnable
    {
        /// <summary>
        /// Gets or sets a value indicating whether this control uses the global <see cref="ZeroSkinManager.CurrentSkin"/>.
        /// Default is <c>true</c>. When set to <c>false</c>, the control consumes <see cref="CustomSkin"/>.
        /// </summary>
        bool UseDefaultSkin { get; set; }

        /// <summary>
        /// Gets or sets a localized skin override for this control, enabling scoped dark/light views.
        /// Only effective when <see cref="UseDefaultSkin"/> is <c>false</c>.
        /// </summary>
        ZeroSkin? CustomSkin { get; set; }

        /// <summary>
        /// Gets the currently resolved active skin (either <see cref="CustomSkin"/> or <see cref="ZeroSkinManager.CurrentSkin"/>).
        /// </summary>
        ZeroSkin EffectiveSkin { get; }

        /// <summary>
        /// Applies the specified skin to this control and its internal elements.
        /// </summary>
        /// <param name="skin">The skin containing target visual tokens.</param>
        void ApplySkin(ZeroSkin skin);
    }
}
