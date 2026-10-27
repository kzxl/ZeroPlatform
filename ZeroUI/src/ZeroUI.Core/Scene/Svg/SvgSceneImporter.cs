using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ZeroUI.Core.Scene.Svg
{
    /// <summary>
    /// High-performance XML-based SVG vector importer for industrial SCADA scene graphs.
    /// Converts SVG vector graphics into scalable <see cref="SvgVectorNode"/> instances,
    /// extracting shapes, styling, viewbox geometry, and SCADA tag telemetry metadata.
    /// </summary>
    public static class SvgSceneImporter
    {
        public static SvgVectorNode ImportFromString(string svgContent, string nodeLabel = "SvgDiagram")
        {
            if (string.IsNullOrWhiteSpace(svgContent))
                throw new ArgumentException("SVG content cannot be empty", nameof(svgContent));

            using var reader = new StringReader(svgContent);
            var doc = XDocument.Load(reader);
            var root = doc.Root;

            if (root == null || !root.Name.LocalName.Equals("svg", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Invalid SVG document: root element must be <svg>");

            var node = new SvgVectorNode(nodeLabel)
            {
                RawSvg = svgContent
            };

            // Parse viewBox
            string? viewBoxAttr = root.Attribute("viewBox")?.Value;
            if (!string.IsNullOrWhiteSpace(viewBoxAttr))
            {
                var parts = viewBoxAttr!.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4 &&
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float vx) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float vy) &&
                    float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float vw) &&
                    float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float vh))
                {
                    node.ViewBox = new SceneRect(vx, vy, vw, vh);
                    node.Width = vw;
                    node.Height = vh;
                }
            }
            else
            {
                float w = ParseFloat(root.Attribute("width")?.Value, 400f);
                float h = ParseFloat(root.Attribute("height")?.Value, 300f);
                node.ViewBox = new SceneRect(0, 0, w, h);
                node.Width = w;
                node.Height = h;
            }

            // Parse Tag Binding Metadata
            string? tagAttr = root.Attribute("data-tag")?.Value ?? root.Attribute("data-scada-tag")?.Value;
            if (!string.IsNullOrWhiteSpace(tagAttr))
            {
                node.BoundTagPath = tagAttr;
            }

            // Recursive parse elements
            ParseContainer(root, node);

            return node;
        }

        private static void ParseContainer(XElement container, SvgVectorNode node)
        {
            foreach (var elem in container.Elements())
            {
                string tag = elem.Name.LocalName.ToLowerInvariant();

                switch (tag)
                {
                    case "rect":
                        node.AddElement(new SvgRectElement
                        {
                            Id = elem.Attribute("id")?.Value,
                            X = ParseFloat(elem.Attribute("x")?.Value, 0f),
                            Y = ParseFloat(elem.Attribute("y")?.Value, 0f),
                            Width = ParseFloat(elem.Attribute("width")?.Value, 0f),
                            Height = ParseFloat(elem.Attribute("height")?.Value, 0f),
                            RadiusX = ParseFloat(elem.Attribute("rx")?.Value, 0f),
                            RadiusY = ParseFloat(elem.Attribute("ry")?.Value, 0f),
                            FillColor = GetStyle(elem, "fill"),
                            StrokeColor = GetStyle(elem, "stroke"),
                            StrokeWidth = ParseFloat(GetStyle(elem, "stroke-width"), 1f),
                            Opacity = ParseFloat(GetStyle(elem, "opacity"), 1f)
                        });
                        break;

                    case "circle":
                        node.AddElement(new SvgCircleElement
                        {
                            Id = elem.Attribute("id")?.Value,
                            Cx = ParseFloat(elem.Attribute("cx")?.Value, 0f),
                            Cy = ParseFloat(elem.Attribute("cy")?.Value, 0f),
                            R = ParseFloat(elem.Attribute("r")?.Value, 0f),
                            FillColor = GetStyle(elem, "fill"),
                            StrokeColor = GetStyle(elem, "stroke"),
                            StrokeWidth = ParseFloat(GetStyle(elem, "stroke-width"), 1f),
                            Opacity = ParseFloat(GetStyle(elem, "opacity"), 1f)
                        });
                        break;

                    case "ellipse":
                        node.AddElement(new SvgEllipseElement
                        {
                            Id = elem.Attribute("id")?.Value,
                            Cx = ParseFloat(elem.Attribute("cx")?.Value, 0f),
                            Cy = ParseFloat(elem.Attribute("cy")?.Value, 0f),
                            Rx = ParseFloat(elem.Attribute("rx")?.Value, 0f),
                            Ry = ParseFloat(elem.Attribute("ry")?.Value, 0f),
                            FillColor = GetStyle(elem, "fill"),
                            StrokeColor = GetStyle(elem, "stroke"),
                            StrokeWidth = ParseFloat(GetStyle(elem, "stroke-width"), 1f),
                            Opacity = ParseFloat(GetStyle(elem, "opacity"), 1f)
                        });
                        break;

                    case "line":
                        node.AddElement(new SvgLineElement
                        {
                            Id = elem.Attribute("id")?.Value,
                            X1 = ParseFloat(elem.Attribute("x1")?.Value, 0f),
                            Y1 = ParseFloat(elem.Attribute("y1")?.Value, 0f),
                            X2 = ParseFloat(elem.Attribute("x2")?.Value, 0f),
                            Y2 = ParseFloat(elem.Attribute("y2")?.Value, 0f),
                            StrokeColor = GetStyle(elem, "stroke"),
                            StrokeWidth = ParseFloat(GetStyle(elem, "stroke-width"), 1f),
                            Opacity = ParseFloat(GetStyle(elem, "opacity"), 1f)
                        });
                        break;

                    case "polyline":
                    case "polygon":
                        var poly = new SvgPolylineElement
                        {
                            Id = elem.Attribute("id")?.Value,
                            IsClosed = tag == "polygon",
                            FillColor = GetStyle(elem, "fill"),
                            StrokeColor = GetStyle(elem, "stroke"),
                            StrokeWidth = ParseFloat(GetStyle(elem, "stroke-width"), 1f),
                            Opacity = ParseFloat(GetStyle(elem, "opacity"), 1f)
                        };
                        string? ptsAttr = elem.Attribute("points")?.Value;
                        if (!string.IsNullOrWhiteSpace(ptsAttr))
                        {
                            var coords = ptsAttr!.Split(new[] { ' ', ',', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            for (int i = 0; i < coords.Length - 1; i += 2)
                            {
                                if (float.TryParse(coords[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float px) &&
                                    float.TryParse(coords[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float py))
                                {
                                    poly.Points.Add(new ScenePoint(px, py));
                                }
                            }
                        }
                        node.AddElement(poly);
                        break;

                    case "path":
                        string? d = elem.Attribute("d")?.Value;
                        if (!string.IsNullOrWhiteSpace(d))
                        {
                            node.AddElement(new SvgPathElement
                            {
                                Id = elem.Attribute("id")?.Value,
                                PathData = d!,
                                FillColor = GetStyle(elem, "fill"),
                                StrokeColor = GetStyle(elem, "stroke"),
                                StrokeWidth = ParseFloat(GetStyle(elem, "stroke-width"), 1f),
                                Opacity = ParseFloat(GetStyle(elem, "opacity"), 1f)
                            });
                        }
                        break;

                    case "g":
                        // Group element: process children recursively
                        ParseContainer(elem, node);
                        break;
                }
            }
        }

        private static string? GetStyle(XElement elem, string styleName)
        {
            // 1. Direct attribute
            var attr = elem.Attribute(styleName);
            if (attr != null) return attr.Value;

            // 2. Inline style="fill:red; stroke:blue"
            var styleAttr = elem.Attribute("style");
            if (styleAttr != null && !string.IsNullOrWhiteSpace(styleAttr.Value))
            {
                var declarations = styleAttr.Value.Split(';');
                foreach (var dec in declarations)
                {
                    var kv = dec.Split(':');
                    if (kv.Length == 2 && kv[0].Trim().Equals(styleName, StringComparison.OrdinalIgnoreCase))
                    {
                        return kv[1].Trim();
                    }
                }
            }

            return null;
        }

        private static float ParseFloat(string? text, float fallback)
        {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            // Strip px suffix if present
            text = text!.TrimEnd('p', 'x', 'P', 'X', '%');
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float val) ? val : fallback;
        }
    }
}
