using System.Collections.Generic;
using Xunit;
using ZeroInference.Core.Vision;

namespace ZeroInference.Tests
{
    public class NonMaximumSuppressionTests
    {
        [Fact]
        public void NMS_SuppressesOverlappingBoxes()
        {
            var boxes = new List<BoundingBox>
            {
                new BoundingBox(10, 10, 50, 50, score: 0.9f, classId: 0),
                new BoundingBox(12, 12, 52, 52, score: 0.75f, classId: 0), // Highly overlapping with first box
                new BoundingBox(100, 100, 140, 140, score: 0.85f, classId: 0) // Separate box
            };

            var filtered = NonMaximumSuppression.Filter(boxes, confidenceThreshold: 0.5f, iouThreshold: 0.4f);

            // Should suppress the 0.75 box and keep the 0.9 and 0.85 boxes
            Assert.Equal(2, filtered.Count);
            Assert.Equal(0.9f, filtered[0].Score);
            Assert.Equal(0.85f, filtered[1].Score);
        }

        [Fact]
        public void NMS_FiltersLowConfidenceBoxes()
        {
            var boxes = new List<BoundingBox>
            {
                new BoundingBox(10, 10, 30, 30, score: 0.2f),
                new BoundingBox(40, 40, 60, 60, score: 0.8f)
            };

            var filtered = NonMaximumSuppression.Filter(boxes, confidenceThreshold: 0.5f);

            Assert.Single(filtered);
            Assert.Equal(0.8f, filtered[0].Score);
        }
    }
}
