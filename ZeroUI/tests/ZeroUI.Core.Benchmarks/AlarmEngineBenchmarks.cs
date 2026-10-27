using System;
using BenchmarkDotNet.Attributes;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Benchmarks
{
    [MemoryDiagnoser]
    [ShortRunJob]
    public class AlarmEngineBenchmarks
    {
        private const int AlarmCount = 10_000;

        [Benchmark(Description = "Alarm Storm: Raise 10,000 Alarms")]
        public int AlarmStorm_Raise10k()
        {
            for (int i = 0; i < AlarmCount; i++)
            {
                ScadaAlarmEngine.RaiseAlarm(
                    $"STORM_ALM_{i}",
                    $"Plant.Substation.Breaker{i % 50}",
                    $"Overcurrent trip on circuit breaker {i}",
                    (ScadaAlarmSeverity)(i % 5),
                    i * 10.5);
            }
            return ScadaAlarmEngine.GetActiveAlarms().Count;
        }

        [Benchmark(Description = "Alarm Acknowledge All: 10,000 Alarms")]
        public int AcknowledgeAll_10k()
        {
            return ScadaAlarmEngine.AcknowledgeAll("SafetyOperator");
        }

        [Benchmark(Description = "Alarm Severity Summary Aggregation")]
        public AlarmSeverityCount Tally_SeveritySummary()
        {
            return ScadaAlarmEngine.GetAlarmSummary();
        }
    }
}
