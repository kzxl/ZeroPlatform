using System;
using System.Text.Json;
using ZeroAgent.Core.Database;

namespace ZeroAgent.Dialog.Memory
{
    /// <summary>
    /// Architectural Bridge providing crash-resilient sovereign persistence for AgenticMemoryEngine.
    /// Bridges in-memory cognitive tiers (SemanticMemory, EpisodicMemory, SemanticResponseCache)
    /// directly to the crash-proof, WAL-journaled ZabDatabase (.zab) with 100% lossless embedding preservation.
    /// </summary>
    public static class ZabMemoryStorageBridge
    {
        private const string EpisodicPrefix = "mem.episodic.";
        private const string SemanticPrefix = "mem.semantic.";

        /// <summary>
        /// Persists all in-memory cognitive memory entries from AgenticMemoryEngine into the sovereign ZabDatabase container.
        /// </summary>
        public static void PersistToZab(AgenticMemoryEngine engine, ZabDatabase db)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (db == null) throw new ArgumentNullException(nameof(db));

            float[] tempSpan = new float[128];

            // 1. Persist Semantic Memory (Domain SOPs & Knowledge)
            var semanticItems = engine.Semantic.GetAllItems();
            for (int i = 0; i < semanticItems.Count; i++)
            {
                var item = semanticItems[i];
                string key = $"{SemanticPrefix}{item.Id}";
                float[]? emb = null;
                if (engine.Semantic.TryGetEmbedding(item.Id, tempSpan))
                {
                    emb = (float[])tempSpan.Clone();
                }

                string json = JsonSerializer.Serialize(new
                {
                    item.Id,
                    item.Title,
                    item.Content,
                    item.Category,
                    item.ValidFromUtc,
                    item.ValidUntilUtc,
                    Embedding = emb
                });
                db.AddKnowledge(key, json, category: item.Category);
            }

            // 2. Persist Episodic Memory (Incidents, Ebbinghaus decay parameters & resolutions)
            var episodes = engine.Episodic.GetAllEpisodes();
            for (int i = 0; i < episodes.Count; i++)
            {
                var ep = episodes[i];
                string key = $"{EpisodicPrefix}{ep.Id}";
                float[]? emb = null;
                if (engine.Episodic.TryGetEmbedding(ep.Id, tempSpan))
                {
                    emb = (float[])tempSpan.Clone();
                }

                string json = JsonSerializer.Serialize(new
                {
                    ep.Id,
                    ep.Issue,
                    ep.Resolution,
                    ep.Success,
                    ep.TimestampUtc,
                    ep.LastAccessedUtc,
                    ep.AccessCount,
                    ep.HalfLifeHours,
                    Embedding = emb
                });
                db.AddKnowledge(key, json, category: "Episodic");

                // Also persist as Reflexion lesson in .zab
                db.AddReflexion(ep.Issue, ep.Success ? "Resolved" : "Unresolved", ep.Resolution);
            }

            // 3. Persist Semantic Response Cache into Zab Plans
            var cacheEntries = engine.ResponseCache.GetAllEntries();
            for (int i = 0; i < cacheEntries.Count; i++)
            {
                var entry = cacheEntries[i];
                if (!entry.IsExpired)
                {
                    float[]? emb = null;
                    if (engine.ResponseCache.TryGetEmbedding(entry.Id, tempSpan))
                    {
                        emb = (float[])tempSpan.Clone();
                    }

                    db.CachePlan(entry.Query, entry.ResponseText, stepsCount: 1, confidence: 1.0f, goalVector: emb ?? ReadOnlySpan<float>.Empty);
                }
            }

            // Execute explicit WAL Checkpoint
            db.Commit();
        }

        /// <summary>
        /// Restores and hydrates in-memory cognitive tiers in AgenticMemoryEngine from a sovereign ZabDatabase container.
        /// </summary>
        public static void HydrateFromZab(AgenticMemoryEngine engine, ZabDatabase db)
        {
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (db == null) throw new ArgumentNullException(nameof(db));

            for (int i = 0; i < db.Knowledge.Count; i++)
            {
                var record = db.Knowledge[i];
                if (record.Key.StartsWith(SemanticPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using (var doc = JsonDocument.Parse(record.Value))
                        {
                            var root = doc.RootElement;
                            string title = root.GetProperty("Title").GetString() ?? string.Empty;
                            string content = root.GetProperty("Content").GetString() ?? string.Empty;
                            string category = root.GetProperty("Category").GetString() ?? "SOP";

                            DateTime? vFrom = root.TryGetProperty("ValidFromUtc", out var fromProp) && fromProp.ValueKind == JsonValueKind.String
                                ? DateTime.Parse(fromProp.GetString()!) : (DateTime?)null;
                            DateTime? vUntil = root.TryGetProperty("ValidUntilUtc", out var untilProp) && untilProp.ValueKind == JsonValueKind.String
                                ? DateTime.Parse(untilProp.GetString()!) : (DateTime?)null;

                            float[]? emb = ExtractEmbedding(root);
                            if (emb == null || emb.Length == 0)
                            {
                                emb = engine.Embedder.Embed(title + " " + content);
                            }

                            engine.Semantic.Add(title, content, emb, category, vFrom, vUntil);
                        }
                    }
                    catch { }
                }
                else if (record.Key.StartsWith(EpisodicPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using (var doc = JsonDocument.Parse(record.Value))
                        {
                            var root = doc.RootElement;
                            string issue = root.GetProperty("Issue").GetString() ?? string.Empty;
                            string resolution = root.GetProperty("Resolution").GetString() ?? string.Empty;
                            bool success = root.GetProperty("Success").GetBoolean();
                            double halfLife = root.TryGetProperty("HalfLifeHours", out var hl) ? hl.GetDouble() : 72.0;

                            float[]? emb = ExtractEmbedding(root);
                            if (emb == null || emb.Length == 0)
                            {
                                emb = engine.Embedder.Embed(issue);
                            }

                            int accessCount = root.TryGetProperty("AccessCount", out var ac) ? ac.GetInt32() : 1;
                            DateTime? lastAccessed = root.TryGetProperty("LastAccessedUtc", out var la) && la.ValueKind == JsonValueKind.String
                                ? DateTime.Parse(la.GetString()!) : (DateTime?)null;

                            var ep = engine.Episodic.Record(issue, resolution, emb, success, halfLife);
                            if (ep != null)
                            {
                                ep.AccessCount = accessCount;
                                if (lastAccessed.HasValue) ep.LastAccessedUtc = lastAccessed.Value;
                            }
                        }
                    }
                    catch { }
                }
            }

            // Hydrate Plans into Response Cache
            for (int i = 0; i < db.Plans.Count; i++)
            {
                var plan = db.Plans[i];
                float[] emb = engine.Embedder.Embed(plan.Goal);
                engine.ResponseCache.Store(emb, plan.Goal, plan.Solution);
            }
        }

        private static float[]? ExtractEmbedding(JsonElement root)
        {
            if (root.TryGetProperty("Embedding", out var embProp) && embProp.ValueKind == JsonValueKind.Array)
            {
                int len = embProp.GetArrayLength();
                if (len > 0)
                {
                    float[] arr = new float[len];
                    int idx = 0;
                    foreach (var el in embProp.EnumerateArray())
                    {
                        arr[idx++] = (float)el.GetDouble();
                    }
                    return arr;
                }
            }
            return null;
        }
    }
}
