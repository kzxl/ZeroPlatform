using System;
using System.Collections.Generic;
using Xunit;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Scene;

namespace ZeroUI.Core.Tests
{
    public class SceneGraphTests
    {
        private class TestNode : SceneNode
        {
            public TestNode(string id, float x, float y, float width, float height, int zIndex = 0)
            {
                Id = id;
                Transform.SetPosition(x, y);
                Width = width;
                Height = height;
                ZIndex = zIndex;
            }

            public override void Render(object graphicsContext, in RenderContext context)
            {
                // Test stub
            }
        }

        [Fact]
        public void Hierarchy_ParentChildTransformPropagation_UpdatesWorldBoundsCorrectly()
        {
            var parent = new TestNode("Parent", 100f, 200f, 200f, 200f);
            var child = new TestNode("Child", 25f, 35f, 50f, 40f);

            parent.AddChild(child);

            // Child world bounds should be: Parent(100, 200) + Child(25, 35) = (125, 235)
            var bounds = child.WorldBounds;
            Assert.Equal(125f, bounds.X);
            Assert.Equal(235f, bounds.Y);
            Assert.Equal(50f, bounds.Width);
            Assert.Equal(40f, bounds.Height);

            // Move parent
            parent.Transform.SetPosition(300f, 400f);

            var updatedBounds = child.WorldBounds;
            Assert.Equal(325f, updatedBounds.X);
            Assert.Equal(435f, updatedBounds.Y);
        }

        [Fact]
        public void GridSpatialIndex_FrustumCulling_ReturnsOnlyVisibleNodes()
        {
            var scene = new ZeroScene(new GridSpatialIndex(128f));

            // Place 5 nodes inside viewport [0..200, 0..200]
            for (int i = 0; i < 5; i++)
            {
                scene.AddNode(new TestNode($"Inside_{i}", i * 30f, i * 30f, 25f, 25f));
            }

            // Place 50 nodes far outside viewport [1000..5000]
            for (int i = 0; i < 50; i++)
            {
                scene.AddNode(new TestNode($"Outside_{i}", 1500f + i * 50f, 1500f + i * 50f, 30f, 30f));
            }

            var viewport = new SceneRect(0f, 0f, 200f, 200f);
            var visibleNodes = new List<SceneNode>();

            scene.QueryVisibleNodes(viewport, visibleNodes);

            // Exactly 5 nodes should be returned
            Assert.Equal(5, visibleNodes.Count);
            for (int i = 0; i < visibleNodes.Count; i++)
            {
                Assert.StartsWith("Inside_", visibleNodes[i].Id);
            }
        }

        [Fact]
        public void SpatialIndex_HitTest_FindsTopmostNodeByZIndex()
        {
            var scene = new ZeroScene(new GridSpatialIndex(128f));

            // Two overlapping nodes at the exact same location with different Z-Indices
            var bottomNode = new TestNode("Bottom", 50f, 50f, 100f, 100f, zIndex: 1);
            var topNode = new TestNode("Top", 50f, 50f, 100f, 100f, zIndex: 10);

            scene.AddNode(bottomNode);
            scene.AddNode(topNode);

            var hit = scene.HitTest(75f, 75f);
            Assert.NotNull(hit);
            Assert.Equal("Top", hit!.Id);
        }

        [Fact]
        public void ZeroScene_DispatchTagUpdate_UpdatesBoundNodeValue()
        {
            var scene = new ZeroScene();
            var node = new TestNode("Tank1", 0f, 0f, 100f, 100f)
            {
                TagPath = "Plant.Section1.TankLevel"
            };

            scene.AddNode(node);

            // Dispatch SCADA telemetry update
            var updateValue = new ScadaValue(82.4, ScadaQuality.Good);
            scene.DispatchTagUpdate("Plant.Section1.TankLevel", in updateValue);

            Assert.Equal(82.4, node.Value, precision: 1);
        }

        [Fact]
        public void SceneNode_SelectionAndHover_TriggerStateChanges()
        {
            var scene = new ZeroScene();
            var node1 = new TestNode("N1", 10f, 10f, 40f, 40f);
            var node2 = new TestNode("N2", 60f, 60f, 40f, 40f);

            scene.AddNode(node1);
            scene.AddNode(node2);

            scene.SelectedNode = node1;
            Assert.True(node1.IsSelected);
            Assert.False(node2.IsSelected);

            scene.SelectedNode = node2;
            Assert.False(node1.IsSelected);
            Assert.True(node2.IsSelected);

            scene.HoveredNode = node1;
            Assert.True(node1.IsHovered);
        }
    }
}
