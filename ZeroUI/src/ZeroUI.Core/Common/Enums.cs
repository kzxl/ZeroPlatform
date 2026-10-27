namespace ZeroUI.Core.Common
{
    public enum CellAlignment : byte
    {
        Left = 0,
        Center = 1,
        Right = 2
    }

    public enum SortDirection : byte
    {
        None = 0,
        Ascending = 1,
        Descending = 2
    }

    public enum GridDensity : byte
    {
        Compact = 24,
        Middle = 28,
        Loose = 36
    }

    public enum ZeroGridSelectionMode : byte
    {
        SingleRow = 0,
        MultiRow = 1,
        Cell = 2,
        Block = 3
    }

    public enum SummaryType : byte
    {
        None = 0,
        Sum = 1,
        Count = 2,
        Average = 3,
        Min = 4,
        Max = 5
    }

    public enum GridColumnType : byte
    {
        Text = 0,
        Numeric = 1,
        DateTime = 2,
        Boolean = 3,
        Custom = 4,
        Masked = 5
    }

    public enum SparklineType : byte
    {
        None = 0,
        Line = 1,
        Area = 2,
        Bar = 3,
        WinLoss = 4
    }
}

