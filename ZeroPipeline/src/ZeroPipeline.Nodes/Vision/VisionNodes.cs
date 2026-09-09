using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Filters;
using ZeroGraphics.Vision.Matching;
using ZeroGraphics.Vision.Metrology;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;

namespace ZeroPipeline.Nodes.Vision
{
    /// <summary>
    /// Pipeline source node that acquires or generates industrial ImageBuffer frames.
    /// Can simulate camera acquisition loops or stream from memory.
    /// </summary>
    public sealed class ImageSourceNode : SourceNode<ImageBuffer>
    {
        private readonly Func<long, ImageBuffer>? _frameGenerator;
        private readonly IEnumerator<ImageBuffer>? _frameEnumerator;
        private long _sequenceNumber;

        public ImageSourceNode(Func<long, ImageBuffer> frameGenerator, string? name = null)
            : base(name ?? "ImageSource")
        {
            _frameGenerator = frameGenerator ?? throw new ArgumentNullException(nameof(frameGenerator));
        }

        public ImageSourceNode(IEnumerable<ImageBuffer> frames, string? name = null)
            : base(name ?? "ImageSource")
        {
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            _frameEnumerator = frames.GetEnumerator();
        }

        protected override Task OnExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            ImageBuffer? frame = null;

            if (_frameGenerator != null)
            {
                frame = _frameGenerator(_sequenceNumber);
            }
            else if (_frameEnumerator != null)
            {
                if (_frameEnumerator.MoveNext())
                {
                    frame = _frameEnumerator.Current;
                }
                else
                {
                    Output.EmitEndOfStream(_sequenceNumber);
                    return Task.CompletedTask;
                }
            }

            if (frame != null)
            {
                Output.Emit(frame, _sequenceNumber++);
            }

            return Task.CompletedTask;
        }

        protected override Task OnResetAsync()
        {
            _sequenceNumber = 0;
            _frameEnumerator?.Reset();
            return base.OnResetAsync();
        }
    }

    /// <summary>
    /// Transforms an arbitrary format image buffer into an 8-bit grayscale ImageBuffer for vision processing.
    /// </summary>
    public sealed class ImageGrayscaleNode : TransformNode<ImageBuffer, ImageBuffer>
    {
        public ImageGrayscaleNode(string? name = null)
            : base(name ?? "GrayscaleTransform")
        {
        }

        protected override Task<ImageBuffer> ProcessAsync(ImageBuffer input, PipelineContext context, CancellationToken cancellationToken)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            if (input.Format == ImageFormatMode.Gray8)
            {
                return Task.FromResult(input);
            }

            var gray = ImageBuffer.CreateGray8(input.Width, input.Height);
            ColorTransform.ToGrayscale(input, gray);
            return Task.FromResult(gray);
        }
    }

    /// <summary>
    /// Pipeline node performing sub-pixel Normalized Cross Correlation (NCC) template matching on input images.
    /// </summary>
    public sealed class NccTemplateMatchingNode : TransformNode<ImageBuffer, TemplateMatchResult>
    {
        public ImageBuffer TemplateImage { get; set; }
        public double MinScore { get; set; }
        public bool SubPixelRefinement { get; set; }

        public NccTemplateMatchingNode(
            ImageBuffer templateImage,
            double minScore = 0.75,
            bool subPixelRefinement = true,
            string? name = null)
            : base(name ?? "NccTemplateMatcher")
        {
            TemplateImage = templateImage ?? throw new ArgumentNullException(nameof(templateImage));
            MinScore = minScore;
            SubPixelRefinement = subPixelRefinement;
        }

        protected override Task<TemplateMatchResult> ProcessAsync(ImageBuffer input, PipelineContext context, CancellationToken cancellationToken)
        {
            var result = NccTemplateMatcher.Match(input, TemplateImage, MinScore, SubPixelRefinement);
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Pipeline node performing 1D edge caliper metrology along a line profile to detect sub-pixel boundary transitions.
    /// </summary>
    public sealed class EdgeCaliperNode : TransformNode<ImageBuffer, List<CaliperEdge>>
    {
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
        public double MinMagnitude { get; set; }
        public EdgePolarity Polarity { get; set; }
        public double SampleStep { get; set; }

        public EdgeCaliperNode(
            double x1, double y1,
            double x2, double y2,
            double minMagnitude = 15.0,
            EdgePolarity polarity = EdgePolarity.Any,
            double sampleStep = 0.5,
            string? name = null)
            : base(name ?? "EdgeCaliper1D")
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            MinMagnitude = minMagnitude;
            Polarity = polarity;
            SampleStep = sampleStep;
        }

        protected override Task<List<CaliperEdge>> ProcessAsync(ImageBuffer input, PipelineContext context, CancellationToken cancellationToken)
        {
            var edges = EdgeCaliper1D.FindEdges(input, X1, Y1, X2, Y2, MinMagnitude, Polarity, SampleStep);
            return Task.FromResult(edges);
        }
    }
}
