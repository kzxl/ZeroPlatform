using System;
using System.ComponentModel;

namespace ZeroUI.WinForms.Industrial
{
    // =========================================================================================
    // BACKWARD COMPATIBILITY SHIMS
    // These type declarations ensure that existing code referencing ZeroUI.WinForms.Industrial
    // continues to compile without breaking changes.
    // Recommended: migrate using directives to ZeroUI.WinForms.Workflow, Containers, Data, etc.
    // =========================================================================================

    #region Workflow Controls Shims

    [Obsolete("Use ZeroUI.WinForms.Workflow.ProcessMap instead.")]
    public class ProcessMap : ZeroUI.WinForms.Workflow.ProcessMap { }

    [Obsolete("Use ZeroUI.WinForms.Workflow.GanttControl instead.")]
    public class GanttControl : ZeroUI.WinForms.Workflow.GanttControl { }

    [Obsolete("Use ZeroUI.WinForms.Workflow.KanbanBoard instead.")]
    public class KanbanBoard : ZeroUI.WinForms.Workflow.KanbanBoard { }

    [Obsolete("Use ZeroUI.WinForms.Workflow.Timeline instead.")]
    public class Timeline : ZeroUI.WinForms.Workflow.Timeline { }

    [Obsolete("Use ZeroUI.WinForms.Workflow.StepsControl instead.")]
    public class StepsControl : ZeroUI.WinForms.Workflow.StepsControl { }

    [Obsolete("Use ZeroUI.WinForms.Workflow.ZeroSteps instead.")]
    public class ZeroSteps : ZeroUI.WinForms.Workflow.ZeroSteps { }

    [Obsolete("Use ZeroUI.WinForms.Workflow.ZeroStepsControl instead.")]
    public class ZeroStepsControl : ZeroUI.WinForms.Workflow.ZeroStepsControl { }

    [Obsolete("Use ZeroUI.WinForms.Workflow.ZeroStepItem instead.")]
    public class ZeroStepItem : ZeroUI.WinForms.Workflow.ZeroStepItem { }

    [Obsolete("Use ZeroUI.WinForms.Workflow.ZeroStepStatus instead.")]
    public enum ZeroStepStatus
    {
        Waiting = ZeroUI.WinForms.Workflow.ZeroStepStatus.Waiting,
        InProgress = ZeroUI.WinForms.Workflow.ZeroStepStatus.InProgress,
        Completed = ZeroUI.WinForms.Workflow.ZeroStepStatus.Completed,
        Warning = ZeroUI.WinForms.Workflow.ZeroStepStatus.Warning,
        Error = ZeroUI.WinForms.Workflow.ZeroStepStatus.Error
    }

    [Obsolete("Use ZeroUI.WinForms.Workflow.ZeroStepGlyph instead.")]
    public enum ZeroStepGlyph
    {
        Gear = ZeroUI.WinForms.Workflow.ZeroStepGlyph.Gear,
        Checkmark = ZeroUI.WinForms.Workflow.ZeroStepGlyph.Checkmark,
        Warehouse = ZeroUI.WinForms.Workflow.ZeroStepGlyph.Warehouse,
        Truck = ZeroUI.WinForms.Workflow.ZeroStepGlyph.Truck,
        Alert = ZeroUI.WinForms.Workflow.ZeroStepGlyph.Alert,
        Custom = ZeroUI.WinForms.Workflow.ZeroStepGlyph.Custom
    }

    [Obsolete("Use ZeroUI.WinForms.Workflow.ZeroStepClickedEventArgs instead.")]
    public class ZeroStepClickedEventArgs : ZeroUI.WinForms.Workflow.ZeroStepClickedEventArgs
    {
        public ZeroStepClickedEventArgs(int index, ZeroUI.WinForms.Workflow.StepsControlItem step) : base(index, step) { }
    }

    [Obsolete("Use ZeroUI.WinForms.Workflow.WorkflowCard instead.")]
    public class WorkflowCard : ZeroUI.WinForms.Workflow.WorkflowCard { }

    #endregion

    #region Containers & Surfaces Shims

    [Obsolete("Use ZeroUI.WinForms.Containers.Card instead.")]
    public class Card : ZeroUI.WinForms.Containers.Card { }

    [Obsolete("Use ZeroUI.WinForms.Containers.ZeroCard instead.")]
    [ToolboxItem(false)]
    public class ZeroCard : ZeroUI.WinForms.Containers.ZeroCard { }

    [Obsolete("Use ZeroUI.WinForms.Containers.GridCard instead.")]
    public class GridCard : ZeroUI.WinForms.Containers.GridCard { }

    [Obsolete("Use ZeroUI.WinForms.Containers.ZeroGridCard instead.")]
    [ToolboxItem(false)]
    public class ZeroGridCard : ZeroUI.WinForms.Containers.ZeroGridCard { }

    [Obsolete("Use ZeroUI.WinForms.Containers.Descriptions instead.")]
    public class Descriptions : ZeroUI.WinForms.Containers.Descriptions { }

    [Obsolete("Use ZeroUI.WinForms.Containers.ZeroDescriptions instead.")]
    [ToolboxItem(false)]
    public class ZeroDescriptions : ZeroUI.WinForms.Containers.ZeroDescriptions { }

    [Obsolete("Use ZeroUI.WinForms.Containers.ZeroDescriptionItem instead.")]
    public class ZeroDescriptionItem : ZeroUI.WinForms.Containers.ZeroDescriptionItem
    {
        public ZeroDescriptionItem() : base() { }
        public ZeroDescriptionItem(string label, string value, System.Drawing.Color? valueColor = null, bool isHighlighted = false)
            : base(label, value, valueColor, isHighlighted) { }
    }

    [Obsolete("Use ZeroUI.WinForms.Containers.HardwareCard instead.")]
    public class HardwareCard : ZeroUI.WinForms.Containers.HardwareCard { }

    [Obsolete("Use ZeroUI.WinForms.Containers.MachineCard instead.")]
    public class MachineCard : ZeroUI.WinForms.Containers.MachineCard { }

    #endregion

    #region Charts & Feedback Shims

    [Obsolete("Use ZeroUI.WinForms.Charts.Heatmap instead.")]
    public class Heatmap : ZeroUI.WinForms.Charts.Heatmap { }

    [Obsolete("Use ZeroUI.WinForms.Charts.Sparkline instead.")]
    public class Sparkline : ZeroUI.WinForms.Charts.Sparkline { }

    [Obsolete("Use ZeroUI.WinForms.Feedback.AlertBanner instead.")]
    public class AlertBanner : ZeroUI.WinForms.Feedback.AlertBanner { }

    #endregion
}
