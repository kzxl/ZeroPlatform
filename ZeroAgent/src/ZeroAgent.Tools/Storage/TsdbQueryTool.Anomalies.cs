using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZeroAgent.Tools.Storage
{
    public static partial class TsdbQueryTool
    {
        private static Task<string> ExecuteDetectAnomaliesAsync(string arg)
        {
            try
            {
                string metricName = "motor_temperature";
                double sigma = 3.0;

                if (arg.TrimStart().StartsWith("{"))
                {
                    using (var doc = JsonDocument.Parse(arg))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("metricName", out var m)) metricName = m.GetString() ?? metricName;
                        if (root.TryGetProperty("sigmaThreshold", out var s)) sigma = s.GetDouble();
                    }
                }
                else if (!string.IsNullOrWhiteSpace(arg))
                {
                    metricName = arg.Trim();
                }

                if (!_seriesStore.TryGetValue(metricName, out var points) || points.Count < 5)
                {
                    return Task.FromResult($"Insufficient data points to compute anomaly thresholds for '{metricName}'.");
                }

                double sum = 0;
                int count = points.Count;
                for (int i = 0; i < count; i++) sum += points[i].Value;
                double mean = sum / count;

                double varianceSum = 0;
                for (int i = 0; i < count; i++)
                {
                    double diff = points[i].Value - mean;
                    varianceSum += diff * diff;
                }
                double stdDev = Math.Sqrt(varianceSum / count);
                double upperLimit = mean + (sigma * stdDev);
                double lowerLimit = mean - (sigma * stdDev);

                var anomalies = new List<object>();
                for (int i = 0; i < count; i++)
                {
                    double v = points[i].Value;
                    if (v > upperLimit || v < lowerLimit)
                    {
                        anomalies.Add(new
                        {
                            timestampUnixMs = points[i].TimestampUnixMs,
                            value = Math.Round(v, 2),
                            zScore = Math.Round(Math.Abs(v - mean) / (stdDev > 1e-6 ? stdDev : 1.0), 2)
                        });
                    }
                }

                var report = new
                {
                    metric = metricName,
                    mean = Math.Round(mean, 2),
                    stdDev = Math.Round(stdDev, 2),
                    upperThreshold = Math.Round(upperLimit, 2),
                    lowerThreshold = Math.Round(lowerLimit, 2),
                    anomalyCount = anomalies.Count,
                    anomalies = anomalies
                };

                return Task.FromResult(JsonSerializer.Serialize(report));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Failed to detect TSDB anomalies: {ex.Message}");
            }
        }
    }
}
