using System;
using System.Collections.Generic;
using ZeroData.Core;

namespace ZeroStorage.Core.TimeSeries
{
    /// <summary>
    /// Bidirectional zero-allocation bridge between ZeroStorage TSDB blocks and ZeroData columnar DataFrames.
    /// </summary>
    public static class TimeSeriesDataFrameBridge
    {
        /// <summary>
        /// Decompresses a univariate TimeSeriesBlock directly into a high-performance DataFrame.
        /// </summary>
        public static DataFrame ToDataFrame(this TimeSeriesBlock block)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));

            var points = block.Decompress();
            var dtArray = new DateTime[points.Count];
            var valArray = new double[points.Count];

            for (int i = 0; i < points.Count; i++)
            {
                dtArray[i] = DateTimeOffset.FromUnixTimeMilliseconds(points[i].TimestampMs).UtcDateTime;
                valArray[i] = points[i].Value;
            }

            var df = new DataFrame();
            df.AddColumn(new DataColumn<DateTime>("Timestamp", dtArray));
            df.AddColumn(new DataColumn<double>("Value", valArray));
            return df;
        }

        /// <summary>
        /// Converts a list of MultiMetricRows into a typed columnar DataFrame based on the provided schema.
        /// </summary>
        public static DataFrame ToDataFrame(this MultiMetricSchema schema, IReadOnlyList<MultiMetricRow> rows)
        {
            if (schema == null) throw new ArgumentNullException(nameof(schema));
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            int rowCount = rows.Count;
            var dtArray = new DateTime[rowCount];

            for (int r = 0; r < rowCount; r++)
            {
                dtArray[r] = DateTimeOffset.FromUnixTimeMilliseconds(rows[r].TimestampMs).UtcDateTime;
            }

            var df = new DataFrame();
            df.AddColumn(new DataColumn<DateTime>("Timestamp", dtArray));

            for (int f = 0; f < schema.Fields.Count; f++)
            {
                var field = schema.Fields[f];
                IDataColumn col = CreateTypedColumn(field.Name, field.Type, rows, f, rowCount);
                df.AddColumn(col);
            }

            return df;
        }

        /// <summary>
        /// Converts a ColumnarBatch directly into a typed columnar DataFrame with zero element-by-element copy overhead.
        /// </summary>
        public static DataFrame ToDataFrame(this ColumnarBatch batch)
        {
            if (batch == null) throw new ArgumentNullException(nameof(batch));

            int rowCount = batch.RowCount;
            var dtArray = new DateTime[rowCount];
            for (int r = 0; r < rowCount; r++)
            {
                dtArray[r] = DateTimeOffset.FromUnixTimeMilliseconds(batch.Timestamps[r]).UtcDateTime;
            }

            var df = new DataFrame();
            df.AddColumn(new DataColumn<DateTime>("Timestamp", dtArray));

            for (int f = 0; f < batch.Schema.Fields.Count; f++)
            {
                var field = batch.Schema.Fields[f];
                switch (field.Type)
                {
                    case MultiMetricType.Float64:
                        df.AddColumn(new DataColumn<double>(field.Name, batch.GetFloat64Column(f)));
                        break;
                    case MultiMetricType.Int64:
                        df.AddColumn(new DataColumn<long>(field.Name, batch.GetInt64Column(f)));
                        break;
                    case MultiMetricType.Int32:
                        df.AddColumn(new DataColumn<int>(field.Name, batch.GetInt32Column(f)));
                        break;
                    case MultiMetricType.Boolean:
                        df.AddColumn(new DataColumn<bool>(field.Name, batch.GetBooleanColumn(f)));
                        break;
                    case MultiMetricType.Decimal:
                        df.AddColumn(new DataColumn<decimal>(field.Name, batch.GetDecimalColumn(f)));
                        break;
                    case MultiMetricType.String:
                        df.AddColumn(new DataColumn<string>(field.Name, batch.GetStringColumn(f)));
                        break;
                }
            }

            return df;
        }

        /// <summary>
        /// Decompresses a MultiMetricBlock directly into a typed columnar DataFrame via ColumnarBatch.
        /// </summary>
        public static DataFrame ToDataFrame(this MultiMetricBlock block)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            return block.DecompressColumnar().ToDataFrame();
        }

        private static IDataColumn CreateTypedColumn(string name, MultiMetricType type, IReadOnlyList<MultiMetricRow> rows, int fieldIdx, int rowCount)
        {
            switch (type)
            {
                case MultiMetricType.Float64:
                    var f64 = new DataColumn<double>(name, rowCount);
                    for (int r = 0; r < rowCount; r++)
                    {
                        var val = rows[r].Values[fieldIdx];
                        if (val == null) f64.SetNull(r);
                        else f64.SetValid(r, val is double d ? d : Convert.ToDouble(val));
                    }
                    return f64;

                case MultiMetricType.Int32:
                    var i32 = new DataColumn<int>(name, rowCount);
                    for (int r = 0; r < rowCount; r++)
                    {
                        var val = rows[r].Values[fieldIdx];
                        if (val == null) i32.SetNull(r);
                        else i32.SetValid(r, val is int iv ? iv : Convert.ToInt32(val));
                    }
                    return i32;

                case MultiMetricType.Int64:
                    var i64 = new DataColumn<long>(name, rowCount);
                    for (int r = 0; r < rowCount; r++)
                    {
                        var val = rows[r].Values[fieldIdx];
                        if (val == null) i64.SetNull(r);
                        else i64.SetValid(r, val is long lv ? lv : Convert.ToInt64(val));
                    }
                    return i64;

                case MultiMetricType.Boolean:
                    var bCol = new DataColumn<bool>(name, rowCount);
                    for (int r = 0; r < rowCount; r++)
                    {
                        var val = rows[r].Values[fieldIdx];
                        if (val == null) bCol.SetNull(r);
                        else bCol.SetValid(r, val is bool bv && bv);
                    }
                    return bCol;

                case MultiMetricType.Decimal:
                    var decCol = new DataColumn<decimal>(name, rowCount);
                    for (int r = 0; r < rowCount; r++)
                    {
                        var val = rows[r].Values[fieldIdx];
                        if (val == null) decCol.SetNull(r);
                        else decCol.SetValid(r, val is decimal dv ? dv : Convert.ToDecimal(val));
                    }
                    return decCol;

                default:
                    var strCol = new DataColumn<string>(name, rowCount);
                    for (int r = 0; r < rowCount; r++)
                    {
                        var val = rows[r].Values[fieldIdx];
                        if (val == null) strCol.SetNull(r);
                        else strCol.SetValid(r, val.ToString()!);
                    }
                    return strCol;
            }
        }
    }
}
