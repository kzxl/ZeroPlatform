using System;
using System.Collections.Generic;
using ZeroAgent.Core.Database;

namespace ZeroAgent.Core.Reasoning.Cognitive
{
    /// <summary>
    /// Privacy-Preserving Federated Weight Aggregator (FedAvg) for Distributed Agent Swarms.
    /// Aggregates decentralized System 1 INT8 reflex weights from multiple edge agents
    /// into a unified global policy network without transmitting raw user dialogue or proprietary data.
    /// </summary>
    public static class ZabFederatedAveraging
    {
        /// <summary>
        /// Combines multiple agent neural policies into a single synchronized swarm policy.
        /// </summary>
        public static ZabNeuralPolicy Aggregate(
            IReadOnlyList<ZabNeuralPolicy> policies,
            string aggregatedModelName = "SwarmGlobalPolicy")
        {
            if (policies == null || policies.Count == 0)
                throw new ArgumentException("Policies collection cannot be null or empty.", nameof(policies));

            if (policies.Count == 1)
            {
                return policies[0];
            }

            var baseline = policies[0];
            int inputDim = baseline.InputDim;
            int classCount = baseline.OutputClasses.Count;
            int totalWeights = inputDim * classCount;

            // Validate homogeneous architecture across all agents
            for (int p = 1; p < policies.Count; p++)
            {
                var pol = policies[p];
                if (pol.InputDim != inputDim || pol.OutputClasses.Count != classCount)
                {
                    throw new InvalidOperationException($"Policy at index {p} has mismatched architecture. Expected Dim: {inputDim}, Classes: {classCount}.");
                }
            }

            sbyte[] aggregatedWeights = new sbyte[totalWeights];
            float[] aggregatedBiases = new float[classCount];
            float m = policies.Count;

            // 1. Average INT8 Weights with safe numerical bounds
            for (int w = 0; w < totalWeights; w++)
            {
                double sum = 0;
                for (int p = 0; p < policies.Count; p++)
                {
                    sum += policies[p].WeightsInt8[w];
                }
                int avg = (int)Math.Round(sum / m);
                aggregatedWeights[w] = (sbyte)Math.Max(-127, Math.Min(127, avg));
            }

            // 2. Average Floating-point Biases
            for (int c = 0; c < classCount; c++)
            {
                double bSum = 0;
                for (int p = 0; p < policies.Count; p++)
                {
                    if (c < policies[p].Biases.Length)
                    {
                        bSum += policies[p].Biases[c];
                    }
                }
                aggregatedBiases[c] = (float)(bSum / m);
            }

            // Calculate averaged scale
            float scaleSum = 0;
            for (int p = 0; p < policies.Count; p++) scaleSum += policies[p].Scale;
            float avgScale = scaleSum / m;

            var globalPolicy = new ZabNeuralPolicy
            {
                ModelName = aggregatedModelName,
                InputDim = inputDim,
                OutputClasses = new List<string>(baseline.OutputClasses),
                Scale = avgScale,
                WeightsInt8 = aggregatedWeights,
                Biases = aggregatedBiases
            };

            return globalPolicy;
        }

