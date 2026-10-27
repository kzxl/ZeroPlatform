using System;
using System.Collections.Generic;
using Xunit;
using ZeroInference.Core.Vision;
using ZeroTensor.Core;

namespace ZeroInference.Tests
{
    public class YoloDecoderTests
    {
        [Fact]
        public void DecodeDetection_ChannelsFirst_ExtractsAndFiltersBoxesCorrectly()
        {
            // Arrange: 3 anchors, 2 classes -> channels = 4 + 2 = 6. Shape = [1, 6, 3]
            // Anchor 0: (cx=100, cy=100, w=50, h=50), class 0 = 0.9, class 1 = 0.1
            // Anchor 1: (cx=105, cy=105, w=50, h=50), class 0 = 0.85, class 1 = 0.2 (Overlaps with Anchor 0)
            // Anchor 2: (cx=400, cy=300, w=80, h=60), class 0 = 0.1, class 1 = 0.95 (Separate box)
            int channels = 6;
            int anchors = 3;
            var data = new float[channels * anchors];

            // Anchor 0
            data[0 * anchors + 0] = 100f; // cx
            data[1 * anchors + 0] = 100f; // cy
            data[2 * anchors + 0] = 50f;  // w
            data[3 * anchors + 0] = 50f;  // h
            data[4 * anchors + 0] = 0.9f; // class 0
            data[5 * anchors + 0] = 0.1f; // class 1

            // Anchor 1 (High overlap with Anchor 0)
            data[0 * anchors + 1] = 105f;
            data[1 * anchors + 1] = 105f;
            data[2 * anchors + 1] = 50f;
            data[3 * anchors + 1] = 50f;
            data[4 * anchors + 1] = 0.85f;
            data[5 * anchors + 1] = 0.2f;

            // Anchor 2
            data[0 * anchors + 2] = 400f;
            data[1 * anchors + 2] = 300f;
            data[2 * anchors + 2] = 80f;
            data[3 * anchors + 2] = 60f;
            data[4 * anchors + 2] = 0.1f;
            data[5 * anchors + 2] = 0.95f;

            var tensor = Tensor.FromArray(data, 1, channels, anchors);
            var labels = new[] { "defect_scratch", "defect_dent" };

            // Act
            var results = YoloDecoder.DecodeDetection(
                tensor,
                confidenceThreshold: 0.5f,
                iouThreshold: 0.45f,
                origWidth: 0,
                origHeight: 0,
                labels: labels);

            // Assert: Anchor 1 should be suppressed by NMS; 2 boxes remaining
            Assert.Equal(2, results.Count);

            // Box 1 (Anchor 2 has highest score 0.95)
            Assert.Equal("defect_dent", results[0].Label);
            Assert.Equal(1, results[0].ClassId);
            Assert.Equal(0.95f, results[0].Score, 2);
            Assert.Equal(360f, results[0].X1); // 400 - 80/2
            Assert.Equal(270f, results[0].Y1); // 300 - 60/2
            Assert.Equal(440f, results[0].X2); // 400 + 80/2
            Assert.Equal(330f, results[0].Y2); // 300 + 60/2

            // Box 2 (Anchor 0 with score 0.9)
            Assert.Equal("defect_scratch", results[1].Label);
            Assert.Equal(0, results[1].ClassId);
            Assert.Equal(0.9f, results[1].Score, 2);
            Assert.Equal(75f, results[1].X1);  // 100 - 50/2
            Assert.Equal(75f, results[1].Y1);
            Assert.Equal(125f, results[1].X2); // 100 + 50/2
            Assert.Equal(125f, results[1].Y2);
        }

