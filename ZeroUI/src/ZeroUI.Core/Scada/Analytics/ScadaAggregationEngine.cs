using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ZeroUI.Core.Common;

namespace ZeroUI.Core.Scada.Analytics
{
    /// <summary>
    /// Type of sliding-window statistical calculation performed in Tier 2.
    /// </summary>
    public enum AggregationType
    {
        SimpleMovingAverage,
        ExponentialMovingAverage,
        Minimum,
        Maximum,
        PeakToPeak,
        RootMeanSquare,
        IntegralAccumulation
    }

    /// <summary>
    /// Configuration and rolling buffer state for an aggregated tag.
    /// </summary>
    public sealed class TagAggregator
    {
        public string SourceTagPath { get; }
        public int SourceTagId { get; internal set; }
        public string TargetTagPath { get; }
        public int TargetTagId { get; internal set; }
        public AggregationType AggregationType { get; }
        public int WindowSize { get; }
        public double Alpha { get; } // For EMA

        private readonly double[] _buffer;
        private int _head = 0;
        private int _count = 0;
        private double _runningSum = 0;
        private double _runningSumSquares = 0;
        private double _ema = 0;
        private double _totalIntegral = 0;
        private long _lastUpdateTick = -1;

        public TagAggregator(
            string sourceTagPath,
            string targetTagPath,
            AggregationType aggregationType,
            int windowSize = 100,
            double alpha = 0.1)
        {
            SourceTagPath = sourceTagPath ?? throw new ArgumentNullException(nameof(sourceTagPath));
            TargetTagPath = targetTagPath ?? throw new ArgumentNullException(nameof(targetTagPath));
            AggregationType = aggregationType;
            WindowSize = Math.Max(2, windowSize);
            Alpha = Math.Max(0.001, Math.Min(1.0, alpha));
            _buffer = new double[WindowSize];
        }

        public double Update(double sampleValue, long currentTick)
        {
            switch (AggregationType)
            {
                case AggregationType.SimpleMovingAverage:
                {
                    if (_count < WindowSize)
                    {
                        _buffer[_count] = sampleValue;
                        _runningSum += sampleValue;
                        _count++;
                    }
                    else
                    {
                        double oldVal = _buffer[_head];
                        _buffer[_head] = sampleValue;
                        _runningSum += sampleValue - oldVal;
                        _head = (_head + 1) % WindowSize;
                    }
                    return _count > 0 ? _runningSum / _count : sampleValue;
                }

                case AggregationType.ExponentialMovingAverage:
                {
                    if (_count == 0)
                    {
                        _ema = sampleValue;
                        _count = 1;
                    }
                    else
                    {
                        _ema = (Alpha * sampleValue) + ((1.0 - Alpha) * _ema);
                    }
                    return _ema;
                }

                case AggregationType.RootMeanSquare:
                {
                    double sq = sampleValue * sampleValue;
                    if (_count < WindowSize)
                    {
                        _buffer[_count] = sq;
                        _runningSumSquares += sq;
                        _count++;
                    }
                    else
                    {
                        double oldSq = _buffer[_head];
                        _buffer[_head] = sq;
                        _runningSumSquares += sq - oldSq;
                        _head = (_head + 1) % WindowSize;
                    }
                    double meanSq = _count > 0 ? _runningSumSquares / _count : sq;
                    return Math.Sqrt(Math.Max(0.0, meanSq));
                }

                case AggregationType.Minimum:
                case AggregationType.Maximum:
                case AggregationType.PeakToPeak:
                {
                    if (_count < WindowSize)
                    {
                        _buffer[_count++] = sampleValue;
                    }
                    else
                    {
                        _buffer[_head] = sampleValue;
                        _head = (_head + 1) % WindowSize;
                    }

                    double min = _buffer[0];
                    double max = _buffer[0];
                    for (int i = 1; i < _count; i++)
                    {
                        double v = _buffer[i];
                        if (v < min) min = v;
                        if (v > max) max = v;
                    }

                    if (AggregationType == AggregationType.Minimum) return min;
                    if (AggregationType == AggregationType.Maximum) return max;
                    return max - min;
                }

                case AggregationType.IntegralAccumulation:
                {
                    if (_lastUpdateTick >= 0)
                    {
                        double dtSeconds = Math.Max(0.0001, (currentTick - _lastUpdateTick) / 1000.0);
                        _totalIntegral += sampleValue * dtSeconds;
                    }
                    _lastUpdateTick = currentTick;
                    return _totalIntegral;
                }

                default:
                    return sampleValue;
            }
        }
    }

    /// <summary>
    /// Medium-tier (100 Hz - 1000 Hz) statistical and mathematical aggregation engine.
    /// Computes moving averages, RMS, min/max, and integrations from incoming telemetry.
    /// </summary>
    public sealed class ScadaAggregationEngine
    {
        private readonly List<TagAggregator> _aggregators = new List<TagAggregator>();
        private readonly object _lock = new object();

        /// <summary>
        /// Registers a sliding-window calculation to be computed in Tier 2.
        /// </summary>
        public void RegisterAggregator(TagAggregator aggregator)
        {
            if (aggregator == null) throw new ArgumentNullException(nameof(aggregator));

            lock (_lock)
            {
                aggregator.SourceTagId = ZeroTagEngine.GetOrRegisterTag(aggregator.SourceTagPath);
                aggregator.TargetTagId = ZeroTagEngine.GetOrRegisterTag(aggregator.TargetTagPath);
                _aggregators.Add(aggregator);
            }
        }

        /// <summary>
        /// Executes an aggregation pass across all registered aggregators.
        /// Called from the Medium Tier periodic loop (100 Hz - 1000 Hz).
        /// </summary>
        public int ExecuteAggregationCycle(long currentTick)
        {
            List<TagAggregator> list;
            lock (_lock)
            {
                if (_aggregators.Count == 0) return 0;
                list = new List<TagAggregator>(_aggregators);
            }

            int computedCount = 0;
            for (int i = 0; i < list.Count; i++)
            {
                var agg = list[i];
                var raw = ZeroTagEngine.Storage.GetValue(agg.SourceTagId);
                if (raw.Quality != ScadaQuality.Bad)
                {
                    double result = agg.Update(raw.AsDouble(), currentTick);
                    ZeroTagEngine.SetNumeric(agg.TargetTagId, result, raw.Quality);
                    computedCount++;
                }
            }

            return computedCount;
        }
    }
}
