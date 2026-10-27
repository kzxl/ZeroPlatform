using System;
using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using ZeroUI.Core.Common;
using ZeroUI.Core.Scene;
using ZeroUI.WinForms.Native;
using ZeroUI.WinForms.Rendering;

namespace ZeroUI.Benchmarks.Categories
{
    /// <summary>
    /// Category A: Rendering Performance Benchmarks.
    /// Evaluates direct DIBSection rasterization across 100 cells, 1k cells, 10k cells, and 100k graphics primitives.
    /// </summary>
    [MemoryDiagnoser]
    public class RenderingBenchmarks
    {
        private MemoryDIBSection? _dib;
        private ZeroScene? _scene;

        [GlobalSetup]
        public void Setup()
        {
            _dib = new MemoryDIBSection();
            _dib.EnsureSize(1920, 1080, IntPtr.Zero);

            _scene = new ZeroScene();
            for (int i = 0; i < 100_000; i++)
            {
                var node = new BenchmarkPrimitiveNode($"node_{i}", (i * 13) % 1920, (i * 17) % 1080, 16, 16);
                _scene.AddNode(node);
            }
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _dib?.Dispose();
            _dib = null;
        }

        [Benchmark(OperationsPerInvoke = 100)]
        public void Draw100Cells() => ExecuteDrawCells(100);

        [Benchmark(OperationsPerInvoke = 1_000)]
        public void Draw1KCells() => ExecuteDrawCells(1_000);

        [Benchmark(OperationsPerInvoke = 10_000)]
        public void Draw10KCells() => ExecuteDrawCells(10_000);

        [Benchmark(OperationsPerInvoke = 100_000)]
        public void Draw100KPrimitives()
        {
            if (_dib == null) return;
            const uint color = 0x002A2A2A;

            for (int i = 0; i < 100_000; i++)
            {
                int left = (i * 11) % 1900;
                int top = (i * 7) % 1060;
                _dib.FillRectangle(left, top, 16, 16, color);
            }
        }

        private void ExecuteDrawCells(int cellCount)
        {
            if (_dib == null) return;
            const int cellWidth = 100;
            const int cellHeight = 24;
            const int cols = 10;

            const uint bg = 0x001E1E1E;
            const uint border = 0x00333333;
            var text = "CellVal".AsSpan();
            RECT r = new RECT();

            for (int i = 0; i < cellCount; i++)
            {
                int c = i % cols;
                int row = i / cols;

                int x = c * cellWidth;
                int y = row * cellHeight;

                r.Left = x;
                r.Top = y;
                r.Right = x + cellWidth;
                r.Bottom = y + cellHeight;

                _dib.FillRectangle(x, y, cellWidth, cellHeight, bg);
                _dib.FillRectangle(x, y + cellHeight - 1, cellWidth, 1, border);
                _dib.FillRectangle(x + cellWidth - 1, y, 1, cellHeight, border);
                _dib.DrawText(text, ref r, 0x00D4D4D4, CellAlignment.Left, 14);
            }
        }

        public static void RunProfiler()
        {
            Console.WriteLine("----------------------------------------------------------------------------------");
            Console.WriteLine("A. Rendering Benchmarks (Direct Win32 MemoryDIBSection & Primitive Blitting)");
            Console.WriteLine("----------------------------------------------------------------------------------");

            var bench = new RenderingBenchmarks();
            bench.Setup();

            int[] cellCounts = { 100, 1_000, 10_000 };
            foreach (var count in cellCounts)
            {
                int iters = count <= 100 ? 100 : count <= 1_000 ? 50 : 20;
                var res = StatisticalRunner.Run(() => bench.ExecuteDrawCells(count), warmupCount: 10, iterationCount: iters, scaleOpsPerIter: count);
                Console.WriteLine($"  • Draw {count,6:N0} cells:        P50: {res.P50Ms,6:F3} ms | P95: {res.P95Ms,6:F3} ms | P99: {res.P99Ms,6:F3} ms ({res.OpsPerSec,10:N0} cells/s) | Alloc: {res.AllocatedBytesPerOp,2} B | Gen0: {res.Gen0Collections}");
            }

            // Draw 100K Primitives
            {
                var res = StatisticalRunner.Run(() => bench.Draw100KPrimitives(), warmupCount: 5, iterationCount: 20, scaleOpsPerIter: 100_000);
                Console.WriteLine($"  • Draw 100,000 primitives:   P50: {res.P50Ms,6:F3} ms | P95: {res.P95Ms,6:F3} ms | P99: {res.P99Ms,6:F3} ms ({res.OpsPerSec,10:N0} prim/s)  | Alloc: {res.AllocatedBytesPerOp,2} B | Gen0: {res.Gen0Collections}");
            }

            bench.Cleanup();
            Console.WriteLine();
        }

        private sealed class BenchmarkPrimitiveNode : SceneNode
        {
            public BenchmarkPrimitiveNode(string id, float x, float y, float w, float h)
            {
                Id = id;
                Transform.SetPosition(x, y);
                Width = w;
                Height = h;
            }

            public override void Render(object graphicsContext, in RenderContext context) { }
        }
    }
}
