using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using ZeroAgent.Core.Tools;
using ZeroPrompt.Core.Grammar;

namespace ZeroAgent.Tools.System
{
    /// <summary>
    /// Host operating system and runtime telemetry sensor tool for AI diagnostic agents.
    /// Provides CPU core count, thread allocation, managed heap stats, and working set footprint.
    /// </summary>
    public static class HostTelemetryTool
    {
        private static readonly DateTime _startTime = DateTime.UtcNow;

        public static void RegisterAll(AgentToolRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));

            var schema = new JsonSchemaConstraint("GetHostTelemetry");

            registry.Register(new AgentTool(
                "get_system_telemetry",
                "Queries host system runtime statistics including CPU logical cores, RAM consumption, GC heap, and thread count.",
                "none",
                ExecuteGetTelemetryAsync,
                schema: schema,
                requiresApproval: false
            ));
        }

        private static Task<string> ExecuteGetTelemetryAsync(string arg)
        {
            try
            {
                var process = Process.GetCurrentProcess();
                long workingSetMb = process.WorkingSet64 / (1024 * 1024);
                long gcMemoryMb = GC.GetTotalMemory(forceFullCollection: false) / (1024 * 1024);
                int threadCount = process.Threads.Count;
                int processorCount = Environment.ProcessorCount;
                var uptime = DateTime.UtcNow - _startTime;

                var telemetry = new
                {
                    logicalProcessors = processorCount,
                    workingSetMemoryMb = workingSetMb,
                    gcAllocatedMemoryMb = gcMemoryMb,
                    activeThreads = threadCount,
                    uptimeSeconds = (long)uptime.TotalSeconds,
                    framework = Environment.Version.ToString(),
                    operatingSystem = Environment.OSVersion.ToString()
                };

                return Task.FromResult(JsonSerializer.Serialize(telemetry));
            }
            catch (Exception ex)
            {
                return Task.FromResult($"Failed to query system telemetry: {ex.Message}");
            }
        }
    }
}
