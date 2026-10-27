using System;

namespace ZeroUI.Core.Scada
{
    /// <summary>
    /// Contract for lightweight SCADA elements that can be drawn within a single-HWND canvas,
    /// avoiding Windows HWND handle exhaustion while retaining individual interactivity and tag binding.
    /// </summary>
    public interface IScadaDrawable : IScadaBindable
    {
        /// <summary>
        /// Unique identifier for this graphical element.
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// Human-readable descriptor or equipment tag name (e.g. "TK-101", "P-202A").
        /// </summary>
        string Label { get; set; }

        /// <summary>
        /// X position relative to the canvas coordinate space.
        /// </summary>
        float X { get; set; }

        /// <summary>
        /// Y position relative to the canvas coordinate space.
        /// </summary>
        float Y { get; set; }

        /// <summary>
        /// Width of the element bounding box.
        /// </summary>
        float Width { get; set; }

        /// <summary>
        /// Height of the element bounding box.
        /// </summary>
        float Height { get; set; }

        /// <summary>
        /// Visibility flag.
        /// </summary>
        bool IsVisible { get; set; }

        /// <summary>
        /// Selection or focus state on the canvas.
        /// </summary>
        bool IsSelected { get; set; }

        /// <summary>
        /// Hover state when the mouse is positioned over this element.
        /// </summary>
        bool IsHovered { get; set; }

        /// <summary>
        /// Layer Z-index for draw ordering.
        /// </summary>
        int ZIndex { get; set; }

        /// <summary>
        /// Evaluates whether the specified canvas point intersects with this element.
        /// </summary>
        bool HitTest(float canvasX, float canvasY);
    }
}
