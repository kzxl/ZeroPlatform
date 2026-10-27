using System;

namespace ZeroPipeline.Nodes.Inspection
{
    /// <summary>
    /// Status indicator for an inspection evaluation cycle.
    /// </summary>
    public enum InspectionStatus
    {
        Passed = 0,
        Failed = 1,
        Warning = 2,
        Skipped = 3
    }

    /// <summary>
    /// Comprehensive record of an industrial inspection evaluation.
    /// Carries traceability metadata, tolerance bounds, measurement results, and pass/fail verdict.
    /// </summary>
    public sealed class InspectionResult
    {
        public string PartId { get; }
        public string LotNumber { get; }
        public string InspectionName { get; }
        public InspectionStatus Status { get; }
        public bool IsPassed => Status == InspectionStatus.Passed;
        public double MeasuredValue { get; }
        public double MinTolerance { get; }
        public double MaxTolerance { get; }
        public string Unit { get; }
        public string Message { get; }
        public long TimestampTicks { get; }

        public InspectionResult(
            string inspectionName,
            InspectionStatus status,
            double measuredValue,
            double minTolerance,
            double maxTolerance,
            string unit = "mm",
            string? partId = null,
            string? lotNumber = null,
            string? message = null,
            long? timestampTicks = null)
        {
            InspectionName = inspectionName ?? throw new ArgumentNullException(nameof(inspectionName));
            Status = status;
            MeasuredValue = measuredValue;
            MinTolerance = minTolerance;
            MaxTolerance = maxTolerance;
            Unit = unit ?? "mm";
            PartId = partId ?? "UNKNOWN";
            LotNumber = lotNumber ?? "LOT-000";
            Message = message ?? (status == InspectionStatus.Passed ? "OK" : "NG");
            TimestampTicks = timestampTicks ?? DateTime.UtcNow.Ticks;
        }

        public static InspectionResult Pass(
            string inspectionName,
            double measuredValue,
            double minTolerance,
            double maxTolerance,
            string unit = "mm",
            string? partId = null,
            string? lotNumber = null)
        {
            return new InspectionResult(
                inspectionName,
                InspectionStatus.Passed,
                measuredValue,
                minTolerance,
                maxTolerance,
                unit,
                partId,
                lotNumber,
                "Passed tolerance criteria");
        }

        public static InspectionResult Fail(
            string inspectionName,
            double measuredValue,
            double minTolerance,
            double maxTolerance,
            string unit = "mm",
            string? partId = null,
            string? lotNumber = null,
            string? reason = null)
        {
            return new InspectionResult(
                inspectionName,
                InspectionStatus.Failed,
                measuredValue,
                minTolerance,
                maxTolerance,
                unit,
                partId,
                lotNumber,
                reason ?? "Out of tolerance bounds");
        }

        public override string ToString() =>
            $"[{Status}] {InspectionName} (Part: {PartId}, Lot: {LotNumber}): {MeasuredValue:F3}{Unit} [{MinTolerance:F3}..{MaxTolerance:F3}]";
    }
}
