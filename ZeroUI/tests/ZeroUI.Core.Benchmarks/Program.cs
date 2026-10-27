using System;
using System.Linq;
using BenchmarkDotNet.Running;

namespace ZeroUI.Core.Benchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Contains("--bdn", StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine("Executing full BenchmarkDotNet runner...");
                BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
            }
            else
            {
                // Default high-speed profiler for immediate diagnostics and telemetry verification
                FastProfilerRunner.RunAll();
            }
        }
    }
}
