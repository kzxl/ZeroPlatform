using System;
using System.Collections.Generic;

namespace ZeroUI.Core.Scene.Svg
{
    public abstract class SvgElement
    {
        public string? Id { get; set; }
        public string? FillColor { get; set; }
        public string? StrokeColor { get; set; }
        public float StrokeWidth { get; set; } = 1f;
        public float Opacity { get; set; } = 1f;
    }

    public class SvgRectElement : SvgElement
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public float RadiusX { get; set; }
        public float RadiusY { get; set; }
    }

    public class SvgCircleElement : SvgElement
    {
        public float Cx { get; set; }
        public float Cy { get; set; }
        public float R { get; set; }
    }

    public class SvgEllipseElement : SvgElement
    {
        public float Cx { get; set; }
        public float Cy { get; set; }
        public float Rx { get; set; }
        public float Ry { get; set; }
    }

    public class SvgLineElement : SvgElement
    {
        public float X1 { get; set; }
        public float Y1 { get; set; }
        public float X2 { get; set; }
        public float Y2 { get; set; }
    }

    public class SvgPolylineElement : SvgElement
    {
        public List<ScenePoint> Points { get; } = new List<ScenePoint>();
        public bool IsClosed { get; set; }
    }

    public class SvgPathElement : SvgElement
    {
        public string PathData { get; set; } = string.Empty;
    }

    /// <summary>
    /// SceneNode representing an imported SVG vector graphic.
    /// Preserves vector shapes, fills, strokes, and telemetry metadata for cross-platform canvas rendering.
    /// </summary>
    public class SvgVectorNode : SceneNode
    {
        private readonly List<SvgElement> _elements = new List<SvgElement>();

        public IReadOnlyList<SvgElement> Elements => _elements;
        public string? RawSvg { get; set; }
        public SceneRect ViewBox { get; set; }

        public SvgVectorNode(string label = "")
        {
            Label = label;
        }

        public void AddElement(SvgElement element)
        {
            if (element != null)
            {
                _elements.Add(element);
                NotifyDirty();
            }
        }

        public void ClearElements()
        {
            _elements.Clear();
            NotifyDirty();
        }

        public override void Render(object graphicsContext, in RenderContext context)
        {
            // Vector rendering is handled by the platform-specific canvas (WinForms GDI+ or WPF DrawingContext)
        }
    }
}
