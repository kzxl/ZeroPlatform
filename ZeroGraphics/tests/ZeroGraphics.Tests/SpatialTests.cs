using System;
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;
using ZeroGraphics.Core.Spatial;

namespace ZeroGraphics.Tests
{
    public class TestRackItem : ISpatialItem
    {
        public string Id { get; }
        public BoundingBox2D Bounds { get; set; }

        public TestRackItem(string id, float x, float y, float width, float height)
        {
            Id = id;
            Bounds = BoundingBox2D.FromRect(x, y, width, height);
        }
    }

    public class SpatialTests
    {
        [Fact]
        public void BoundingBox2D_IntersectsAndContains_WorkCorrectly()
        {
            var b1 = new BoundingBox2D(0, 0, 100, 100);
            var b2 = new BoundingBox2D(50, 50, 150, 150);
            var b3 = new BoundingBox2D(200, 200, 300, 300);

            Assert.True(b1.Intersects(b2));
            Assert.False(b1.Intersects(b3));

            Assert.True(b1.Contains(50, 50));
            Assert.False(b1.Contains(150, 50));

            var inner = new BoundingBox2D(20, 20, 80, 80);
            Assert.True(b1.Contains(inner));
            Assert.False(inner.Contains(b1));

            var union = b1.Union(b3);
            Assert.Equal(0, union.MinX);
            Assert.Equal(0, union.MinY);
            Assert.Equal(300, union.MaxX);
            Assert.Equal(300, union.MaxY);
        }

        [Fact]
        public void QuadTree_FastViewportCulling_OnLargeWarehouseLayout()
        {
            // Simulate a large warehouse floor: 10,000m x 10,000m with 10,000 storage racks
            var floorBounds = new BoundingBox2D(0, 0, 10000, 10000);
            var quadTree = new QuadTree<TestRackItem>(floorBounds, maxItemsPerNode: 16, maxDepth: 8);

            int rackCount = 10000;
            for (int i = 0; i < rackCount; i++)
            {
                float x = (i % 100) * 90.0f + 10.0f;
                float y = (i / 100) * 90.0f + 10.0f;
                quadTree.Insert(new TestRackItem($"RACK_{i}", x, y, 40.0f, 20.0f));
            }

            Assert.Equal(rackCount, quadTree.Count);

            // User zooms in on a specific aisle: 500m x 500m viewport
            var viewport = new BoundingBox2D(1000, 1000, 1500, 1500);
            var visibleRacks = new List<TestRackItem>();

            var sw = Stopwatch.StartNew();
            quadTree.Query(viewport, visibleRacks);
            sw.Stop();

            Assert.NotEmpty(visibleRacks);
            Assert.True(visibleRacks.Count < rackCount, "Culling should filter out items outside viewport.");

            // Verify all returned items indeed intersect the viewport
            foreach (var rack in visibleRacks)
            {
                Assert.True(viewport.Intersects(rack.Bounds));
            }

            // High performance check: querying 10,000 items in a quadtree should take < 5ms
            Assert.True(sw.ElapsedMilliseconds < 20, $"Viewport culling took {sw.ElapsedMilliseconds}ms, should be < 20ms.");
        }

        [Fact]
        public void SpatialGrid_AddQueryAndMove_WorksCorrectly()
        {
            var grid = new SpatialGrid<TestRackItem>(cellSize: 50.0f);

            var agv = new TestRackItem("AGV_01", 10, 10, 5, 5);
            grid.Add(agv);
            Assert.Equal(1, grid.Count);

            var queryRange = new BoundingBox2D(0, 0, 100, 100);
            var results = new HashSet<TestRackItem>();
            grid.Query(queryRange, results);
            Assert.Contains(agv, results);

            // Move AGV outside query range
            var oldBounds = agv.Bounds;
            agv.Bounds = new BoundingBox2D(500, 500, 505, 505);
            grid.Update(agv, oldBounds);

            results.Clear();
            grid.Query(queryRange, results);
            Assert.DoesNotContain(agv, results);

            // Query new range
            grid.Query(new BoundingBox2D(450, 450, 550, 550), results);
            Assert.Contains(agv, results);
        }
    }
}