        /// <summary>
        /// Byzantine-Resilient Coordinate-Wise Trimmed Mean / Median Aggregation over INT8 reflex policies.
        /// Sorts each parameter coordinate across participating edge nodes, trims top and bottom extreme outliers,
        /// and averages only the benign majority so compromised Byzantine edge nodes cannot poison the swarm policy.
        /// </summary>
        public static ZabNeuralPolicy AggregateByzantineRobust(
            IReadOnlyList<ZabNeuralPolicy> policies,
            float trimFraction = 0.2f,
            string aggregatedModelName = "SwarmByzantineRobustPolicy")
        {
            if (policies == null || policies.Count == 0)
                throw new ArgumentException("Policies collection cannot be null or empty.", nameof(policies));

            if (trimFraction < 0.0f || trimFraction >= 0.5f)
                throw new ArgumentOutOfRangeException(nameof(trimFraction), "Trim fraction must be in [0.0, 0.5).");

            if (policies.Count == 1)
            {
                return policies[0];
            }

            var baseline = policies[0];
            int inputDim = baseline.InputDim;
            int classCount = baseline.OutputClasses.Count;
            int totalWeights = inputDim * classCount;
            int kNodes = policies.Count;

            var weightArrays = new sbyte[kNodes][];
            for (int p = 0; p < kNodes; p++)
            {
                var pol = policies[p];
                if (pol.InputDim != inputDim || pol.OutputClasses.Count != classCount || pol.WeightsInt8.Length < totalWeights)
                {
                    throw new InvalidOperationException(
                        $"Policy at index {p} has mismatched architecture. Expected Dim: {inputDim}, Classes: {classCount}.");
                }
                weightArrays[p] = pol.WeightsInt8;
            }

            sbyte[] robustWeights = AggregateByzantineRobust(weightArrays, trimFraction);

            int trimCount = ComputeTrimCount(kNodes, trimFraction);
            int retainedCount = kNodes - 2 * trimCount;

            float[] robustBiases = new float[classCount];
            float[] coordBuffer = new float[kNodes];

            for (int c = 0; c < classCount; c++)
            {
                for (int p = 0; p < kNodes; p++)
                {
                    coordBuffer[p] = c < policies[p].Biases.Length ? policies[p].Biases[c] : 0.0f;
                }
                Array.Sort(coordBuffer);

                double bSum = 0.0;
                for (int i = trimCount; i < kNodes - trimCount; i++)
                {
                    bSum += coordBuffer[i];
                }
                robustBiases[c] = (float)(bSum / retainedCount);
            }

            for (int p = 0; p < kNodes; p++)
            {
                coordBuffer[p] = policies[p].Scale;
            }
            Array.Sort(coordBuffer);

            double sSum = 0.0;
            for (int i = trimCount; i < kNodes - trimCount; i++)
            {
                sSum += coordBuffer[i];
            }
            float robustScale = (float)(sSum / retainedCount);

            return new ZabNeuralPolicy
            {
                ModelName = aggregatedModelName,
                InputDim = inputDim,
                OutputClasses = new List<string>(baseline.OutputClasses),
                Scale = robustScale,
                WeightsInt8 = robustWeights,
                Biases = robustBiases,
                AnchorWeights = (sbyte[])robustWeights.Clone()
            };
        }

        /// <summary>
        /// Byzantine-Resilient Coordinate-Wise Trimmed Mean / Median directly over raw INT8 weight vectors.
        /// </summary>
        public static sbyte[] AggregateByzantineRobust(
            IReadOnlyList<sbyte[]> weightVectors,
            float trimFraction = 0.2f)
        {
            if (weightVectors == null || weightVectors.Count == 0)
                throw new ArgumentException("Weight vectors collection cannot be null or empty.", nameof(weightVectors));

            if (trimFraction < 0.0f || trimFraction >= 0.5f)
                throw new ArgumentOutOfRangeException(nameof(trimFraction), "Trim fraction must be in [0.0, 0.5).");

            int kNodes = weightVectors.Count;
            int totalWeights = weightVectors[0].Length;

            if (kNodes == 1)
            {
                return (sbyte[])weightVectors[0].Clone();
            }

            for (int p = 1; p < kNodes; p++)
            {
                if (weightVectors[p] == null || weightVectors[p].Length != totalWeights)
                {
                    throw new InvalidOperationException($"Weight vector at index {p} has mismatched length.");
                }
            }

            int trimCount = ComputeTrimCount(kNodes, trimFraction);
            int retainedCount = kNodes - 2 * trimCount;

            sbyte[] aggregated = new sbyte[totalWeights];
            sbyte[] slice = new sbyte[kNodes];

            for (int w = 0; w < totalWeights; w++)
            {
                for (int p = 0; p < kNodes; p++)
                {
                    slice[p] = weightVectors[p][w];
                }

                Array.Sort(slice);

                int sum = 0;
                for (int i = trimCount; i < kNodes - trimCount; i++)
                {
                    sum += slice[i];
                }

                int avg = (int)Math.Round((double)sum / retainedCount);
                aggregated[w] = (sbyte)Math.Max(-127, Math.Min(127, avg));
            }

            return aggregated;
        }

        private static int ComputeTrimCount(int kNodes, float trimFraction)
        {
            if (trimFraction <= 0.0f || kNodes < 3)
                return 0;

            int trimCount = (int)Math.Floor(kNodes * trimFraction);
            if (trimCount == 0 && kNodes >= 3)
            {
                // Ensure at least 1 extreme on each side is trimmed (coordinate-wise median for K=3 or K=4)
                trimCount = 1;
            }

            int maxTrim = (kNodes - 1) / 2;
            return Math.Min(trimCount, maxTrim);
        }
    }
}
