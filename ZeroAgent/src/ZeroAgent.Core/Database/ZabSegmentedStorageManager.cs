using System;
using System.Collections.Generic;
using System.IO;

namespace ZeroAgent.Core.Database
{
    /// <summary>
    /// Segmented Storage Manager for billion-scale databases.
    /// Partitions monolithic .zab databases into manageable, bounded chunks (e.g. 2GB max per segment),
    /// eliminating disk exhaustion during compaction and bypassing single-file OS allocation limits.
    /// </summary>
    public sealed class ZabSegmentedStorageManager
    {
        private readonly string _baseDirectory;
        private readonly string _databaseName;
        private readonly long _maxSegmentSizeBytes;

        public string BaseDirectory => _baseDirectory;
        public string DatabaseName => _databaseName;
        public long MaxSegmentSizeBytes => _maxSegmentSizeBytes;

        public ZabSegmentedStorageManager(string baseDirectory, string databaseName, long maxSegmentSizeBytes = 2L * 1024 * 1024 * 1024)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory)) throw new ArgumentNullException(nameof(baseDirectory));
            if (string.IsNullOrWhiteSpace(databaseName)) throw new ArgumentNullException(nameof(databaseName));

            _baseDirectory = baseDirectory;
            _databaseName = databaseName;
            _maxSegmentSizeBytes = Math.Max(16 * 1024 * 1024, maxSegmentSizeBytes); // Minimum 16MB

            if (!Directory.Exists(_baseDirectory))
            {
                Directory.CreateDirectory(_baseDirectory);
            }
        }

        /// <summary>
        /// Generates the deterministic path for a specific segment index.
        /// e.g. "C:/data/agent_0001.zab"
        /// </summary>
        public string GetSegmentFilePath(int segmentIndex)
        {
            return Path.Combine(_baseDirectory, $"{_databaseName}_{segmentIndex:D4}.zab");
        }

        /// <summary>
        /// Scans the directory and returns all active segment files in order.
        /// </summary>
        public List<string> DiscoverExistingSegments()
        {
            var segments = new List<string>();
            int index = 0;
            while (true)
            {
                string path = GetSegmentFilePath(index);
                if (File.Exists(path))
                {
                    segments.Add(path);
                    index++;
                }
                else
                {
                    break;
                }
            }
            return segments;
        }

        /// <summary>
        /// Determines whether the active segment has crossed the physical size threshold and requires rolling.
        /// </summary>
        public bool ShouldRollSegment(string currentSegmentPath)
        {
            if (!File.Exists(currentSegmentPath)) return false;
            var fileInfo = new FileInfo(currentSegmentPath);
            return fileInfo.Length >= _maxSegmentSizeBytes;
        }
    }
}
