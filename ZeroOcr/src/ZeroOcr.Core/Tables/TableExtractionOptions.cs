using System;

namespace ZeroOcr.Core.Tables;

/// <summary>
/// Operational parameters and heuristic tuning for table structure recognition.
/// </summary>
public sealed class TableExtractionOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to analyze image morphological line kernels when an image buffer is available.
    /// Default is true.
    /// </summary>
    public bool DetectBorderedTables { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to detect borderless tables via multi-column spatial alignment.
    /// Default is true.
    /// </summary>
    public bool DetectBorderlessTables { get; set; } = true;

    /// <summary>
    /// Minimum number of rows to qualify a cluster as a valid table. Default is 2.
    /// </summary>
    public int MinRows { get; set; } = 2;

    /// <summary>
    /// Minimum number of columns to qualify a cluster as a valid table. Default is 2.
    /// </summary>
    public int MinColumns { get; set; } = 2;

    /// <summary>
    /// Minimum vertical overlap ratio between words to group them into the same row. Default is 0.5f.
    /// </summary>
    public float RowVerticalTolerance { get; set; } = 0.5f;

    /// <summary>
    /// Minimum whitespace gap in pixels between columns to recognize column separation. Default is 15.0f.
    /// </summary>
    public float ColumnGapThreshold { get; set; } = 15.0f;

    /// <summary>
    /// Whether to automatically classify the top row as a table header. Default is true.
    /// </summary>
    public bool AutoDetectHeaders { get; set; } = true;
}
