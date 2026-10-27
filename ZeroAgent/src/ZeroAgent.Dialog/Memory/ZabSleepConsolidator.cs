using System;
using System.Diagnostics;
using ZeroAgent.Core.Database;
using ZeroAgent.Core.Reasoning.Cognitive;
using ZeroVector.Core.Quantization;

namespace ZeroAgent.Dialog.Memory
{
    /// <summary>
    /// Configuration options for the cognitive sleep consolidation loop.
    /// </summary>
    public sealed class SleepConsolidationOptions
    {
        public int MinAccessCountForReflex { get; set; } = 2;
        public float AdaptationLearningRate { get; set; } = 0.05f;
        public bool AutoCachePlans { get; set; } = true;
        public float PruneLowRetentionThreshold { get; set; } = 0.05f;
        public bool AutoCompactDatabase { get; set; } = true;

        // Ouroboros Causal-to-Reflex Consolidation
        public int MinCausalObservationsForPromotion { get; set; } = 2;
        public float MinCausalRewardThreshold { get; set; } = 0.80f;
        public float MinCausalFailureThreshold { get; set; } = -0.70f;
    }

    /// <summary>
    /// Summary report generated after the agent completes a sleep consolidation cycle.
    /// </summary>
    public sealed class SleepConsolidationReport
    {
        public int TotalEpisodesScanned { get; set; }
        public int TotalCausalEdgesScanned { get; set; }
        public int ReflexesSynthesized { get; set; }
        public int ReflexionsGenerated { get; set; }
        public int PlansCached { get; set; }
        public int CausalReflexesPromoted { get; set; }
        public int CausalFailuresReflected { get; set; }
        public TimeSpan Elapsed { get; set; }
        public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Offline / Idle Cognitive Sleep Consolidator modeled after human memory consolidation during sleep.
    /// Sweeps short-term episodic experiences (hippocampus tier) and verified causal transitions,
    /// distills them into instant System 1 reflexes (INT8 weights) and semantic plan trajectories,
    /// and prunes decayed memory according to Ebbinghaus forgetting dynamics.
    /// </summary>
    public static class ZabSleepConsolidator
    {
        /// <summary>
        /// Executes a full cognitive sleep consolidation cycle between the AgenticMemoryEngine and ZabDatabase.
        /// </summary>
        public static SleepConsolidationReport Consolidate(
            AgenticMemoryEngine engine,
            ZabDatabase db,
            SleepConsolidationOptions? options = null)
        {
            return Consolidate(engine, db, causalGraph: null, options);
        }

        /// <summary>
        /// Executes an Ouroboros cognitive sleep consolidation cycle including both episodic incidents and causal DAG transitions.
        /// </summary>
        public static SleepConsolidationReport Consolidate(
            AgenticMemoryEngine engine,
            ZabDatabase db,
            ZabCausalGraph? causalGraph,
            SleepConsolidationOptions? options = null)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (db == null) throw new ArgumentNullException(nameof(db));

            options ??= new SleepConsolidationOptions();
            var sw = Stopwatch.StartNew();
            var report = new SleepConsolidationReport();

            var episodes = engine.Episodic.GetAllEpisodes();
            report.TotalEpisodesScanned = episodes.Count;

            float[] tempSpan = new float[128];

            for (int i = 0; i < episodes.Count; i++)
            {
                var ep = episodes[i];
                bool hasEmbedding = engine.Episodic.TryGetEmbedding(ep.Id, tempSpan);
                ReadOnlySpan<float> emb = hasEmbedding ? tempSpan.AsSpan() : ReadOnlySpan<float>.Empty;

                if (ep.Success)
                {
                    // If episode was successful and encountered frequently, distill into System 1 instinct
                    if (ep.AccessCount >= options.MinAccessCountForReflex)
                    {
                        if (options.AutoCachePlans && !string.IsNullOrWhiteSpace(ep.Resolution))
                        {
                            db.CachePlan(
                                goal: ep.Issue,
                                solution: ep.Resolution,
                                stepsCount: 1,
                                confidence: 0.95f,
                                goalVector: emb);
                            report.PlansCached++;
                        }

                        if (!emb.IsEmpty && db.NeuralPolicy != null && !string.IsNullOrWhiteSpace(ep.Resolution))
                        {
                            db.AdaptNeuralWeights(emb, ep.Resolution, options.AdaptationLearningRate);
                            report.ReflexesSynthesized++;
                        }
                    }
                }
                else
                {
                    // Failed attempt -> Crystallize into explicit self-reflection lesson
                    db.AddReflexion(
                        goal: ep.Issue,
                        failureReason: "Incident failed in operational history.",
                        lesson: string.IsNullOrWhiteSpace(ep.Resolution)
                            ? "Avoid repeating past failed action paths without verification."
                            : ep.Resolution);
                    report.ReflexionsGenerated++;
                }
            }

            // Ouroboros Causal Transition Sweep
            if (causalGraph != null)
            {
                var edges = causalGraph.Edges;
                report.TotalCausalEdgesScanned = edges.Count;

                for (int i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    if (edge.ObservationCount >= options.MinCausalObservationsForPromotion &&
                        edge.Reward >= options.MinCausalRewardThreshold)
                    {
                        float[]? dequantized = null;
                        if (edge.ConditionVectorSq8 != null && edge.Dimension > 0)
                        {
                            dequantized = new float[edge.Dimension];
                            BinaryQuantizer.DequantizeSQ8(edge.ConditionVectorSq8, dequantized, edge.Scale, edge.Offset);
                        }

                        if (options.AutoCachePlans && !string.IsNullOrWhiteSpace(edge.ActionTaken))
                        {
                            db.CachePlan(
                                goal: edge.Condition,
                                solution: edge.ActionTaken,
                                stepsCount: 1,
                                confidence: edge.Reward,
                                goalVector: dequantized != null ? dequantized.AsSpan() : ReadOnlySpan<float>.Empty);
                            report.PlansCached++;
                        }

                        if (dequantized != null && db.NeuralPolicy != null && !string.IsNullOrWhiteSpace(edge.ActionTaken))
                        {
                            db.AdaptNeuralWeights(dequantized, edge.ActionTaken, options.AdaptationLearningRate);
                            report.ReflexesSynthesized++;
                            report.CausalReflexesPromoted++;
                        }
                    }
                    else if (edge.Reward <= options.MinCausalFailureThreshold && edge.ObservationCount >= 2)
                    {
                        db.AddReflexion(
                            goal: edge.Condition,
                            failureReason: $"Causal action '{edge.ActionTaken}' yielded severe failure: {edge.ObservedOutcome}",
                            lesson: $"Under condition '{edge.Condition}', strictly avoid action '{edge.ActionTaken}'.");
                        report.ReflexionsGenerated++;
                        report.CausalFailuresReflected++;
                    }
                }
            }

            // Continuous decay pruning
            engine.Episodic.PruneExpiredEpisodes(DateTime.UtcNow, options.PruneLowRetentionThreshold);

            // Persist full state to sovereign .zab container
            ZabMemoryStorageBridge.PersistToZab(engine, db);

            if (options.AutoCompactDatabase)
            {
                db.Compact();
            }

            sw.Stop();
            report.Elapsed = sw.Elapsed;
            return report;
        }
    }
}
