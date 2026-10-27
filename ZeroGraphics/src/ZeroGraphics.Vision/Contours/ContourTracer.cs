using System;
using System.Collections.Generic;
using System.Drawing;
using ZeroGraphics.Imaging.Core;

namespace ZeroGraphics.Vision.Contours
{
    /// <summary>
    /// Represents an extracted closed polygonal boundary chain with hierarchy metadata.
    /// </summary>
    public sealed class Contour
    {
        public List<Point> Points { get; }
        public bool IsHole { get; }
        public int Id { get; }

        public Contour(int id, bool isHole)
        {
            Id = id;
            IsHole = isHole;
            Points = new List<Point>();
        }

        public Contour(int id, bool isHole, IEnumerable<Point> points)
        {
            Id = id;
            IsHole = isHole;
            Points = new List<Point>(points);
        }
    }

    /// <summary>
    /// High-speed topological border following algorithm (Suzuki-Abe) for binary/edge images.
    /// Extracts external and internal hole boundaries into ordered contour polygon chains.
    /// </summary>
    public static class ContourTracer
    {
        // 8-neighborhood directional offsets (clockwise starting from East)
        // 0: (1, 0), 1: (1, 1), 2: (0, 1), 3: (-1, 1), 4: (-1, 0), 5: (-1, -1), 6: (0, -1), 7: (1, -1)
        private static readonly int[] Dx = { 1, 1, 0, -1, -1, -1, 0, 1 };
        private static readonly int[] Dy = { 0, 1, 1, 1, 0, -1, -1, -1 };

        /// <summary>
        /// Finds all closed contours in a binary/edge Gray8 image.
        /// Non-zero pixels are treated as foreground; zero pixels as background.
        /// </summary>
        /// <param name="image">Source Gray8 image buffer.</param>
        /// <param name="minPoints">Minimum point count threshold to filter out speckle noise.</param>
        /// <returns>List of detected outer and hole contours.</returns>
        public static unsafe List<Contour> FindContours(ImageBuffer image, int minPoints = 4)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            if (image.Format != ImageFormatMode.Gray8)
                throw new ArgumentException("ContourTracer requires an 8-bit grayscale binary image.", nameof(image));

            int w = image.Width;
            int h = image.Height;
            var contours = new List<Contour>();

            // Label buffer to prevent duplicate tracing:
            // 0: background, 1: foreground unvisited, >1 or <-1: visited border IDs
            int[] labels = new int[w * h];
            for (int y = 0; y < h; y++)
            {
                byte* pRow = image.GetRowPointer(y);
                int rowIdx = y * w;
                for (int x = 0; x < w; x++)
                {
                    labels[rowIdx + x] = pRow[x] > 0 ? 1 : 0;
                }
            }

            int nextBorderId = 2;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    int val = labels[idx];
                    if (val == 0) continue;

                    // Condition A: Outer border (pixel is 1, left pixel is 0)
                    bool isOuter = (x == 0 || labels[idx - 1] == 0) && val == 1;

                    // Condition B: Hole border (pixel is >= 1, right pixel is 0)
                    bool isHole = (x == w - 1 || labels[idx + 1] == 0) && val >= 1;

                    if (!isOuter && !isHole)
                        continue;

                    bool holeFlag = !isOuter && isHole;
                    int fromDir = isOuter ? 4 : 0; // Outer starts examining from West (4); Hole from East (0)

                    var contour = TraceSingleBorder(labels, w, h, x, y, fromDir, nextBorderId, holeFlag);
                    if (contour.Points.Count >= minPoints)
                    {
                        contours.Add(contour);
                    }
                    nextBorderId++;
                }
            }

            return contours;
        }

        private static Contour TraceSingleBorder(
            int[] labels, int w, int h,
            int startX, int startY, int fromDir,
            int borderId, bool isHole)
        {
            var contour = new Contour(borderId, isHole);

            // 1. Find second point B(x1, y1) around start point A(x0, y0)
            int dir = fromDir;
            int x1 = -1, y1 = -1;
            int dir1 = -1;

            for (int k = 0; k < 8; k++)
            {
                int testDir = (dir + k) % 8;
                int nx = startX + Dx[testDir];
                int ny = startY + Dy[testDir];

                if (nx >= 0 && nx < w && ny >= 0 && ny < h && labels[ny * w + nx] != 0)
                {
                    x1 = nx;
                    y1 = ny;
                    dir1 = testDir;
                    break;
                }
            }

            if (x1 == -1)
            {
                // Isolated single pixel
                labels[startY * w + startX] = -borderId;
                contour.Points.Add(new Point(startX, startY));
                return contour;
            }

            // 2. Trace border using Moore-Neighbor clockwise traversal
            contour.Points.Add(new Point(startX, startY));

            int currX = x1;
            int currY = y1;
            int prevDir = dir1;

            int prevX = startX;
            int prevY = startY;

            int maxSteps = w * h * 2;
            int step = 0;

            while (step++ < maxSteps)
            {
                contour.Points.Add(new Point(currX, currY));
                labels[currY * w + currX] = borderId;

                // Search next neighbor counter-clockwise from (opposite of incoming direction + 1)
                int searchStartDir = (prevDir + 4 + 2) % 8; // opposite direction + clockwise step
                int nextX = -1, nextY = -1;
                int nextDir = -1;

                for (int k = 0; k < 8; k++)
                {
                    int testDir = (searchStartDir + k) % 8;
                    int nx = currX + Dx[testDir];
                    int ny = currY + Dy[testDir];

                    if (nx >= 0 && nx < w && ny >= 0 && ny < h && labels[ny * w + nx] != 0)
                    {
                        nextX = nx;
                        nextY = ny;
                        nextDir = testDir;
                        break;
                    }
                }

                if (nextX == -1)
                    break;

                // Stop condition: returned to start point from the original direction
                if (currX == startX && currY == startY && nextX == x1 && nextY == y1)
                {
                    break;
                }

                prevX = currX;
                prevY = currY;
                currX = nextX;
                currY = nextY;
                prevDir = nextDir;
            }

            return contour;
        }
    }
}
