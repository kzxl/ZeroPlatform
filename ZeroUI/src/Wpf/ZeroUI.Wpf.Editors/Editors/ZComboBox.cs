using System;
using System.Windows.Controls;
using ZeroUI.Wpf.Theme;

namespace ZeroUI.Wpf.Editors
{
    /// <summary>
    /// Modern styled ComboBox adhering to AgentOption WPF UI standards.
    /// Provides dark/light theme aware dropdown popups with high-contrast text and selections.
    /// </summary>
    public class ZComboBox : ComboBox
    {
        public ZComboBox()
        {
            Style = ZeroWpfStyles.ComboBoxStyle;
            ItemContainerStyle = ZeroWpfStyles.ComboBoxItemStyle;
        }
    }

    #region Backward Compatibility Shims (5-Release Deprecation Policy)

    /// <summary>
    /// Convenience alias for <see cref="ZComboBox"/>.
    /// </summary>
    public class ZComboBoxEdit : ZComboBox { }

    /// <summary>
    /// Legacy alias for <see cref="ZComboBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ComboBoxEdit is deprecated and will be removed in 5 release cycles. Please migrate to ZComboBox instead.")]
    public class ComboBoxEdit : ZComboBox { }

    /// <summary>
    /// Legacy alias for <see cref="ZComboBox"/>.
    /// Preserved for backward compatibility across 5 release cycles.
    /// </summary>
    [Obsolete("ZeroComboBox is deprecated and will be removed in 5 release cycles. Please migrate to ZComboBox instead.")]
    public class ZeroComboBox : ZComboBox { }

    #endregion

}
