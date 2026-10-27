using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ZeroStorage.Core.Gorilla;
using ZeroStorage.Core.TimeSeries;

namespace ZeroStorage.Core.Persistence
{
    /// <summary>
    /// Background compactor that merges fragmented or smaller TimeSeriesBlocks into consolidated,
    /// highly-compressed archival blocks to optimize storage footprint and query performance.
    /// </summary>
    public static class TimeSeriesCompactor
    {
        /// <summary>
        /// Merges multiple smaller blocks for a given metric into a single dense, sorted TimeSeriesBlock.
        /// </summary>
        public static TimeSeriesBlock Compact(int metricId, IEnumerable<TimeSeriesBlock> blocks)
        {
            if (blocks == null) throw new ArgumentNullException(nameof(blocks));

            var allPoints = new List<TimeSeriesPoint>();
            foreach (var block in blocks)
            {
                if (block.MetricId == metricId && block.Count > 0)
                {
                    allPoints.AddRange(block.Decompress());
                }
            }

            if (allPoints.Count == 0)
                throw new InvalidOperationException($"No points found for metric {metricId} across provided blocks.");

            // Sort chronologically
            allPoints.Sort((a, b) => a.TimestampMs.CompareTo(b.TimestampMs));

            // Deduplicate matching timestamps if needed (taking latest value)
            var deduped = new List<TimeSeriesPoint>(allPoints.Count);
            for (int i = 0; i < allPoints.Count; i++)
            {
                if (deduped.Count > 0 && deduped[deduped.Count - 1].TimestampMs == allPoints[i].TimestampMs)
                {
                    deduped[deduped.Count - 1] = allPoints[i];
                }
                else
                {
                    deduped.Add(allPoints[i]);
                }
            }

            return TimeSeriesBlock.FromPoints(metricId, deduped);
        }

        /// <summary>
        /// Reads all blocks from a source MemoryMappedTimeSeriesLog, groups by metric ID,
        /// compacts each metric's blocks, and writes them into a new target log.
        /// </summary>
        public static int CompactLog(MemoryMappedTimeSeriesLog sourceLog, string targetFilePath)
        {
            if (sourceLog == null) throw new ArgumentNullException(nameof(sourceLog));
            if (string.IsNullOrEmpty(targetFilePath)) throw new ArgumentNullException(nameof(targetFilePath));

            if (File.Exists(targetFilePath))
                File.Delete(targetFilePath);

            var allBlocks = sourceLog.ReadAllBlocks();
            if (allBlocks.Count == 0) return 0;

            var metricGroups = new Dictionary<int, List<TimeSeriesBlock>>();
            foreach (var b in allBlocks)
            {
                if (!metricGroups.TryGetValue(b.MetricId, out var list))
                {
                    list = new List<TimeSeriesBlock>();
                    metricGroups[b.MetricId] = list;
                }
                list.Add(b);
            }

            int compactedCount = 0;
            using (var targetLog = new MemoryMappedTimeSeriesLog(targetFilePath))
            {
                foreach (var kvp in metricGroups)
                {
                    var compactedBlock = Compact(kvp.Key, kvp.Value);
                    targetLog.AppendBlock(compactedBlock);
                    compactedCount++;
                }
            }

            return compactedCount;
        }
    }
}
