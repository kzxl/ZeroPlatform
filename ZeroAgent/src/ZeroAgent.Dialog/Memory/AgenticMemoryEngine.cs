using System;
using System.Collections.Concurrent;
using ZeroAgent.Dialog.Embedding;

namespace ZeroAgent.Dialog.Memory
{
    /// <summary>
    /// Master 4-tier Agentic Memory Engine:
    /// - Working Memory (Short-Term context, slots, pronoun resolution)
    /// - Episodic Memory (Past incidents, human-verified resolutions)
    /// - Semantic Memory (SOP documents, machine manuals, domain FAQs)
    /// - Profile Memory (Operator identity, permissions, RBAC policies)
    /// </summary>
    public sealed class AgenticMemoryEngine
    {
        private readonly ConcurrentDictionary<string, WorkingMemory> _workingSessions = new ConcurrentDictionary<string, WorkingMemory>(StringComparer.OrdinalIgnoreCase);

        public EpisodicMemory Episodic { get; }
        public SemanticMemory Semantic { get; }
        public SemanticResponseCache ResponseCache { get; }
        public ProfileMemory Profiles { get; } = new ProfileMemory();
        public LexicalSemanticEmbedder Embedder { get; }

        public AgenticMemoryEngine(int dimension = 128)
            : this(dimension, new HybridSemanticEmbedder(dimension))
        {
        }

        public AgenticMemoryEngine(int dimension, LexicalSemanticEmbedder embedder)
        {
            Embedder = embedder ?? new HybridSemanticEmbedder(dimension);
            Episodic = new EpisodicMemory(dimension);
            Semantic = new SemanticMemory(dimension);
            ResponseCache = new SemanticResponseCache(dimension);
        }

        public WorkingMemory GetWorkingMemory(string sessionId)
        {
            return _workingSessions.GetOrAdd(sessionId, id => new WorkingMemory(id));
        }

        public void ClearSession(string sessionId)
        {
            _workingSessions.TryRemove(sessionId, out _);
        }
    }
}
