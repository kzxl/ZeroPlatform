using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ZeroGraphics.Imaging.Core;
using ZeroGraphics.Vision.Codes;
using ZeroGraphics.Vision.Metrology;
using ZeroPipeline.Core.Execution;
using ZeroPipeline.Core.Nodes;
using ZeroPipeline.Core.Ports;

namespace ZeroPipeline.Nodes.Inspection
{
    /// <summary>
    /// Pipeline node that scans and decodes 1D industrial barcodes (Code 128, Code 39) from input images.
    /// </summary>
    public sealed class BarcodeReaderNode : TransformNode<ImageBuffer, BarcodeResult?>
    {
        public BarcodeSymbology Symbology { get; set; }
        public double ScanlineStepFraction { get; set; }

        public BarcodeReaderNode(
            BarcodeSymbology symbology = BarcodeSymbology.Unknown,
            double scanlineStepFraction = 0.15,
            string? name = null)
            : base(name ?? "BarcodeReader1D")
        {
            Symbology = symbology;
            ScanlineStepFraction = scanlineStepFraction;
        }

        protected override Task<BarcodeResult?> ProcessAsync(ImageBuffer input, PipelineContext context, CancellationToken cancellationToken)
        {
            var result = BarcodeReader1D.Decode(input, Symbology, ScanlineStepFraction);
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Pipeline node that judges dimensional measurement (caliper distance) against defined tolerance limits.
    /// </summary>
    public sealed class DimensionJudgeNode : TransformNode<List<CaliperEdge>, InspectionResult>
    {
        public string InspectionName { get; set; }
        public double NominalValue { get; set; }
        public double MinTolerance { get; set; }
        public double MaxTolerance { get; set; }
        public string Unit { get; set; }

        public DimensionJudgeNode(
            string inspectionName,
            double nominalValue,
            double minTolerance,
            double maxTolerance,
            string unit = "mm",
            string? name = null)
            : base(name ?? $"Judge<{inspectionName}>")
        {
            InspectionName = inspectionName ?? throw new ArgumentNullException(nameof(inspectionName));
            NominalValue = nominalValue;
            MinTolerance = minTolerance;
            MaxTolerance = maxTolerance;
            Unit = unit ?? "mm";
        }

        protected override Task<InspectionResult> ProcessAsync(List<CaliperEdge> edges, PipelineContext context, CancellationToken cancellationToken)
        {
            if (edges == null || edges.Count < 2)
            {
                return Task.FromResult(InspectionResult.Fail(
                    InspectionName,
                    0.0,
                    MinTolerance,
                    MaxTolerance,
                    Unit,
                    reason: $"Insufficient edges found (count={edges?.Count ?? 0})"));
            }

            // Calculate Euclidean distance between first and last edge
            var e1 = edges[0];
            var e2 = edges[edges.Count - 1];
            double dx = e2.X - e1.X;
            double dy = e2.Y - e1.Y;
            double distance = Math.Sqrt(dx * dx + dy * dy);

            bool passed = distance >= MinTolerance && distance <= MaxTolerance;

            var result = passed
                ? InspectionResult.Pass(InspectionName, distance, MinTolerance, MaxTolerance, Unit)
                : InspectionResult.Fail(InspectionName, distance, MinTolerance, MaxTolerance, Unit);

            return Task.FromResult(result);
        }
    }
}
