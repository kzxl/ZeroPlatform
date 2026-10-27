using System;
using System.Collections.Generic;

namespace ZeroInference.Core.Vision
{
    /// <summary>
    /// Represents an individual 2D keypoint detected by a pose estimation model (e.g. YOLOv8-pose / YOLOv11-pose).
    /// </summary>
    public readonly struct Keypoint
    {
        /// <summary>
        /// Gets the X coordinate in image space.
        /// </summary>
        public float X { get; }

        /// <summary>
        /// Gets the Y coordinate in image space.
        /// </summary>
        public float Y { get; }

        /// <summary>
        /// Gets the confidence score or visibility probability [0.0, 1.0].
        /// </summary>
        public float Confidence { get; }

        public Keypoint(float x, float y, float confidence)
        {
            X = x;
            Y = y;
            Confidence = confidence;
        }

        public override string ToString() => $"Keypoint({X:F1}, {Y:F1}, Conf={Confidence:P0})";
    }

    /// <summary>
    /// Represents an object detection paired with pose keypoints (e.g., human posture, robotic joints).
    /// </summary>
    public sealed class PoseDetection
    {
        /// <summary>
        /// Gets the bounding box surrounding the detected person or object.
        /// </summary>
        public BoundingBox Box { get; }

        /// <summary>
        /// Gets the array of detected keypoints (e.g. 17 COCO body joints).
        /// </summary>
        public Keypoint[] Keypoints { get; }

        public PoseDetection(BoundingBox box, Keypoint[] keypoints)
        {
            Box = box;
            Keypoints = keypoints ?? Array.Empty<Keypoint>();
        }

        public override string ToString() => $"PoseDetection({Box}, Keypoints={Keypoints.Length})";
    }
}
