using System;
using System.Collections.Generic;
using System.Linq;
using ZeroInference.Core.Graph;

namespace ZeroInference.Core.Engine
{
    public sealed class MemoryPlan
    {
        public IReadOnlyDictionary<string, int> OutputToSlotMap { get; }
        public int TotalSlots { get; }

        public MemoryPlan(Dictionary<string, int> outputToSlotMap, int totalSlots)
        {
            OutputToSlotMap = outputToSlotMap;
            TotalSlots = totalSlots;
        }
    }

    /// <summary>
    /// Analyzes tensor liveness in the inference DAG to compute the minimum number
    /// of reusable memory buffer slots required for zero-allocation runtime execution.
    /// </summary>
    public static class MemoryPlanner
    {
        public static MemoryPlan Plan(InferenceGraph graph)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            var topoNodes = graph.GetTopologicalOrder();
            var outputToSlot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Compute last consumer step for each output
            var lastConsumerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            // Graph outputs must remain alive until the very end
            foreach (var outName in graph.OutputNames)
            {
                lastConsumerIndex[outName] = topoNodes.Count;
            }

            for (int i = 0; i < topoNodes.Count; i++)
            {
                foreach (var inName in topoNodes[i].InputNames)
                {
                    if (lastConsumerIndex.TryGetValue(inName, out int existing))
                    {
                        if (i > existing) lastConsumerIndex[inName] = i;
                    }
                    else
                    {
                        lastConsumerIndex[inName] = i;
                    }
                }
            }

            // Track active slots: slotId -> expirationStep
            var slotExpiration = new List<int>();

            for (int i = 0; i < topoNodes.Count; i++)
            {
                var node = topoNodes[i];
                string outName = node.OutputName;

                int expiry = lastConsumerIndex.TryGetValue(outName, out int lastCons) ? lastCons : i;

                // Find first free slot whose previous tenant expired before or at step i
                int assignedSlot = -1;
                for (int s = 0; s < slotExpiration.Count; s++)
                {
                    if (slotExpiration[s] <= i)
                    {
                        assignedSlot = s;
                        slotExpiration[s] = expiry;
                        break;
                    }
                }

                if (assignedSlot == -1)
                {
                    // Allocate new slot
                    assignedSlot = slotExpiration.Count;
                    slotExpiration.Add(expiry);
                }

                outputToSlot[outName] = assignedSlot;
            }

            return new MemoryPlan(outputToSlot, Math.Max(slotExpiration.Count, 1));
        }
    }

    /// <summary>
    /// Pre-allocated continuous memory arena holding dedicated slots for intermediate tensors.
    /// Completely eliminates heap garbage collection during real-time vision inference.
    /// </summary>
    public sealed class StaticTensorArena
    {
        private readonly float[][] _slots;
        private readonly int _slotCapacity;

        public int SlotCount => _slots.Length;
        public int SlotCapacity => _slotCapacity;

        public StaticTensorArena(int slotCount, int maxElementsPerSlot = 2 * 1024 * 1024)
        {
            if (slotCount < 1) slotCount = 1;
            _slotCapacity = maxElementsPerSlot;
            _slots = new float[slotCount][];
            for (int i = 0; i < slotCount; i++)
            {
                _slots[i] = new float[maxElementsPerSlot];
            }
        }

        public Span<float> GetSlotSpan(int slotIndex, int requiredLength)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            if (requiredLength > _slotCapacity)
                throw new InvalidOperationException($"Required buffer size ({requiredLength}) exceeds static arena slot capacity ({_slotCapacity}).");

            return _slots[slotIndex].AsSpan(0, requiredLength);
        }

        public float[] GetSlotBuffer(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            return _slots[slotIndex];
        }
    }
}
