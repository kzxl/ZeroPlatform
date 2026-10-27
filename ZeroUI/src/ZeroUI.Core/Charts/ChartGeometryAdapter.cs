using System;
using System.Collections.Generic;
using ZeroGeometry.Core.Polygons;
using ZeroGeometry.Core.Spatial;

namespace ZeroUI.Core.Charts
{
    /// <summary>
    /// Vector geometry and spatial polygon clipping adapter for ZeroUI charts.
    /// Harnesses sovereign Tier-3 <see cref="ZeroGeometry.Core"/> for viewport polygon clipping and hit testing.
    /// </summary>
    public static class ChartGeometryAdapter
    {
        /// <summary>
        /// Converts a <see cref="ChartRectF"/> to a 4-vertex <see cref="Polygon2D"/>.
        /// </summary>
        public static Polygon2D ToPolygon(ChartRectF rect)
        {
            var vertices = new[]
            {
                new Point2D(rect.Left, rect.Top),
                new Point2D(rect.Right, rect.Top),
                new Point2D(rect.Right, rect.Bottom),
                new Point2D(rect.Left, rect.Bottom)
            };
            return new Polygon2D(vertices);
        }

        /// <summary>
        /// Clips an arbitrary polygon (e.g. area chart fill polygon) against the chart's <see cref="ChartRectF"/> plot area.
        /// </summary>
        public static Polygon2D ClipToPlotArea(Polygon2D subjectPolygon, ChartRectF plotArea)
        {
            if (subjectPolygon == null) throw new ArgumentNullException(nameof(subjectPolygon));
            var clipRect = ToPolygon(plotArea);
            return PolygonClipper.Intersect(subjectPolygon, clipRect);
        }

        /// <summary>
        /// Performs point-in-polygon hit testing against a chart series boundary (e.g. radar or funnel slice).
        /// </summary>
        public static bool ContainsPoint(Polygon2D polygon, float screenX, float screenY)
        {
            if (polygon == null || polygon.VertexCount < 3) return false;
            return polygon.ContainsPoint(new Point2D(screenX, screenY));
        }

        /// <summary>
        /// Calculates the centroid of a polygon for anchor placement of badges or callouts.
        /// </summary>
        public static (float X, float Y) GetCentroid(Polygon2D polygon)
        {
            if (polygon == null || polygon.VertexCount == 0) return (0f, 0f);
            var c = polygon.Centroid();
            return ((float)c.X, (float)c.Y);
        }
    }
}
