using System;
using System.Collections.Generic;
using System.Linq;
using ZeroGraphics.Vision.Matching;
using ZeroGraphics.Vision.Quality;

namespace ZeroGraphics.Vision.Curation
{
    /// <summary>
    /// Represents an individual photo candidate with perceptual hash and quality metrics for clustering.
    /// </summary>
    public class CurationCandidate
    {
        /// <summary>
        /// Unique identifier or file path.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 64-bit difference hash computed via <see cref="DifferenceHash"/>.
        /// </summary>
        public ulong Hash { get; set; }

        /// <summary>
        /// Effective sharpness from <see cref="ImageSharpnessEvaluator"/>.
        /// </summary>
        public double EffectiveSharpness { get; set; }

        /// <summary>
        /// Exposure score from <see cref="ImageExposureEvaluator"/>.
        /// </summary>
        public double ExposureScore { get; set; }

        /// <summary>
        /// Optional capture timestamp for time-windowed burst grouping.
        /// </summary>
        public DateTime? CaptureTime { get; set; }

        /// <summary>
        /// User-defined tag or payload (e.g. metadata reference).
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// Overall rank score within a burst group: Sharpness * 0.7 + Exposure * 0.3.
        /// </summary>
        public double RankScore => Math.Round(EffectiveSharpness * 0.70 + ExposureScore * 0.30, 2);
    }

    /// <summary>
    /// Represents a cluster of duplicate or burst shots with an elected best shot.
    /// </summary>
    public class BurstCluster
    {
        /// <summary>
        /// Numeric cluster index.
        /// </summary>
        public int ClusterId { get; set; }

        /// <summary>
        /// All photos belonging to this burst or duplicate cluster.
        /// </summary>
        public List<CurationCandidate> Members { get; set; } = new List<CurationCandidate>();

        /// <summary>
        /// The highest quality photo elected as best shot.
        /// </summary>
        public CurationCandidate? BestShot { get; set; }

        /// <summary>
        /// Redundant photos suggested for rejection/removal.
        /// </summary>
        public List<CurationCandidate> RedundantShots { get; set; } = new List<CurationCandidate>();
    }

    /// <summary>
    /// Clustering options for burst and duplicate detection.
    /// </summary>
    public class BurstClusterOptions
    {
        /// <summary>
        /// Maximum Hamming distance for burst/duplicate grouping. Default 6 (out of 64).
        /// </summary>
        public int MaxHammingDistance { get; set; } = 6;

        /// <summary>
        /// Optional maximum allowable time difference in seconds for burst grouping.
        /// If null, timestamps are not constrained. Default 5.0 seconds.
        /// </summary>
        public double? MaxTimeDeltaSeconds { get; set; } = 5.0;

        /// <summary>
        /// If true, only clusters with 2 or more photos are returned. Default false.
        /// </summary>
        public bool OnlyMultiPhotoClusters { get; set; } = false;
    }

    /// <summary>
    /// Groups photos into burst series and near-duplicate sets, electing the sharpest, best-exposed shot.
    /// Uses Union-Find with path compression for optimal O(N) clustering performance.
    /// </summary>
    public static class BurstClusterer
    {
        /// <summary>
        /// Clusters candidate photos and elects the best shot in each group.
        /// </summary>
        /// <param name="candidates">List of photo candidates with hashes and scores.</param>
        /// <param name="options">Clustering options.</param>
        /// <returns>List of <see cref="BurstCluster"/>.</returns>
        public static List<BurstCluster> Cluster(IReadOnlyList<CurationCandidate> candidates, BurstClusterOptions? options = null)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (candidates.Count == 0) return new List<BurstCluster>();

            options ??= new BurstClusterOptions();
            int n = candidates.Count;

            // Disjoint-set union (Union-Find)
            int[] parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;

            int Find(int i)
            {
                int root = i;
                while (root != parent[root])
                    root = parent[root];
                // Path compression
                int curr = i;
                while (curr != root)
                {
                    int next = parent[curr];
                    parent[curr] = root;
                    curr = next;
                }
                return root;
            }

            void Union(int i, int j)
            {
                int rootI = Find(i);
                int rootJ = Find(j);
                if (rootI != rootJ)
                {
                    parent[rootJ] = rootI;
                }
            }

            // Pairwise comparison (pruned by time window if available)
            for (int i = 0; i < n; i++)
            {
                var a = candidates[i];
                for (int j = i + 1; j < n; j++)
                {
                    var b = candidates[j];

                    if (options.MaxTimeDeltaSeconds.HasValue && a.CaptureTime.HasValue && b.CaptureTime.HasValue)
                    {
                        double dt = Math.Abs((a.CaptureTime.Value - b.CaptureTime.Value).TotalSeconds);
                        if (dt > options.MaxTimeDeltaSeconds.Value)
                        {
                            continue;
                        }
                    }

                    int dist = DifferenceHash.HammingDistance(a.Hash, b.Hash);
                    if (dist <= options.MaxHammingDistance)
                    {
                        Union(i, j);
                    }
                }
            }

            // Group candidates by disjoint-set root
            var groups = new Dictionary<int, List<CurationCandidate>>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(i);
                if (!groups.TryGetValue(root, out var list))
                {
                    list = new List<CurationCandidate>();
                    groups[root] = list;
                }
                list.Add(candidates[i]);
            }

            var results = new List<BurstCluster>();
            int clusterIndex = 1;

            foreach (var kvp in groups.OrderByDescending(g => g.Value.Count))
            {
                var members = kvp.Value;
                if (options.OnlyMultiPhotoClusters && members.Count < 2)
                    continue;

                // Sort members by RankScore descending
                members.Sort((x, y) => y.RankScore.CompareTo(x.RankScore));

                var cluster = new BurstCluster
                {
                    ClusterId = clusterIndex++,
                    Members = members,
                    BestShot = members[0],
                    RedundantShots = members.Skip(1).ToList()
                };

                results.Add(cluster);
            }

            return results;
        }
    }
}
