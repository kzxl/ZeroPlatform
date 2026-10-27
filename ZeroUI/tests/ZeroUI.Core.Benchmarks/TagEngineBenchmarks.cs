using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class TagEngineBenchmarks
    {
        private const int SingleThreadBatch = 50_000;
        private const int MultiThreadWorkers = 4;
        private const int ItemsPerWorker = 25_000;

        [Benchmark(Description = "SetTagValue Single-Thread 50,000 Ops")]
        public int SingleThread_50k_Updates()
        {
            int published = 0;
            for (int i = 0; i < SingleThreadBatch; i++)
            {
                if (ZeroTagEngine.SetTagValue("Line1.Motor.RPM", 1450.0 + (i % 100), ScadaQuality.Good))
                {
                    published++;
                }
            }
            return published;
        }

        [Benchmark(Description = "SetTagValue Deadband Filter (Jitter Suppressed) 50,000 Ops")]
        public int Deadband_Filter_50k_Ops()
        {
            ZeroTagEngine.SetDeadband("Line1.Pressure", 2.0);
            ZeroTagEngine.SetTagValue("Line1.Pressure", 50.0);

            int published = 0;
            for (int i = 0; i < SingleThreadBatch; i++)
            {
                // Jitter within 0.1 delta < 2.0 deadband
                double jitterVal = 50.0 + (i % 5) * 0.02;
                if (ZeroTagEngine.SetTagValue("Line1.Pressure", jitterVal, ScadaQuality.Good))
                {
                    published++;
                }
            }
            return published;
        }

        [Benchmark(Description = "SetTagValue Multi-Threaded 4 Workers x 25,000 (100k Total)")]
        public int MultiThreaded_100k_Concurrent()
        {
            int totalPublished = 0;
            Parallel.For(0, MultiThreadWorkers, workerId =>
            {
                string tag = $"Line{workerId}.Telemetry.Sensor";
                int localPub = 0;
                for (int i = 0; i < ItemsPerWorker; i++)
                {
                    if (ZeroTagEngine.SetTagValue(tag, i * 1.5, ScadaQuality.Good))
                    {
                        localPub++;
                    }
                }
                System.Threading.Interlocked.Add(ref totalPublished, localPub);
            });
            return totalPublished;
        }
    }
}
