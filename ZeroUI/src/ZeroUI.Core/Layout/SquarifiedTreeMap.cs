using System;
using System.Collections.Generic;
using System.Linq;

namespace ZeroUI.Core.Layout;

/// <summary>
/// Output rectangle representing a node in a Squarified TreeMap layout.
/// </summary>
public readonly struct TreeMapRect<T>
{
    public T Item { get; }
    public double X { get; }
    public double Y { get; }
    public double Width { get; }
    public double Height { get; }
    public double NormalizedWeight { get; }

    public TreeMapRect(T item, double x, double y, double width, double height, double normalizedWeight)
    {
        Item = item;
        X = x;
        Y = y;
        Width = width;
        Height = height;
        NormalizedWeight = normalizedWeight;
    }
}

/// <summary>
/// Generic implementation of the Bruls-Huizing-van Wijk Squarified Treemap layout algorithm.
/// Provides optimal aspect ratio tiling close to 1:1 (squares) for proportional data visualization.
/// </summary>
public static class SquarifiedTreeMap
{
    public static IReadOnlyList<TreeMapRect<T>> Layout<T>(
        IReadOnlyList<T> items,
        Func<T, double> weightSelector,
        double width,
        double height,
        double padding = 1.0)
    {
        var result = new List<TreeMapRect<T>>();
        if (items == null || items.Count == 0 || width <= 0 || height <= 0)
            return result;

        var weighted = items
            .Select(x => (Item: x, Weight: Math.Max(0.0, weightSelector(x))))
            .Where(x => x.Weight > 0.0)
            .OrderByDescending(x => x.Weight)
            .ToList();

        if (weighted.Count == 0) return result;

        double totalWeight = weighted.Sum(x => x.Weight);
        if (totalWeight <= 0.0) return result;

        double totalArea = width * height;
        var areas = weighted
            .Select(w => (w.Item, Area: (w.Weight / totalWeight) * totalArea, NormWeight: w.Weight / totalWeight))
            .ToList();

        double curX = 0;
        double curY = 0;
        double curW = width;
        double curH = height;

        var queue = new Queue<(T Item, double Area, double NormWeight)>(areas);

        while (queue.Count > 0 && curW > 0.5 && curH > 0.5)
        {
            var row = new List<(T Item, double Area, double NormWeight)>();
            double shortSide = Math.Min(curW, curH);

            while (queue.Count > 0)
            {
                var next = queue.Peek();
                var testRowAreas = new List<double>(row.Count + 1);
                for (int i = 0; i < row.Count; i++) testRowAreas.Add(row[i].Area);
                testRowAreas.Add(next.Area);

                var currentRowAreas = new List<double>(row.Count);
                for (int i = 0; i < row.Count; i++) currentRowAreas.Add(row[i].Area);

                if (row.Count == 0 || WorstAspect(testRowAreas, shortSide) <= WorstAspect(currentRowAreas, shortSide))
                {
                    row.Add(queue.Dequeue());
                }
                else
                {
                    break;
                }
            }

            double rowArea = row.Sum(r => r.Area);
            double rowThickness = rowArea / shortSide;

            if (curW <= curH)
            {
                double itemX = curX;
                foreach (var r in row)
                {
                    double itemW = (r.Area / rowArea) * curW;
                    double finalW = Math.Max(1.0, itemW - padding);
                    double finalH = Math.Max(1.0, rowThickness - padding);

                    result.Add(new TreeMapRect<T>(r.Item, itemX, curY, finalW, finalH, r.NormWeight));
                    itemX += itemW;
                }
                curY += rowThickness;
                curH -= rowThickness;
            }
            else
            {
                double itemY = curY;
                foreach (var r in row)
                {
                    double itemH = (r.Area / rowArea) * curH;
                    double finalW = Math.Max(1.0, rowThickness - padding);
                    double finalH = Math.Max(1.0, itemH - padding);

                    result.Add(new TreeMapRect<T>(r.Item, curX, itemY, finalW, finalH, r.NormWeight));
                    itemY += itemH;
                }
                curX += rowThickness;
                curW -= rowThickness;
            }
        }

        return result;
    }

    private static double WorstAspect(IReadOnlyList<double> rowAreas, double sideLength)
    {
        if (rowAreas == null || rowAreas.Count == 0 || sideLength <= 0.0001)
            return double.MaxValue;

        double sum = rowAreas.Sum();
        if (sum <= 0.0001) return double.MaxValue;

        double min = rowAreas.Min();
        double max = rowAreas.Max();
        double s2 = sideLength * sideLength;
        double sum2 = sum * sum;

        return Math.Max((s2 * max) / sum2, sum2 / (s2 * min));
    }
}
