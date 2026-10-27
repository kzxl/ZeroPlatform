using System;
using System.Collections.Generic;

namespace ZeroInference.Core.Vision
{
    public readonly struct BoundingBox
    {
        public float X1 { get; }
        public float Y1 { get; }
        public float X2 { get; }
        public float Y2 { get; }
        public float Score { get; }
        public int ClassId { get; }
        public string? Label { get; }

        public float Width => Math.Max(0.0f, X2 - X1);
        public float Height => Math.Max(0.0f, Y2 - Y1);
        public float Area => Width * Height;

        public BoundingBox(float x1, float y1, float x2, float y2, float score, int classId = 0, string? label = null)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            Score = score;
            ClassId = classId;
            Label = label;
        }

        public float IntersectionOverUnion(BoundingBox other)
        {
            float interX1 = Math.Max(X1, other.X1);
            float interY1 = Math.Max(Y1, other.Y1);
            float interX2 = Math.Min(X2, other.X2);
            float interY2 = Math.Min(Y2, other.Y2);

            float interW = Math.Max(0.0f, interX2 - interX1);
            float interH = Math.Max(0.0f, interY2 - interY1);
            float interArea = interW * interH;

            if (interArea <= 0.0f) return 0.0f;

            float unionArea = Area + other.Area - interArea;
            return unionArea > 0.0f ? interArea / unionArea : 0.0f;
        }

        public override string ToString() => $"Box({X1:F1},{Y1:F1},{X2:F1},{Y2:F1}) Score={Score:P1} Class={ClassId}";
    }

    /// <summary>
    /// High-performance Non-Maximum Suppression (NMS) for vision object detection and defect bounding boxes.
    /// </summary>
    public static class NonMaximumSuppression
    {
        public static List<BoundingBox> Filter(
            IEnumerable<BoundingBox> boxes,
            float confidenceThreshold = 0.25f,
            float iouThreshold = 0.45f,
            bool classAgnostic = false)
        {
            if (boxes == null) throw new ArgumentNullException(nameof(boxes));

            // Filter by confidence
            var candidateList = new List<BoundingBox>();
            foreach (var b in boxes)
            {
                if (b.Score >= confidenceThreshold)
                {
                    candidateList.Add(b);
                }
            }

            if (candidateList.Count <= 1) return candidateList;

            // Sort descending by score
            candidateList.Sort((a, b) => b.Score.CompareTo(a.Score));

            var results = new List<BoundingBox>();
            var suppressed = new bool[candidateList.Count];

            for (int i = 0; i < candidateList.Count; i++)
            {
                if (suppressed[i]) continue;

                var current = candidateList[i];
                results.Add(current);

                for (int j = i + 1; j < candidateList.Count; j++)
                {
                    if (suppressed[j]) continue;

                    var candidate = candidateList[j];
                    if (!classAgnostic && candidate.ClassId != current.ClassId)
                        continue;

                    float iou = current.IntersectionOverUnion(candidate);
                    if (iou > iouThreshold)
                    {
                        suppressed[j] = true;
                    }
                }
            }

            return results;
        }
    }
}
