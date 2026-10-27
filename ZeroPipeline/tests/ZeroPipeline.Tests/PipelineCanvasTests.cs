using System;
using System.Drawing;
using Xunit;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;
using ZeroPipeline.Nodes.Inspection;
using ZeroPipeline.Recipe.Models;
using ZeroPipeline.UI.Controls;
using ZeroPipeline.UI.Models;
using ZeroPipeline.UI.Studio;

namespace ZeroPipeline.Tests
{
    public class PipelineCanvasTests
    {
        [Fact]
        public void CanvasTransform_WorldToScreen_And_ScreenToWorld_Invertibility()
        {
            var transform = new CanvasTransform(100f, 50f, 1.5f);
            var originalWorld = new PointF(345.6f, 789.1f);

            var screen = transform.WorldToScreen(originalWorld);
            var restoredWorld = transform.ScreenToWorld(screen);

            Assert.Equal(originalWorld.X, restoredWorld.X, 3);
            Assert.Equal(originalWorld.Y, restoredWorld.Y, 3);
        }

        [Fact]
        public void CanvasTransform_ZoomAt_PreservesPivotWorldPoint()
        {
            var transform = new CanvasTransform(50f, 50f, 1.0f);
            var screenPivot = new PointF(400f, 300f);

            var worldBefore = transform.ScreenToWorld(screenPivot);
            transform.ZoomAt(screenPivot, 2.0f);
            var worldAfter = transform.ScreenToWorld(screenPivot);

            Assert.Equal(worldBefore.X, worldAfter.X, 3);
            Assert.Equal(worldBefore.Y, worldAfter.Y, 3);
            Assert.Equal(2.0f, transform.Zoom, 3);
        }

        [Fact]
        public void CanvasPortPin_ResolvePinColor_MatchesDataTypes()
        {
            Assert.Equal(Color.FromArgb(0, 229, 255), CanvasPortPin.ResolvePinColor(typeof(byte[]))); // Vision / Image
            Assert.Equal(Color.FromArgb(0, 255, 136), CanvasPortPin.ResolvePinColor(typeof(double))); // Numeric
            Assert.Equal(Color.FromArgb(179, 136, 255), CanvasPortPin.ResolvePinColor(typeof(InspectionResult))); // Inspection
            Assert.Equal(Color.FromArgb(255, 152, 0), CanvasPortPin.ResolvePinColor(typeof(string))); // Barcode / String
            Assert.Equal(Color.FromArgb(244, 67, 54), CanvasPortPin.ResolvePinColor(typeof(bool))); // Bool
        }

        [Fact]
        public void CanvasNode_LayoutAndPinHitTesting()
        {
            var node = new CanvasNode("node1", "Test Node", "TestType", "Vision", 100f, 200f);
            var inPin = node.AddInputPin("In1", typeof(byte[]));
            var outPin = node.AddOutputPin("Out1", typeof(string));

            Assert.Equal(100f, node.X);
            Assert.Equal(200f, node.Y);

            // In pin should be on left edge (X = 0 local)
            Assert.Equal(0f, inPin.LocalOffset.X);
            Assert.Equal(node.X, inPin.GetWorldCenter().X);

            // Out pin should be on right edge (X = Width local)
            Assert.Equal(node.Width, outPin.LocalOffset.X);
            Assert.Equal(node.X + node.Width, outPin.GetWorldCenter().X);

            // Node bounds hit testing
            Assert.True(node.ContainsPoint(new PointF(150f, 220f)));
            Assert.False(node.ContainsPoint(new PointF(50f, 50f)));

            // Pin hit testing
            var hitPin = node.FindPinAt(inPin.GetWorldCenter());
            Assert.NotNull(hitPin);
            Assert.Same(inPin, hitPin);
        }

        [Fact]
        public void CanvasConnection_BezierGeometry_And_HitTesting()
        {
            var srcNode = new CanvasNode("n1", "Source", "Type1", "General", 50f, 50f);
            var tgtNode = new CanvasNode("n2", "Target", "Type2", "General", 350f, 50f);

            var outPin = srcNode.AddOutputPin("Out", typeof(byte[]));
            var inPin = tgtNode.AddInputPin("In", typeof(byte[]));

            var conn = new CanvasConnection(outPin, inPin);

            conn.GetControlPoints(out var p0, out var p1, out var p2, out var p3);
            Assert.True(p1.X > p0.X);
            Assert.True(p2.X < p3.X);

            // Midpoint evaluation
            var mid = CanvasConnection.EvaluateBezier(p0, p1, p2, p3, 0.5f);
            Assert.True(conn.Hits(mid, tolerance: 10.0f));

            // Far away point should not hit
            Assert.False(conn.Hits(new PointF(1000f, 1000f), tolerance: 5.0f));
        }

        [Fact]
        public void ZeroPipelineCanvas_AddConnectAndRemove()
        {
            var canvas = new ZeroPipelineCanvas();
            var n1 = new CanvasNode("cam", "Camera", "CameraNode", "Vision", 50f, 50f);
            var n2 = new CanvasNode("caliper", "Caliper", "CaliperNode", "Inspection", 350f, 50f);

            var outPin = n1.AddOutputPin("Image", typeof(byte[]));
            var inPin = n2.AddInputPin("Image", typeof(byte[]));

            canvas.AddNode(n1);
            canvas.AddNode(n2);
            Assert.Equal(2, canvas.Nodes.Count);

            var conn = canvas.Connect(outPin, inPin);
            Assert.NotNull(conn);
            Assert.Single(canvas.Connections);

            // Incompatible type connection should be rejected
            var stringPin = n2.AddInputPin("Name", typeof(string));
            var badConn = canvas.Connect(outPin, stringPin);
            Assert.Null(badConn);
            Assert.Single(canvas.Connections);

            // Removing node removes attached connections
            canvas.RemoveNode(n1);
            Assert.Single(canvas.Nodes);
            Assert.Empty(canvas.Connections);
        }

        [Fact]
        public void ZeroPipelineStudio_RecipeExportAndImport_Roundtrip()
        {
            var studio = new ZeroPipelineStudioControl();

            var cam = new CanvasNode("cam01", "Camera 1", "SyntheticCameraNode", "Vision", 100f, 120f);
            cam.AddOutputPin("Frame", typeof(byte[]));
            cam.Properties["FrameRate"] = "60";

            var thresh = new CanvasNode("thresh01", "Threshold 1", "ImageThresholdNode", "Vision", 400f, 120f);
            thresh.AddInputPin("SourceImage", typeof(byte[]));
            thresh.AddOutputPin("BinaryImage", typeof(byte[]));
            thresh.Properties["Threshold"] = "150";

            studio.Canvas.AddNode(cam);
            studio.Canvas.AddNode(thresh);
            studio.Canvas.Connect(cam.Outputs[0], thresh.Inputs[0]);

            // Export to JSON
            string json = studio.SaveRecipeJson();
            Assert.Contains("cam01", json);
            Assert.Contains("thresh01", json);
            Assert.Contains("FrameRate", json);
            Assert.Contains("Threshold", json);

            // Reload into new studio
            var studio2 = new ZeroPipelineStudioControl();
            studio2.LoadRecipeJson(json);

            Assert.Equal(2, studio2.Canvas.Nodes.Count);
            Assert.Single(studio2.Canvas.Connections);

            var loadedCam = studio2.Canvas.Nodes.Find(n => n.Id == "cam01");
            Assert.NotNull(loadedCam);
            Assert.Equal(100f, loadedCam.X);
            Assert.Equal(120f, loadedCam.Y);
            Assert.Equal("60", loadedCam.Properties["FrameRate"]);
        }
    }
}
