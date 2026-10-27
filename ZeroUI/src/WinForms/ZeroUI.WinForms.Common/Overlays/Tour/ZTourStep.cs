using System;
using System.Drawing;
using System.Windows.Forms;

namespace ZeroUI.WinForms.Overlays
{
    /// <summary>
    /// Represents an individual guided walkthrough step within a WinForms <see cref="ZTour"/>.
    /// Supports target control anchoring, spotlight masking, custom placement, and callbacks.
    /// </summary>
    public class ZTourStep
    {
        public Control? Target { get; set; }
        public string? TargetName { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ZTourPlacement Placement { get; set; } = ZTourPlacement.Auto;
        public Padding TargetPadding { get; set; } = new Padding(6);
        public int CornerRadius { get; set; } = 8;
        public bool Mask { get; set; } = true;
        public string? NextButtonText { get; set; }
        public string? PrevButtonText { get; set; }
        public int? CardWidth { get; set; }
        public int? CardHeight { get; set; }
        public Action<ZTourStep>? OnEnter { get; set; }
        public Action<ZTourStep>? OnLeave { get; set; }

        public ZTourStep() { }

        public ZTourStep(Control target, string title, string description, ZTourPlacement placement = ZTourPlacement.Auto)
        {
            Target = target;
            Title = title;
            Description = description;
            Placement = placement;
        }

        public ZTourStep(string targetName, string title, string description, ZTourPlacement placement = ZTourPlacement.Auto)
        {
            TargetName = targetName;
            Title = title;
            Description = description;
            Placement = placement;
        }
    }
}
