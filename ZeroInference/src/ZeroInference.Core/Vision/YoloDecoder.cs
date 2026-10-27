using System;
using System.Collections.Generic;
using ZeroTensor.Core;

namespace ZeroInference.Core.Vision
{
    /// <summary>
    /// High-performance, pure C# anchor-free tensor decoder for YOLOv8, YOLOv9, YOLOv10, and YOLOv11 models.
    /// Decodes raw ONNX output tensors into bounding boxes and pose keypoints with zero external dependencies.
    /// </summary>
    public static class YoloDecoder
    {
        #region Object Detection Decoding

        /// <summary>
        /// Decodes a raw output tensor from a YOLO detection model into filtered bounding boxes.
        /// </summary>
        /// <param name="tensor">Output tensor from ONNX inference (typically 3D [1, channels, anchors] or [1, anchors, channels]).</param>
        /// <param name="confidenceThreshold">Minimum class confidence score threshold (default: 0.25f).</param>
        /// <param name="iouThreshold">Intersection-over-Union threshold for Non-Maximum Suppression (default: 0.45f).</param>
        /// <param name="origWidth">Original image width for scaling coordinates back (optional).</param>
        /// <param name="origHeight">Original image height for scaling coordinates back (optional).</param>
        /// <param name="modelWidth">Input width used by the model (default: 640).</param>
        /// <param name="modelHeight">Input height used by the model (default: 640).</param>
        /// <param name="useLetterbox">Whether coordinates were letterbox padded during preprocessing (default: true).</param>
        /// <param name="labels">Optional string label lookup array indexed by class ID.</param>
        /// <param name="classAgnostic">Whether NMS suppression is applied across all classes or per class.</param>
        public static List<BoundingBox> DecodeDetection(
            Tensor<float> tensor,
            float confidenceThreshold = 0.25f,
            float iouThreshold = 0.45f,
            int origWidth = 0,
            int origHeight = 0,
            int modelWidth = 640,
            int modelHeight = 640,
            bool useLetterbox = true,
            IReadOnlyList<string>? labels = null,
            bool classAgnostic = false)
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));

            var shape = tensor.Shape.Dimensions;
            int channels, anchors;
            bool isChannelsFirst;

            if (shape.Count != 2 && shape.Count != 3)
            {
                throw new ArgumentException($"Unsupported tensor rank {shape.Count}. Expected 2D or 3D tensor.", nameof(tensor));
            }

            int dim1 = shape.Count == 3 ? shape[1] : shape[0];
            int dim2 = shape.Count == 3 ? shape[2] : shape[1];

            if (dim2 < 5 || (dim1 <= 128 && dim1 <= dim2))
            {
                channels = dim1;
                anchors = dim2;
                isChannelsFirst = true;
            }
            else if (dim1 >= 256 || dim2 < dim1)
            {
                anchors = dim1;
                channels = dim2;
                isChannelsFirst = false;
            }
            else
            {
                channels = dim1;
                anchors = dim2;
                isChannelsFirst = true;
            }

            var span = tensor.AsSpan();
            int numClasses = channels - 4;
            if (numClasses <= 0)
                throw new ArgumentException($"Invalid channel count {channels}. Must have at least 5 channels (4 coordinates + classes).");

            return DecodeDetection(
                span,
                channels,
                anchors,
                isChannelsFirst,
                numClasses,
                confidenceThreshold,
                iouThreshold,
                origWidth,
                origHeight,
                modelWidth,
                modelHeight,
                useLetterbox,
                labels,
                classAgnostic);
        }

        /// <summary>
        /// Decodes a raw span of floats representing a YOLO detection output tensor.
        /// </summary>
        public static List<BoundingBox> DecodeDetection(
            ReadOnlySpan<float> span,
            int channels,
            int anchors,
            bool isChannelsFirst,
            int numClasses,
            float confidenceThreshold = 0.25f,
            float iouThreshold = 0.45f,
            int origWidth = 0,
            int origHeight = 0,
            int modelWidth = 640,
            int modelHeight = 640,
            bool useLetterbox = true,
            IReadOnlyList<string>? labels = null,
            bool classAgnostic = false)
        {
            var candidates = new List<BoundingBox>();

            CalculateScaleParameters(
                origWidth, origHeight, modelWidth, modelHeight, useLetterbox,
                out float gain, out float padX, out float padY, out float scaleX, out float scaleY);

            for (int i = 0; i < anchors; i++)
            {
                float cx, cy, w, h;

                if (isChannelsFirst)
                {
                    cx = span[0 * anchors + i];
                    cy = span[1 * anchors + i];
                    w  = span[2 * anchors + i];
                    h  = span[3 * anchors + i];
                }
                else
                {
                    int offset = i * channels;
                    cx = span[offset + 0];
                    cy = span[offset + 1];
                    w  = span[offset + 2];
                    h  = span[offset + 3];
                }

                // Find highest scoring class
                float maxScore = -1.0f;
                int maxClassId = -1;

                for (int c = 0; c < numClasses; c++)
                {
                    float score;
                    if (isChannelsFirst)
                    {
                        score = span[(4 + c) * anchors + i];
                    }
                    else
                    {
                        score = span[i * channels + 4 + c];
                    }

                    if (score > maxScore)
                    {
                        maxScore = score;
                        maxClassId = c;
                    }
                }

                if (maxScore < confidenceThreshold) continue;

                float x1 = cx - w * 0.5f;
                float y1 = cy - h * 0.5f;
                float x2 = cx + w * 0.5f;
                float y2 = cy + h * 0.5f;

                RescaleCoordinates(ref x1, ref y1, ref x2, ref y2, origWidth, origHeight, useLetterbox, gain, padX, padY, scaleX, scaleY);

                string? label = (labels != null && maxClassId >= 0 && maxClassId < labels.Count)
                    ? labels[maxClassId]
                    : null;

                candidates.Add(new BoundingBox(x1, y1, x2, y2, maxScore, maxClassId, label));
            }

            return NonMaximumSuppression.Filter(candidates, confidenceThreshold, iouThreshold, classAgnostic);
        }

        #endregion

        #region Pose Estimation Decoding

        /// <summary>
        /// Decodes a raw output tensor from a YOLO pose estimation model into detected bounding boxes with keypoints.
        /// </summary>
        public static List<PoseDetection> DecodePose(
            Tensor<float> tensor,
            int numKeypoints = 17,
            float confidenceThreshold = 0.25f,
            float iouThreshold = 0.45f,
            int origWidth = 0,
            int origHeight = 0,
            int modelWidth = 640,
            int modelHeight = 640,
            bool useLetterbox = true,
            IReadOnlyList<string>? labels = null,
            bool classAgnostic = false)
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));

            var shape = tensor.Shape.Dimensions;
            int channels, anchors;
            bool isChannelsFirst;

            if (shape.Count != 2 && shape.Count != 3)
            {
                throw new ArgumentException($"Unsupported tensor rank {shape.Count}. Expected 2D or 3D tensor.", nameof(tensor));
            }

            int minPoseChannels = 4 + 1 + (numKeypoints * 3);
            int dim1 = shape.Count == 3 ? shape[1] : shape[0];
            int dim2 = shape.Count == 3 ? shape[2] : shape[1];

            if (dim2 < minPoseChannels || (dim1 <= 256 && dim1 <= dim2))
            {
                channels = dim1;
                anchors = dim2;
                isChannelsFirst = true;
            }
            else
            {
                anchors = dim1;
                channels = dim2;
                isChannelsFirst = false;
            }

            var span = tensor.AsSpan();
            int numClasses = channels - 4 - (numKeypoints * 3);
            if (numClasses <= 0)
                throw new ArgumentException($"Invalid channel count {channels} for {numKeypoints} keypoints.");

            return DecodePose(
                span,
                channels,
                anchors,
                isChannelsFirst,
                numClasses,
                numKeypoints,
                confidenceThreshold,
                iouThreshold,
                origWidth,
                origHeight,
                modelWidth,
                modelHeight,
                useLetterbox,
                labels,
                classAgnostic);
        }

        /// <summary>
        /// Decodes a raw span of floats representing a YOLO pose estimation output tensor.
        /// </summary>
        public static List<PoseDetection> DecodePose(
            ReadOnlySpan<float> span,
            int channels,
            int anchors,
            bool isChannelsFirst,
            int numClasses,
            int numKeypoints,
            float confidenceThreshold = 0.25f,
            float iouThreshold = 0.45f,
            int origWidth = 0,
            int origHeight = 0,
            int modelWidth = 640,
            int modelHeight = 640,
            bool useLetterbox = true,
            IReadOnlyList<string>? labels = null,
            bool classAgnostic = false)
        {
            var candidatePoses = new List<(BoundingBox Box, Keypoint[] Keypoints)>();

            CalculateScaleParameters(
                origWidth, origHeight, modelWidth, modelHeight, useLetterbox,
                out float gain, out float padX, out float padY, out float scaleX, out float scaleY);

            for (int i = 0; i < anchors; i++)
            {
                float cx, cy, w, h;

                if (isChannelsFirst)
                {
                    cx = span[0 * anchors + i];
                    cy = span[1 * anchors + i];
                    w  = span[2 * anchors + i];
                    h  = span[3 * anchors + i];
                }
                else
                {
                    int offset = i * channels;
                    cx = span[offset + 0];
                    cy = span[offset + 1];
                    w  = span[offset + 2];
                    h  = span[offset + 3];
                }

                // Find highest scoring class
                float maxScore = -1.0f;
                int maxClassId = -1;

                for (int c = 0; c < numClasses; c++)
                {
                    float score;
                    if (isChannelsFirst)
                    {
                        score = span[(4 + c) * anchors + i];
                    }
                    else
                    {
                        score = span[i * channels + 4 + c];
                    }

                    if (score > maxScore)
                    {
                        maxScore = score;
                        maxClassId = c;
                    }
                }

                if (maxScore < confidenceThreshold) continue;

                float x1 = cx - w * 0.5f;
                float y1 = cy - h * 0.5f;
                float x2 = cx + w * 0.5f;
                float y2 = cy + h * 0.5f;

                RescaleCoordinates(ref x1, ref y1, ref x2, ref y2, origWidth, origHeight, useLetterbox, gain, padX, padY, scaleX, scaleY);

                string? label = (labels != null && maxClassId >= 0 && maxClassId < labels.Count)
                    ? labels[maxClassId]
                    : null;

                var box = new BoundingBox(x1, y1, x2, y2, maxScore, maxClassId, label);

                // Extract Keypoints
                var keypoints = new Keypoint[numKeypoints];
                int kptBaseOffset = 4 + numClasses;

                for (int k = 0; k < numKeypoints; k++)
                {
                    float kx, ky, kconf;
                    if (isChannelsFirst)
                    {
                        kx    = span[(kptBaseOffset + k * 3 + 0) * anchors + i];
                        ky    = span[(kptBaseOffset + k * 3 + 1) * anchors + i];
                        kconf = span[(kptBaseOffset + k * 3 + 2) * anchors + i];
                    }
                    else
                    {
                        int offset = i * channels + kptBaseOffset + k * 3;
                        kx    = span[offset + 0];
                        ky    = span[offset + 1];
                        kconf = span[offset + 2];
                    }

                    RescalePoint(ref kx, ref ky, origWidth, origHeight, useLetterbox, gain, padX, padY, scaleX, scaleY);
                    keypoints[k] = new Keypoint(kx, ky, kconf);
                }

                candidatePoses.Add((box, keypoints));
            }

            if (candidatePoses.Count == 0) return new List<PoseDetection>();

            // Sort candidate poses descending by box score
            candidatePoses.Sort((a, b) => b.Box.Score.CompareTo(a.Box.Score));

            // NMS filtering over candidate poses
            var results = new List<PoseDetection>();
            var suppressed = new bool[candidatePoses.Count];

            for (int i = 0; i < candidatePoses.Count; i++)
            {
                if (suppressed[i]) continue;

                var current = candidatePoses[i];
                results.Add(new PoseDetection(current.Box, current.Keypoints));

                for (int j = i + 1; j < candidatePoses.Count; j++)
                {
                    if (suppressed[j]) continue;

                    var candidate = candidatePoses[j];
                    if (!classAgnostic && candidate.Box.ClassId != current.Box.ClassId)
                        continue;

                    float iou = current.Box.IntersectionOverUnion(candidate.Box);
                    if (iou > iouThreshold)
                    {
                        suppressed[j] = true;
                    }
                }
            }

            return results;
        }

        #endregion

        #region Coordinate Scaling Helpers

        private static void CalculateScaleParameters(
            int origWidth, int origHeight, int modelWidth, int modelHeight, bool useLetterbox,
            out float gain, out float padX, out float padY, out float scaleX, out float scaleY)
        {
            if (origWidth > 0 && origHeight > 0)
            {
                if (useLetterbox)
                {
                    gain = Math.Min((float)modelWidth / origWidth, (float)modelHeight / origHeight);
                    padX = (modelWidth - origWidth * gain) * 0.5f;
                    padY = (modelHeight - origHeight * gain) * 0.5f;
                    scaleX = 1f;
                    scaleY = 1f;
                }
                else
                {
                    gain = 1f;
                    padX = 0f;
                    padY = 0f;
                    scaleX = (float)origWidth / modelWidth;
                    scaleY = (float)origHeight / modelHeight;
                }
            }
            else
            {
                gain = 1f;
                padX = 0f;
                padY = 0f;
                scaleX = 1f;
                scaleY = 1f;
            }
        }

        private static void RescaleCoordinates(
            ref float x1, ref float y1, ref float x2, ref float y2,
            int origWidth, int origHeight, bool useLetterbox,
            float gain, float padX, float padY, float scaleX, float scaleY)
        {
            if (origWidth <= 0 || origHeight <= 0) return;

            if (useLetterbox)
            {
                x1 = (x1 - padX) / gain;
                x2 = (x2 - padX) / gain;
                y1 = (y1 - padY) / gain;
                y2 = (y2 - padY) / gain;
            }
            else
            {
                x1 *= scaleX;
                x2 *= scaleX;
                y1 *= scaleY;
                y2 *= scaleY;
            }

            x1 = Math.Max(0.0f, Math.Min(origWidth, x1));
            x2 = Math.Max(0.0f, Math.Min(origWidth, x2));
            y1 = Math.Max(0.0f, Math.Min(origHeight, y1));
            y2 = Math.Max(0.0f, Math.Min(origHeight, y2));
        }

        private static void RescalePoint(
            ref float x, ref float y,
            int origWidth, int origHeight, bool useLetterbox,
            float gain, float padX, float padY, float scaleX, float scaleY)
        {
            if (origWidth <= 0 || origHeight <= 0) return;

            if (useLetterbox)
            {
                x = (x - padX) / gain;
                y = (y - padY) / gain;
            }
            else
            {
                x *= scaleX;
                y *= scaleY;
            }

            x = Math.Max(0.0f, Math.Min(origWidth, x));
            y = Math.Max(0.0f, Math.Min(origHeight, y));
        }

        #endregion
    }
}
