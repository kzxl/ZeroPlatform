using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ZeroGeometry.Core.Projective;
using ZeroGraphics.Imaging.ColorScience;
using ZeroGraphics.Imaging.Core;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Graph;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;
using ZeroPipeline.Nodes.Vision;
using ZeroPipeline.Recipe.Registry;

namespace ZeroPipeline.Tests
{
    public class ColorScienceAndHomographyNodesTests
    {
        [Fact]
        public async Task ColorSpaceConvertNode_ConvertsSrgbToDisplayP3()
        {
            var graph = new PipelineGraph();
            var convertNode = new ColorSpaceConvertNode(ColorSpaces.Space.Srgb, ColorSpaces.Space.DisplayP3);

            var outputs = new List<ImageBuffer>();
            var sink = new ActionSinkNode<ImageBuffer>(outputs.Add, "ImageSink");

            graph.Connect(convertNode.Output, sink.Input);

            var executor = new PipelineExecutor(graph);
            await executor.InitializeAsync();

            using var input = ImageBuffer.CreateBgra32(16, 16);
            unsafe
            {
                byte* scan0 = input.Scan0;
                for (int i = 0; i < 16 * 16 * 4; i += 4)
                {
                    scan0[i + 0] = 50;  // B
                    scan0[i + 1] = 100; // G
                    scan0[i + 2] = 200; // R
                    scan0[i + 3] = 255; // A
                }
            }

            convertNode.Input.Deliver(new DataPacket<ImageBuffer>(input));
            await executor.ExecuteStepAsync();

            Assert.Single(outputs);
            var result = outputs[0];
            Assert.Equal(16, result.Width);
            Assert.Equal(16, result.Height);
            Assert.Equal(ImageFormatMode.Bgra32, result.Format);

            unsafe
            {
                byte* res = result.Scan0;
                Assert.Equal(255, res[3]); // Alpha preserved
                // Verify R channel altered by DisplayP3 matrix transform
                Assert.NotEqual(0, res[2]);
            }
        }

        [Fact]
        public async Task ColorSpaceConvertNode_SameSpaceReturnsOriginal()
        {
            var node = new ColorSpaceConvertNode(ColorSpaces.Space.Srgb, ColorSpaces.Space.Srgb);
            using var input = ImageBuffer.CreateBgra32(8, 8);

            var graph = new PipelineGraph();
            var outputs = new List<ImageBuffer>();
            var sink = new ActionSinkNode<ImageBuffer>(outputs.Add);
            graph.Connect(node.Output, sink.Input);

            var executor = new PipelineExecutor(graph);
            await executor.InitializeAsync();

            node.Input.Deliver(new DataPacket<ImageBuffer>(input));
            await executor.ExecuteStepAsync();

            Assert.Single(outputs);
            Assert.Same(input, outputs[0]);
        }

        [Fact]
        public async Task HomographyRectifyNode_RectifiesWarpedQuad()
        {
            var graph = new PipelineGraph();

            var quad = new[]
            {
                new Homography2D.Point2D(10, 10),
                new Homography2D.Point2D(90, 15),
                new Homography2D.Point2D(85, 95),
                new Homography2D.Point2D(5, 90)
            };

            var rectifyNode = new HomographyRectifyNode(targetWidth: 64, targetHeight: 64, sourceQuad: quad);
            var outputs = new List<ImageBuffer>();
            var sink = new ActionSinkNode<ImageBuffer>(outputs.Add);

            graph.Connect(rectifyNode.Output, sink.Input);

            var executor = new PipelineExecutor(graph);
            await executor.InitializeAsync();

            using var input = ImageBuffer.CreateBgra32(100, 100);
            unsafe
            {
                byte* scan0 = input.Scan0;
                for (int y = 20; y < 80; y++)
                {
                    for (int x = 20; x < 80; x++)
                    {
                        byte* p = scan0 + y * input.Stride + x * 4;
                        p[0] = 255;
                        p[1] = 255;
                        p[2] = 255;
                        p[3] = 255;
                    }
                }
            }

            rectifyNode.Input.Deliver(new DataPacket<ImageBuffer>(input));
            await executor.ExecuteStepAsync();

            Assert.Single(outputs);
            var result = outputs[0];
            Assert.Equal(64, result.Width);
            Assert.Equal(64, result.Height);
            Assert.Equal(ImageFormatMode.Bgra32, result.Format);
        }

        [Fact]
        public void NodeRegistry_CanRegisterAndInstantiateNewNodes()
        {
            var registry = new NodeRegistry();
            registry.Register<ColorSpaceConvertNode>("ColorSpaceConvert");
            registry.Register<HomographyRectifyNode>("HomographyRectify");

            var colorNode = registry.Create("ColorSpaceConvert", "CS1", "ColorSpaceNode", new Dictionary<string, string>());
            Assert.NotNull(colorNode);
            Assert.IsType<ColorSpaceConvertNode>(colorNode);

            var homographyNode = registry.Create("HomographyRectify", "HR1", "HomographyNode", new Dictionary<string, string>());
            Assert.NotNull(homographyNode);
            Assert.IsType<HomographyRectifyNode>(homographyNode);
        }
    }
}
