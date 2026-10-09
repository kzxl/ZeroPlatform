namespace ZeroDocuments.Excel
{
    /// <summary>
    /// Entry point for ZeroDocuments Excel fluent builder and high-level operations.
    /// </summary>
    public static class ZeroExcel
    {
        /// <summary>
        /// Creates a new fluent Excel workbook builder supporting multiple worksheets.
        /// </summary>
        public static ExcelWorkbookBuilder Create() => new ExcelWorkbookBuilder();

        /// <summary>
        /// Streams an IDataReader directly to an Excel (.xlsx) file on disk atomically.
        /// </summary>
        public static void WriteFromDataReader(string filePath, System.Data.IDataReader reader, string sheetName = "Sheet1", bool includeHeaders = true) =>
            ExcelWriter.WriteToFile(filePath, reader, sheetName, includeHeaders);

        /// <summary>
        /// Streams an IDataReader directly to an Excel (.xlsx) stream without buffering.
        /// </summary>
        public static void WriteFromDataReader(System.IO.Stream stream, System.Data.IDataReader reader, string sheetName = "Sheet1", bool includeHeaders = true) =>
            ExcelWriter.WriteToStream(stream, reader, sheetName, includeHeaders);
    }
}
