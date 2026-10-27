using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.ColorScience;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Imaging.Filters;
using ZeroGraphics.Vision.Matching;
using ZeroGraphics.Vision.Metrology;
using ZeroGeometry.Core.Projective;
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

    /// <summary>
    /// Pipeline node performing standardized color space conversion (sRGB, Adobe RGB, Display P3, Rec.2020, ProPhoto RGB)
    /// using pure mathematical 3x3 Bradford-adapted chromatic matrices from ZeroGraphics.Imaging.ColorScience.
    /// </summary>
    public sealed unsafe class ColorSpaceConvertNode : TransformNode<ImageBuffer, ImageBuffer>
    {
        public ColorSpaces.Space SourceSpace { get; set; }
        public ColorSpaces.Space TargetSpace { get; set; }

        public ColorSpaceConvertNode() : this(ColorSpaces.Space.Srgb, ColorSpaces.Space.DisplayP3, null) { }

        public ColorSpaceConvertNode(
            ColorSpaces.Space sourceSpace = ColorSpaces.Space.Srgb,
            ColorSpaces.Space targetSpace = ColorSpaces.Space.DisplayP3,
            string? name = null)
            : base(name ?? "ColorSpaceConvert")
        {
            SourceSpace = sourceSpace;
            TargetSpace = targetSpace;
        }

        protected override Task<ImageBuffer> ProcessAsync(ImageBuffer input, PipelineContext context, CancellationToken cancellationToken)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (SourceSpace == TargetSpace || input.Format != ImageFormatMode.Bgra32)
            {
                return Task.FromResult(input);
            }

            float[] m = ColorSpaces.ConversionMatrix(SourceSpace, TargetSpace);
            float m00 = m[0], m01 = m[1], m02 = m[2];
            float m10 = m[3], m11 = m[4], m12 = m[5];
            float m20 = m[6], m21 = m[7], m22 = m[8];

            int width = input.Width;
            int height = input.Height;
            var output = ImageBuffer.CreateBgra32(width, height);

            byte* srcRow = input.Scan0;
            byte* dstRow = output.Scan0;
            int srcStride = input.Stride;
            int dstStride = output.Stride;

            const float inv255 = 1.0f / 255.0f;

            for (int y = 0; y < height; y++)
            {
                byte* src = srcRow + y * srcStride;
                byte* dst = dstRow + y * dstStride;

                for (int x = 0; x < width; x++)
                {
                    float b = src[0] * inv255;
                    float g = src[1] * inv255;
                    float r = src[2] * inv255;
                    byte a = src[3];

                    float rOut = m00 * r + m01 * g + m02 * b;
                    float gOut = m10 * r + m11 * g + m12 * b;
                    float bOut = m20 * r + m21 * g + m22 * b;

                    dst[0] = (byte)(bOut <= 0f ? 0 : (bOut >= 1f ? 255 : (byte)(bOut * 255f + 0.5f)));
                    dst[1] = (byte)(gOut <= 0f ? 0 : (gOut >= 1f ? 255 : (byte)(gOut * 255f + 0.5f)));
                    dst[2] = (byte)(rOut <= 0f ? 0 : (rOut >= 1f ? 255 : (byte)(rOut * 255f + 0.5f)));
                    dst[3] = a;

                    src += 4;
                    dst += 4;
                }
            }

            return Task.FromResult(output);
        }
    }

    /// <summary>
    /// Pipeline node performing 2D planar homography rectification and perspective keystone warping
    /// using Hartley-normalized Direct Linear Transformation (DLT) from ZeroGeometry.Core.Projective.
    /// </summary>
    public sealed unsafe class HomographyRectifyNode : TransformNode<ImageBuffer, ImageBuffer>
    {
        public Homography2D.Point2D[]? SourceQuad { get; set; }
        public int TargetWidth { get; set; }
        public int TargetHeight { get; set; }
        public double[]? HomographyMatrix { get; set; }

        public HomographyRectifyNode() : this(640, 480, null, null) { }

        public HomographyRectifyNode(
            int targetWidth,
            int targetHeight,
            Homography2D.Point2D[]? sourceQuad = null,
            string? name = null)
            : base(name ?? "HomographyRectify")
        {
            if (targetWidth <= 0) throw new ArgumentOutOfRangeException(nameof(targetWidth));
            if (targetHeight <= 0) throw new ArgumentOutOfRangeException(nameof(targetHeight));
            TargetWidth = targetWidth;
            TargetHeight = targetHeight;
            SourceQuad = sourceQuad;
        }

        protected override Task<ImageBuffer> ProcessAsync(ImageBuffer input, PipelineContext context, CancellationToken cancellationToken)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));

            double[]? h = HomographyMatrix;
            if (h == null && SourceQuad != null && SourceQuad.Length >= 4)
            {
                var dstQuad = new[]
                {
                    new Homography2D.Point2D(0, 0),
                    new Homography2D.Point2D(TargetWidth - 1, 0),
                    new Homography2D.Point2D(TargetWidth - 1, TargetHeight - 1),
                    new Homography2D.Point2D(0, TargetHeight - 1)
                };

                // Map target rectangle to source quad (backward mapping for inverse warping)
                h = Homography2D.EstimateDlt(dstQuad, SourceQuad);
            }

            if (h == null)
            {
                return Task.FromResult(input);
            }

            var output = input.Format == ImageFormatMode.Bgra32
                ? ImageBuffer.CreateBgra32(TargetWidth, TargetHeight)
                : ImageBuffer.CreateGray8(TargetWidth, TargetHeight);

            int srcW = input.Width;
            int srcH = input.Height;
            int srcStride = input.Stride;
            byte* srcScan0 = input.Scan0;

            int dstW = TargetWidth;
            int dstH = TargetHeight;
            int dstStride = output.Stride;
            byte* dstScan0 = output.Scan0;
            bool isBgra = input.Format == ImageFormatMode.Bgra32;

            double h00 = h[0], h01 = h[1], h02 = h[2];
            double h10 = h[3], h11 = h[4], h12 = h[5];
            double h20 = h[6], h21 = h[7], h22 = h[8];

            for (int y = 0; y < dstH; y++)
            {
                byte* dstRow = dstScan0 + y * dstStride;

                for (int x = 0; x < dstW; x++)
                {
                    double w = h20 * x + h21 * y + h22;
                    if (Math.Abs(w) < 1e-12) w = 1e-12;
                    double sx = (h00 * x + h01 * y + h02) / w;
                    double sy = (h10 * x + h11 * y + h12) / w;

                    int isx = (int)Math.Round(sx);
                    int isy = (int)Math.Round(sy);

                    if (isx >= 0 && isx < srcW && isy >= 0 && isy < srcH)
                    {
                        byte* pSrc = srcScan0 + isy * srcStride + (isBgra ? isx * 4 : isx);
                        if (isBgra)
                        {
                            byte* pDst = dstRow + x * 4;
                            pDst[0] = pSrc[0];
                            pDst[1] = pSrc[1];
                            pDst[2] = pSrc[2];
                            pDst[3] = pSrc[3];
                        }
                        else
                        {
                            dstRow[x] = *pSrc;
                        }
                    }
                }
            }

            return Task.FromResult(output);
        }
    }
}
