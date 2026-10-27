using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroAgent.Core.Tools;
using ZeroPrompt.Core.Grammar;

namespace ZeroAgent.Tools.Storage
{
    /// <summary>
    /// Telemetry Time-Series Database (TSDB) querying tool for AI diagnostic agents.
    /// Provides statistical aggregation, trend analysis, and Z-score anomaly detection.
    /// </summary>
    public static partial class TsdbQueryTool
    {
        private static readonly ConcurrentDictionary<string, List<TsdbDataPoint>> _seriesStore = new ConcurrentDictionary<string, List<TsdbDataPoint>>(StringComparer.OrdinalIgnoreCase);

        static TsdbQueryTool()
        {
            // Seed sample time-series data for simulated factory telemetry
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var rnd = new Random(1337);

            var tempPoints = new List<TsdbDataPoint>(100);
            for (int i = 99; i >= 0; i--)
            {
                long ts = now - (i * 60_000); // 1 minute intervals
                double baseTemp = 70.0 + (rnd.NextDouble() * 4.0 - 2.0);
                if (i == 5) baseTemp += 25.0; // Anomaly spike
                tempPoints.Add(new TsdbDataPoint(ts, baseTemp));
            }
            _seriesStore["motor_temperature"] = tempPoints;

            var vibPoints = new List<TsdbDataPoint>(100);
            for (int i = 99; i >= 0; i--)
            {
                long ts = now - (i * 60_000);
                double val = 1.2 + (rnd.NextDouble() * 0.3 - 0.15);
                vibPoints.Add(new TsdbDataPoint(ts, val));
            }
            _seriesStore["bearing_vibration"] = vibPoints;
        }

        public static void RegisterAll(AgentToolRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var querySchema = new JsonSchemaConstraint("QueryTsdbMetric")
                .AddProperty("metricName", SchemaPropertyType.String, required: true)
                .AddProperty("durationMinutes", SchemaPropertyType.Number, required: false)
                .AddProperty("aggregation", SchemaPropertyType.String, required: false);

            registry.Register(new AgentTool(
                "query_tsdb_metric",
                "Queries statistical telemetry metrics (avg, min, max, count, latest) for a time series.",
                "metricName: string, durationMinutes: int, aggregation: string",
                ExecuteQueryMetricAsync,
                schema: querySchema,
                requiresApproval: false
            ));

            var anomalySchema = new JsonSchemaConstraint("DetectTsdbAnomalies")
                .AddProperty("metricName", SchemaPropertyType.String, required: true)
                .AddProperty("sigmaThreshold", SchemaPropertyType.Number, required: false);

            registry.Register(new AgentTool(
                "detect_tsdb_anomalies",
                "Detects statistical anomalies and outliers where sensor readings exceed rolling mean +/- k*sigma.",
                "metricName: string, sigmaThreshold: number",
                ExecuteDetectAnomaliesAsync,
                schema: anomalySchema,
                requiresApproval: false
            ));
        }

        private static Task<string> ExecuteQueryMetricAsync(string arg)
        {
            try
            {
                string metricName = "motor_temperature";
                int durationMinutes = 60;
                string aggregation = "avg";

                if (arg.TrimStart().StartsWith("{"))
                {
                    using (var doc = JsonDocument.Parse(arg))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("metricName", out var m)) metricName = m.GetString() ?? metricName;
                        if (root.TryGetProperty("durationMinutes", out var d)) durationMinutes = d.GetInt32();
                        if (root.TryGetProperty("aggregation", out var a)) aggregation = a.GetString() ?? aggregation;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(arg))
                {
                    metricName = arg.Trim();
                }

                if (!_seriesStore.TryGetValue(metricName, out var points) || points.Count == 0)
                {
                    return Task.FromResult($"Metric '{metricName}' not found or has no recorded data points.");
                }

                double sum = 0;
                double min = double.MaxValue;
                double max = double.MinValue;
                int count = 0;

                lock (points)
                {
                    count = Math.Min(points.Count, durationMinutes);
                    int startIndex = points.Count - count;
                    for (int i = startIndex; i < points.Count; i++)
                    {
                        double v = points[i].Value;
                        sum += v;
                        if (v < min) min = v;
                        if (v > max) max = v;
                    }
                }

                double avg = count > 0 ? sum / count : 0.0;
                double latest = points[points.Count - 1].Value;

                var result = new
                {
                    metric = metricName,
                    windowMinutes = count,
                    count = count,
                    avg = Math.Round(avg, 2),
                    min = Math.Round(min, 2),
                    max = Math.Round(max, 2),
                    latest = Math.Round(latest, 2)
                };

                return Task.FromResult(JsonSerializer.Serialize(result));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Failed to query TSDB metric: {ex.Message}");
            }
        }
    }
}
