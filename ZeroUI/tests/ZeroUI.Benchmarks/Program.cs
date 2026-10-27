using System;
using System.Linq;
using BenchmarkDotNet.Running;

namespace ZeroUI.Benchmarks
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Contains("--bdn", StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine("Executing full BenchmarkDotNet runner...");
                BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
                return;
            }

            if (args.Length > 0)
            {
                string cmd = args[0].TrimStart('-').ToLowerInvariant();
                switch (cmd)
                {
                    case "a":
                    case "rendering":
                        ZeroBenchmarkSuite.RunCategoryA();
                        return;
                    case "b":
                    case "grid":
                        ZeroBenchmarkSuite.RunCategoryB();
                        return;
                    case "c":
                    case "telemetry":
                        ZeroBenchmarkSuite.RunCategoryC();
                        return;
                    case "d":
                    case "tagengine":
                        ZeroBenchmarkSuite.RunCategoryD();
                        return;
                    case "e":
                    case "modbus":
                        ZeroBenchmarkSuite.RunCategoryE();
                        return;
                    case "f":
                    case "historian":
                        ZeroBenchmarkSuite.RunCategoryF();
                        return;
                    case "help":
                    case "?":
                        PrintHelp();
                        return;
                }
            }

            // Default: run all categories A through F
            ZeroBenchmarkSuite.RunAll();
        }

        private static void PrintHelp()
        {
            Console.WriteLine("ZeroUI.Benchmarks Usage:");
            Console.WriteLine("  dotnet run                         - Run full suite (Categories A to F)");
            Console.WriteLine("  dotnet run -- a                    - Run Category A: Rendering");
            Console.WriteLine("  dotnet run -- b                    - Run Category B: Grid Virtualization");
            Console.WriteLine("  dotnet run -- c                    - Run Category C: Telemetry");
            Console.WriteLine("  dotnet run -- d                    - Run Category D: TagEngine");
            Console.WriteLine("  dotnet run -- e                    - Run Category E: Modbus");
            Console.WriteLine("  dotnet run -- f                    - Run Category F: Historian");
            Console.WriteLine("  dotnet run -- --bdn                - Run BenchmarkDotNet harness");
        }
    }
}
