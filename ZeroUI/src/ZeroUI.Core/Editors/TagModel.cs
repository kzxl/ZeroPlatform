using System;

namespace ZeroUI.Core.Editors
{
    /// <summary>
    /// Semantic classification status type for tags and status badges.
    /// </summary>
    public enum TagType
    {
        Default,
        Success,
        Processing,
        Warning,
        Error
    }

    /// <summary>
    /// Backward-compatibility alias for <see cref="TagType"/>.
    /// </summary>
    [Obsolete("ZeroTagType is deprecated. Use TagType instead.")]
    public enum ZeroTagType
    {
        Default,
        Success,
        Processing,
        Warning,
        Error
    }
}
