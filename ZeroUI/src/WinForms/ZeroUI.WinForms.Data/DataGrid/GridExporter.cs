using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ZeroUI.Core.Data;

namespace ZeroUI.WinForms.DataGrid
{
    /// <summary>
    /// High-throughput zero-allocation streaming Excel (.xlsx) and CSV exporter for ZeroUI controls.
    /// Capable of streaming 1,000,000+ rows directly to disk at hundreds of thousands of rows per second.
    /// </summary>
    public static class GridExporter
    {
        public static Task<int> ExportToXlsxAsync(
            IZeroVirtualSource dataSource,
            ZGrid grid,
            string filePath,
            string sheetName = "Sheet1",
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            return GridDataExporter.ExportToXlsxAsync(
                dataSource,
                grid.Columns,
                grid.RowCount,
                filePath,
                row => grid.GetModelRowIndex(row),
                sheetName,
                progress,
                cancellationToken);
        }

        public static Task<int> ExportToCsvAsync(
            IZeroVirtualSource dataSource,
            ZGrid grid,
            string filePath,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            return GridDataExporter.ExportToCsvAsync(
                dataSource,
                grid.Columns,
                grid.RowCount,
                filePath,
                row => grid.GetModelRowIndex(row),
                progress,
                cancellationToken);
        }
    }

    /// <summary>
    /// Legacy alias for GridExporter.
    /// Preserved for backward compatibility.
    /// </summary>
    [Obsolete("ZeroGridExporter is deprecated. Please use GridExporter instead.")]
    public static class ZeroGridExporter
    {
        public static Task<int> ExportToXlsxAsync(
            IZeroVirtualSource dataSource,
            ZGrid grid,
            string filePath,
            string sheetName = "Sheet1",
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default) =>
            GridExporter.ExportToXlsxAsync(dataSource, grid, filePath, sheetName, progress, cancellationToken);

        public static Task<int> ExportToCsvAsync(
            IZeroVirtualSource dataSource,
            ZGrid grid,
            string filePath,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default) =>
            GridExporter.ExportToCsvAsync(dataSource, grid, filePath, progress, cancellationToken);
    }
}
