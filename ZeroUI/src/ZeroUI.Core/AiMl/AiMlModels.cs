using System;

namespace ZeroUI.Core.AiMl
{
    public class AnomalyPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public double AnomalyScore { get; set; }
    }

    public class FeatureScore
    {
        public string Name { get; set; } = string.Empty;
        public double Score { get; set; }
    }

    public class PredictionPoint
    {
        public DateTime Timestamp { get; set; }
        public double ActualValue { get; set; }
        public double PredictedValue { get; set; }
        public double UpperConfidence { get; set; }
        public double LowerConfidence { get; set; }
    }

    public class ConfusionMatrixData
    {
        public string[] Labels { get; set; } = Array.Empty<string>();
        public int[,] Matrix { get; set; } = new int[0, 0];
    }

    public class ModelMetric
    {
        public string MetricName { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class TimeValue
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
    }

    public class AnomalyRange
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public double Severity { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}