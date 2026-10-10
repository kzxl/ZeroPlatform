using System;
using System.Reflection;
using ZeroPlatform.Concurrency;

namespace Audit.Tests
{
    public static class WraparoundAuditTest
    {
        public static void Run()
        {
            Console.WriteLine("=== AUDIT TEST: ZeroRingBuffer Wraparound Near int.MaxValue ===");
            var ring = new ZeroRingBuffer<int>(64);

            // Use reflection to set _tail and _head near int.MaxValue
            var tailField = typeof(ZeroRingBufferProducer<int>).GetField("_tail", BindingFlags.NonPublic | BindingFlags.Instance);
            var headField = typeof(ZeroRingBuffer<int>).GetField("_head", BindingFlags.NonPublic | BindingFlags.Instance);
            var cachedHeadField = typeof(ZeroRingBufferProducer<int>).GetField("_cachedHead", BindingFlags.NonPublic | BindingFlags.Instance);
            var cachedTailField = typeof(ZeroRingBuffer<int>).GetField("_cachedTail", BindingFlags.NonPublic | BindingFlags.Instance);

            int nearMax = int.MaxValue - 10;
            tailField!.SetValue(ring, nearMax);
            headField!.SetValue(ring, nearMax);
            cachedHeadField!.SetValue(ring, nearMax);
            cachedTailField!.SetValue(ring, nearMax);

            Console.WriteLine($"Initial state set to: {nearMax}");

            // Enqueue 20 items: will overflow int.MaxValue into negative int.MinValue!
            int enqueued = 0;
            for (int i = 0; i < 20; i++)
            {
                if (ring.TryEnqueue(i))
                {
                    enqueued++;
                }
                else
                {
                    Console.WriteLine($"[FAILURE] Enqueue failed at item {i}!");
                    break;
                }
            }
            Console.WriteLine($"Successfully enqueued: {enqueued}/20 items across int.MaxValue boundary.");

            // Now dequeue
            int dequeued = 0;
            while (ring.TryDequeue(out int val))
            {
                dequeued++;
            }
            Console.WriteLine($"Dequeued: {dequeued}/{enqueued} items.");

            if (dequeued != enqueued)
            {
                Console.WriteLine($"[BUG CONFIRMED] Lost items or premature empty detection! Enqueued: {enqueued}, Dequeued: {dequeued}");
            }
            else
            {
                Console.WriteLine("[PASS] Wraparound handled successfully.");
            }
        }
    }
}