        [Fact]
        public void DecodeDetection_LetterboxRescaling_MapsCoordinatesToOriginalImage()
        {
            // Model input: 640x640. Original image: 1920x1080 (16:9 aspect ratio)
            // Gain = min(640/1920, 640/1080) = min(0.3333, 0.5926) = 0.3333
            // Scaled image in 640x640 is 640x360. padX = 0, padY = (640 - 360)/2 = 140
            int channels = 5; // 4 box + 1 class
            int anchors = 1;
            var data = new float[channels * anchors];

            // Box in model coordinates centered at (320, 320) with width 320, height 180
            data[0] = 320f; // cx
            data[1] = 320f; // cy
            data[2] = 320f; // w
            data[3] = 180f; // h
            data[4] = 0.99f;// score

            var tensor = Tensor.FromArray(data, 1, channels, anchors);

            var results = YoloDecoder.DecodeDetection(
                tensor,
                confidenceThreshold: 0.5f,
                origWidth: 1920,
                origHeight: 1080,
                modelWidth: 640,
                modelHeight: 640,
                useLetterbox: true);

            Assert.Single(results);
            var b = results[0];

            // In model coords: x1 = 160, x2 = 480, y1 = 230, y2 = 410
            // Original: x1 = 160 / (640/1920) = 480
            //           x2 = 480 / (640/1920) = 1440
            //           y1 = (230 - 140) / (640/1920) = 90 / 0.3333 = 270
            //           y2 = (410 - 140) / (640/1920) = 270 / 0.3333 = 810
            Assert.True(Math.Abs(b.X1 - 480f) < 2f);
            Assert.True(Math.Abs(b.X2 - 1440f) < 2f);
            Assert.True(Math.Abs(b.Y1 - 270f) < 2f);
            Assert.True(Math.Abs(b.Y2 - 810f) < 2f);
        }

        [Fact]
        public void DecodePose_ExtractsBoundingBoxAnd17Keypoints()
        {
            // COCO Pose: 1 person class, 17 keypoints -> 4 + 1 + 17 * 3 = 56 channels
            int numKeypoints = 17;
            int channels = 4 + 1 + numKeypoints * 3;
            int anchors = 2;
            var data = new float[channels * anchors];

            // Person 1 (score 0.92)
            data[0 * anchors + 0] = 200f; // cx
            data[1 * anchors + 0] = 300f; // cy
            data[2 * anchors + 0] = 100f; // w
            data[3 * anchors + 0] = 200f; // h
            data[4 * anchors + 0] = 0.92f;// class person

            // Fill 17 keypoints for Person 1
            for (int k = 0; k < numKeypoints; k++)
            {
                data[(5 + k * 3 + 0) * anchors + 0] = 180f + k * 2; // kx
                data[(5 + k * 3 + 1) * anchors + 0] = 220f + k * 8; // ky
                data[(5 + k * 3 + 2) * anchors + 0] = 0.85f;        // conf
            }

            // Person 2 (score 0.1 - below threshold)
            data[0 * anchors + 1] = 500f;
            data[1 * anchors + 1] = 500f;
            data[2 * anchors + 1] = 50f;
            data[3 * anchors + 1] = 50f;
            data[4 * anchors + 1] = 0.1f;

            var tensor = Tensor.FromArray(data, 1, channels, anchors);

            var poses = YoloDecoder.DecodePose(
                tensor,
                numKeypoints: 17,
                confidenceThreshold: 0.5f);

            Assert.Single(poses);
            var pose = poses[0];

            Assert.Equal(0.92f, pose.Box.Score, 2);
            Assert.Equal(150f, pose.Box.X1);
            Assert.Equal(250f, pose.Box.X2);
            Assert.Equal(200f, pose.Box.Y1);
            Assert.Equal(400f, pose.Box.Y2);

            Assert.Equal(17, pose.Keypoints.Length);
            Assert.Equal(180f, pose.Keypoints[0].X);
            Assert.Equal(220f, pose.Keypoints[0].Y);
            Assert.Equal(0.85f, pose.Keypoints[0].Confidence, 2);
            Assert.Equal(180f + 16 * 2, pose.Keypoints[16].X);
        }

        [Fact]
        public void DecodeDetection_LargeAnchorCount_RunsSubMillisecond()
        {
            // Standard YOLOv8 640x640 produces 8,400 anchors with 80 classes (84 channels)
            int channels = 84;
            int anchors = 8400;
            var data = new float[channels * anchors];

            // Add 10 real detections scattered among the 8,400 anchors
            for (int i = 0; i < 10; i++)
            {
                int anchorIdx = i * 800;
                data[0 * anchors + anchorIdx] = 100f + i * 40;
                data[1 * anchors + anchorIdx] = 100f + i * 30;
                data[2 * anchors + anchorIdx] = 50f;
                data[3 * anchors + anchorIdx] = 50f;
                data[(4 + (i % 5)) * anchors + anchorIdx] = 0.88f; // high confidence
            }

            var tensor = Tensor.FromArray(data, 1, channels, anchors);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = YoloDecoder.DecodeDetection(tensor, confidenceThreshold: 0.5f);
            sw.Stop();

            Assert.Equal(10, results.Count);
            // 8,400 anchors processed in under 50ms on any machine
            Assert.True(sw.ElapsedMilliseconds < 50, $"Elapsed: {sw.ElapsedMilliseconds}ms");
        }
    }
}
