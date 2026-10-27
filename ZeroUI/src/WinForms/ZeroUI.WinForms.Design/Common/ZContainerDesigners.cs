using System;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Windows.Forms;
using ZeroUI.WinForms.Containers;
using ZeroUI.WinForms.Layout;
using ZeroUI.WinForms.Navigation;

namespace ZeroUI.WinForms.Design.Common
{
    /// <summary>
    /// Parent Container Designer for ZPanel allowing visual drag-and-drop of child controls.
    /// </summary>
    public class ZPanelDesigner : ZeroParentControlDesigner<ZPanel>
    {
    }

    /// <summary>
    /// Parent Container Designer for ZCard allowing visual drag-and-drop of child controls.
    /// </summary>
    public class ZCardDesigner : ZeroParentControlDesigner<ZCard>
    {
    }

    /// <summary>
    /// Parent Container Designer for ZGroupBox allowing visual drag-and-drop of child controls.
    /// </summary>
    public class ZGroupBoxDesigner : ZeroParentControlDesigner<ZGroupBox>
    {
    }

    /// <summary>
    /// Parent Container Designer for ZTabControl.
    /// </summary>
    public class ZTabControlDesigner : ZeroParentControlDesigner<ZTabControl>
    {
    }

    /// <summary>
    /// Parent Container Designer for ZSplitContainer.
    /// </summary>
    public class ZSplitContainerDesigner : ZeroParentControlDesigner<ZSplitContainer>
    {
    }

    /// <summary>
    /// Parent Container Designer for ZFlowLayout.
    /// </summary>
    public class ZFlowLayoutDesigner : ZeroParentControlDesigner<ZFlowLayout>
    {
    }
}
