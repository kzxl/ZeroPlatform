using System;
using System.Collections.Generic;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Blob
{
    /// <summary>
    /// Industrial 8-connected component labeling (CCL) and blob morphological analysis.
    /// Extracts discrete objects, computes centroids, areas, bounding boxes, perimeters, and circularity.
    /// </summary>
    public static class BlobAnalyzer
    {
        /// <summary>
        /// Analyzes and extracts all connected blobs from an image above a given threshold.
        /// </summary>
        /// <param name="image">Input inspection image.</param>
        /// <param name="threshold">Threshold cutoff (0..255). Pixels >= threshold are considered foreground.</param>
        /// <param name="minArea">Minimum blob pixel area filter.</param>
        /// <param name="maxArea">Maximum blob pixel area filter.</param>
        /// <returns>List of detected and measured blobs.</returns>
        public static unsafe List<BlobInfo> ExtractBlobs(
            ImageBuffer image,
            byte threshold = 128,
            int minArea = 10,
            int maxArea = int.MaxValue,
            bool computeOrientedBox = true)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));

            int w = image.Width;
            int h = image.Height;

            // 1. Ensure Grayscale
            ImageBuffer? grayOwned = null;
            ImageBuffer grayImage = image;
            if (image.Format != ImageFormatMode.Gray8)
            {
                grayOwned = ImageBuffer.CreateGray8(w, h);
                ZeroGraphics.Imaging.Filters.ColorTransform.ToGrayscale(image, grayOwned);
                grayImage = grayOwned;
            }

            try
            {
                // Disjoint Set Union (DSU) table for label equivalence
                int maxLabels = Math.Max(1024, (w * h) / 4);
                int[] parent = new int[maxLabels];
                for (int i = 0; i < maxLabels; i++) parent[i] = i;

                int Find(int i)
                {
                    int root = i;
                    while (root != parent[root]) root = parent[root];
                    int curr = i;
                    while (curr != root)
                    {
                        int next = parent[curr];
                        parent[curr] = root;
                        curr = next;
                    }
                    return root;
                }

                void Union(int i, int j)
                {
                    int rootI = Find(i);
                    int rootJ = Find(j);
                    if (rootI != rootJ)
                    {
                        if (rootI < rootJ) parent[rootJ] = rootI;
                        else parent[rootI] = rootJ;
                    }
                }

                int[] labels = new int[w * h];
                int nextLabel = 1;

                // --- PASS 1: Initial Label Assignment & Equivalence Recording (8-connectivity) ---
                for (int y = 0; y < h; y++)
                {
                    byte* pRow = grayImage.GetRowPointer(y);
                    int rowOffset = y * w;
                    int prevRowOffset = (y - 1) * w;

                    for (int x = 0; x < w; x++)
                    {
                        if (pRow[x] < threshold) continue; // Background

                        // Collect neighboring labels: Top-Left, Top, Top-Right, Left
                        int l1 = (y > 0 && x > 0) ? labels[prevRowOffset + x - 1] : 0;
                        int l2 = (y > 0) ? labels[prevRowOffset + x] : 0;
                        int l3 = (y > 0 && x < w - 1) ? labels[prevRowOffset + x + 1] : 0;
                        int l4 = (x > 0) ? labels[rowOffset + x - 1] : 0;

                        // Find min non-zero neighbor label
                        int minLabel = int.MaxValue;
                        if (l1 > 0 && l1 < minLabel) minLabel = l1;
                        if (l2 > 0 && l2 < minLabel) minLabel = l2;
                        if (l3 > 0 && l3 < minLabel) minLabel = l3;
                        if (l4 > 0 && l4 < minLabel) minLabel = l4;

                        if (minLabel == int.MaxValue)
                        {
                            // New blob label
                            if (nextLabel >= parent.Length)
                            {
                                int newCap = parent.Length * 2;
                                Array.Resize(ref parent, newCap);
                                for (int k = nextLabel; k < newCap; k++) parent[k] = k;
                            }
                            labels[rowOffset + x] = nextLabel++;
                        }
                        else
                        {
                            labels[rowOffset + x] = minLabel;

                            // Record equivalence
                            if (l1 > 0 && l1 != minLabel) Union(minLabel, l1);
                            if (l2 > 0 && l2 != minLabel) Union(minLabel, l2);
                            if (l3 > 0 && l3 != minLabel) Union(minLabel, l3);
                            if (l4 > 0 && l4 != minLabel) Union(minLabel, l4);
                        }
                    }
                }

                // --- Flatten DSU ---
                for (int i = 1; i < nextLabel; i++)
                {
                    parent[i] = Find(i);
                }

                // --- PASS 2: Aggregate Blob Statistics ---
                var blobMap = new Dictionary<int, BlobAccumulator>();

                for (int y = 0; y < h; y++)
                {
                    int rowOffset = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        int rawLabel = labels[rowOffset + x];
                        if (rawLabel == 0) continue;

                        int rootLabel = parent[rawLabel];
                        labels[rowOffset + x] = rootLabel;

                        if (!blobMap.TryGetValue(rootLabel, out var acc))
                        {
                            acc = new BlobAccumulator(rootLabel, computeOrientedBox);
                            blobMap[rootLabel] = acc;
                        }

                        acc.Area++;
                        acc.SumX += x;
                        acc.SumY += y;

                        if (x < acc.MinX) acc.MinX = x;
                        if (x > acc.MaxX) acc.MaxX = x;
                        if (y < acc.MinY) acc.MinY = y;
                        if (y > acc.MaxY) acc.MaxY = y;

                        // Check if pixel is on the perimeter (has at least 1 4-neighbor background)
                        bool isBorder = (x == 0 || x == w - 1 || y == 0 || y == h - 1 ||
                                         labels[rowOffset + x - 1] == 0 ||
                                         labels[rowOffset + x + 1] == 0 ||
                                         labels[(y - 1) * w + x] == 0 ||
                                         labels[(y + 1) * w + x] == 0);
                        if (isBorder)
                        {
                            acc.PerimeterPixels++;
                            if (computeOrientedBox)
                            {
                                acc.PerimeterPoints?.Add(new ZeroGraphics.Vision.Matching.VisionPoint2D(x, y));
                            }
                        }
                    }
                }

                // 3. Compile and Filter final BlobInfo results
                var results = new List<BlobInfo>(blobMap.Count);

                foreach (var kvp in blobMap)
                {
                    var acc = kvp.Value;
                    if (acc.Area < minArea || acc.Area > maxArea)
                        continue;

                    var blob = new BlobInfo(acc.Id)
                    {
                        Area = acc.Area,
                        MinX = acc.MinX,
                        MinY = acc.MinY,
                        MaxX = acc.MaxX,
                        MaxY = acc.MaxY,
                        CentroidX = (double)acc.SumX / acc.Area,
                        CentroidY = (double)acc.SumY / acc.Area,
                        Perimeter = acc.PerimeterPixels
                    };

                    // Circularity = 4 * PI * Area / (Perimeter^2)
                    if (blob.Perimeter > 0)
                    {
                        blob.Circularity = Math.Min(1.0, (4.0 * Math.PI * blob.Area) / (blob.Perimeter * blob.Perimeter));
                    }

                    // Oriented Bounding Box (OBB)
                    if (computeOrientedBox && acc.PerimeterPoints != null && acc.PerimeterPoints.Count >= 3)
                    {
                        blob.OrientedBox = ZeroGraphics.Vision.Metrology.ConvexHull2D.ComputeMinimumAreaBoundingBox(acc.PerimeterPoints);
                    }
                    else
                    {
                        blob.OrientedBox = new ZeroGraphics.Vision.Metrology.RotatedRect2D(blob.CentroidX, blob.CentroidY, blob.Width, blob.Height, 0.0);
                    }

                    results.Add(blob);
                }

                // Sort by area descending (largest objects first)
                results.Sort((a, b) => b.Area.CompareTo(a.Area));

                return results;
            }
            finally
            {
                grayOwned?.Dispose();
            }
        }

        private sealed class BlobAccumulator
        {
            public int Id { get; }
            public int Area;
            public long SumX;
            public long SumY;
            public int MinX = int.MaxValue;
            public int MinY = int.MaxValue;
            public int MaxX = int.MinValue;
            public int MaxY = int.MinValue;
            public int PerimeterPixels;
            public List<ZeroGraphics.Vision.Matching.VisionPoint2D>? PerimeterPoints;

            public BlobAccumulator(int id, bool trackPerimeter = false)
            {
                Id = id;
                if (trackPerimeter) PerimeterPoints = new List<ZeroGraphics.Vision.Matching.VisionPoint2D>();
            }
        }
    }
}
