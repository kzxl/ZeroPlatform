using System.Collections.Generic;

namespace ZeroUI.Core.Icons
{
    /// <summary>
    /// Contract for an external or custom icon source providing resolution and metadata for icon keys.
    /// Enables pluggable icon packs, whitelabel branding, and dynamic icon asset catalogs.
    /// </summary>
    public interface IZeroIconProvider
    {
        /// <summary>
        /// Gets the unique identifier or brand name of this icon provider (e.g. "DefaultVector", "EnterprisePNG", "CustomBrand").
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Checks whether this provider contains an icon definition for the specified key.
        /// </summary>
        /// <param name="key">Icon identifier or key name.</param>
        /// <returns>True if available, false otherwise.</returns>
        bool HasIcon(string key);

        /// <summary>
        /// Gets all icon keys explicitly supported or provided by this source.
        /// </summary>
        IReadOnlyCollection<string> AvailableKeys { get; }
    }
}
