using System;
using System.Diagnostics;
using ZeroUI.Benchmarks.Categories;

namespace ZeroUI.Benchmarks
{
    /// <summary>
    /// Master Orchestrator for the unified ZeroUI Industrial Benchmark Suite (Directives A to F).
    /// </summary>
    public static class ZeroBenchmarkSuite
    {
        public static void RunAll()
        {
            var swTotal = Stopwatch.StartNew();

            Console.WriteLine("==================================================================================");
            Console.WriteLine("⚡ ZeroUI Master Architecture Benchmark Suite (Directive 27)");
            Console.WriteLine($"Environment: .NET {Environment.Version} | OS: {Environment.OSVersion} | Cores: {Environment.ProcessorCount}");
            Console.WriteLine("==================================================================================");
            Console.WriteLine();

            RunCategoryA();
            RunCategoryB();
            RunCategoryC();
            RunCategoryD();
            RunCategoryE();
            RunCategoryF();

            swTotal.Stop();
            Console.WriteLine("==================================================================================");
            Console.WriteLine($"✅ All 6 Architecture Benchmark Categories Completed in {swTotal.Elapsed.TotalSeconds:F2} seconds.");
            Console.WriteLine("==================================================================================");
        }

        public static void RunCategoryA() => RenderingBenchmarks.RunProfiler();
        public static void RunCategoryB() => GridBenchmarks.RunProfiler();
        public static void RunCategoryC() => TelemetryBenchmarks.RunProfiler();
        public static void RunCategoryD() => TagEngineBenchmarks.RunProfiler();
        public static void RunCategoryE() => ModbusBenchmarks.RunProfiler();
        public static void RunCategoryF() => HistorianBenchmarks.RunProfiler();
    }
}
