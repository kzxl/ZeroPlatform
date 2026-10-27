using System;
using System.Collections.Generic;
using System.Data;
using ZeroPrimitives;

namespace ZeroData.Core
{
    public partial class DataFrame
    {
        /// <summary>
        /// Ingests data directly from an IDataReader (e.g. SqlDataReader, SQLiteDataReader) into typed columnar arrays.
        /// Uses zero-boxing typed appenders for primitives (int, long, double, decimal, bool, DateTime, Guid),
        /// eliminating Gen0/Gen1/Gen2 GC pressure and achieving maximum wire-to-memory ingestion throughput.
        /// </summary>
        public static DataFrame FromDataReader(IDataReader reader, int maxRows = -1)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));

            int fieldCount = reader.FieldCount;
            var colNames = new string[fieldCount];
            var colTypes = new Type[fieldCount];
            var appenders = new IColumnAppender[fieldCount];

            for (int i = 0; i < fieldCount; i++)
            {
                colNames[i] = reader.GetName(i);
                colTypes[i] = reader.GetFieldType(i);
                appenders[i] = CreateAppender(colNames[i], colTypes[i]);
            }

            int count = 0;
            while (reader.Read())
            {
                for (int i = 0; i < fieldCount; i++)
                {
                    appenders[i].AppendFromReader(reader, i);
                }
                count++;
                if (maxRows > 0 && count >= maxRows) break;
            }

            var df = new DataFrame();
            for (int i = 0; i < fieldCount; i++)
            {
                df.AddColumn(appenders[i].Build());
            }

            return df;
        }

        /// <summary>
        /// Converts an existing System.Data.DataTable into a high-performance Columnar DataFrame.
        /// </summary>
        public static DataFrame FromDataTable(DataTable dataTable)
        {
            if (dataTable == null) throw new ArgumentNullException(nameof(dataTable));

            int rowCount = dataTable.Rows.Count;
            int colCount = dataTable.Columns.Count;
            var df = new DataFrame();

            for (int c = 0; c < colCount; c++)
            {
                var dtCol = dataTable.Columns[c];
                var col = CreateEmptyTypedColumn(dtCol.ColumnName, dtCol.DataType, rowCount);

                for (int r = 0; r < rowCount; r++)
                {
                    var val = dataTable.Rows[r][c];
                    if (val == DBNull.Value || val == null)
                    {
                        col.SetNull(r);
                    }
                    else
                    {
                        col.SetValue(r, val);
                    }
                }

                df.AddColumn(col);
            }

            return df;
        }

        /// <summary>
        /// Exports this DataFrame into a legacy System.Data.DataTable for binding to standard WinForms/WPF controls.
        /// </summary>
        public DataTable ToDataTable(string tableName = "ZeroData")
        {
            var dt = new DataTable(tableName);
            for (int c = 0; c < _columnOrder.Count; c++)
            {
                var col = _columns[_columnOrder[c]];
                Type t = col.DataType;
                if (col.HasNulls && t.IsValueType)
                {
                    t = Nullable.GetUnderlyingType(t) ?? typeof(object);
                }
                dt.Columns.Add(col.Name, t);
            }

            for (int r = 0; r < _rowCount; r++)
            {
                var row = dt.NewRow();
                for (int c = 0; c < _columnOrder.Count; c++)
                {
                    var col = _columns[_columnOrder[c]];
                    row[c] = col.IsNull(r) ? DBNull.Value : (col.GetValue(r) ?? DBNull.Value);
                }
                dt.Rows.Add(row);
            }

            return dt;
        }

        private static IDataColumn CreateEmptyTypedColumn(string name, Type type, int count)
        {
            if (type == typeof(int) || type == typeof(short) || type == typeof(byte)) return new DataColumn<int>(name, count);
            if (type == typeof(long)) return new DataColumn<long>(name, count);
            if (type == typeof(decimal)) return new DataColumn<decimal>(name, count);
            if (type == typeof(double)) return new DataColumn<double>(name, count);
            if (type == typeof(float)) return new DataColumn<float>(name, count);
            if (type == typeof(bool)) return new DataColumn<bool>(name, count);
            if (type == typeof(DateTime)) return new DataColumn<DateTime>(name, count);
            if (type == typeof(Guid)) return new DataColumn<Guid>(name, count);

            return new DataColumn<string>(name, count);
        }

        #region Zero-Boxing Typed Column Appenders

        private interface IColumnAppender
        {
            void AppendFromReader(IDataReader reader, int ordinal);
            IDataColumn Build();
        }

        private static IColumnAppender CreateAppender(string name, Type type)
        {
            if (type == typeof(int)) return new IntColumnAppender(name);
            if (type == typeof(long)) return new LongColumnAppender(name);
            if (type == typeof(double)) return new DoubleColumnAppender(name);
            if (type == typeof(float)) return new FloatColumnAppender(name);
            if (type == typeof(decimal)) return new DecimalColumnAppender(name);
            if (type == typeof(bool)) return new BoolColumnAppender(name);
            if (type == typeof(DateTime)) return new DateTimeColumnAppender(name);
            if (type == typeof(Guid)) return new GuidColumnAppender(name);
            if (type == typeof(string)) return new StringColumnAppender(name);

            return new GenericColumnAppender(name, type);
        }

        private abstract class BaseColumnAppender<T> : IColumnAppender
        {
            protected readonly string _name;
            protected T[] _data;
            protected int _count;
            protected byte[]? _nullBitmap;
            protected bool _hasNulls;

            protected BaseColumnAppender(string name, int initialCapacity = 256)
            {
                _name = name;
                _data = new T[initialCapacity];
                _count = 0;
                _hasNulls = false;
            }

            public abstract void AppendFromReader(IDataReader reader, int ordinal);

            protected void EnsureCapacity()
            {
                if (_count >= _data.Length)
                {
                    int newCap = _data.Length == 0 ? 256 : _data.Length * 2;
                    Array.Resize(ref _data, newCap);

                    if (_nullBitmap != null)
                    {
                        int newBytes = (newCap + 7) >> 3;
                        var oldBitmap = _nullBitmap;
                        _nullBitmap = new byte[newBytes];
                        Array.Copy(oldBitmap, _nullBitmap, oldBitmap.Length);
                        for (int b = oldBitmap.Length; b < newBytes; b++)
                            _nullBitmap[b] = 0xFF;
                    }
                }
            }

            protected void MarkNull()
            {
                EnsureCapacity();
                EnsureNullBitmap();
                _nullBitmap![_count >> 3] &= (byte)~(1 << (_count & 7));
                _data[_count] = default!;
                _hasNulls = true;
                _count++;
            }

            protected void AppendValid(T value)
            {
                EnsureCapacity();
                _data[_count] = value;
                if (_nullBitmap != null)
                {
                    _nullBitmap[_count >> 3] |= (byte)(1 << (_count & 7));
                }
                _count++;
            }

            private void EnsureNullBitmap()
            {
                if (_nullBitmap == null)
                {
                    int bytes = (_data.Length + 7) >> 3;
                    _nullBitmap = new byte[bytes];
                    for (int i = 0; i < bytes; i++) _nullBitmap[i] = 0xFF;
                }
            }

            public IDataColumn Build()
            {
                if (_count != _data.Length)
                {
                    Array.Resize(ref _data, _count);
                }
                return new DataColumn<T>(_name, _data, _nullBitmap, _hasNulls);
            }
        }

        private sealed class IntColumnAppender : BaseColumnAppender<int>
        {
            private bool _useFallback;
            public IntColumnAppender(string name) : base(name) { }
            public override void AppendFromReader(IDataReader reader, int ordinal)
            {
                if (reader.IsDBNull(ordinal)) { MarkNull(); return; }
                if (!_useFallback)
                {
                    try { AppendValid(reader.GetInt32(ordinal)); return; }
                    catch { _useFallback = true; }
                }
                AppendValid(FastConvert.AsInt(reader.GetValue(ordinal), 0));
            }
        }

        private sealed class LongColumnAppender : BaseColumnAppender<long>
        {
            private bool _useFallback;
            public LongColumnAppender(string name) : base(name) { }
            public override void AppendFromReader(IDataReader reader, int ordinal)
            {
                if (reader.IsDBNull(ordinal)) { MarkNull(); return; }
                if (!_useFallback)
                {
                    try { AppendValid(reader.GetInt64(ordinal)); return; }
                    catch { _useFallback = true; }
                }
                AppendValid(FastConvert.AsLong(reader.GetValue(ordinal), 0L));
            }
        }

        private sealed class DoubleColumnAppender : BaseColumnAppender<double>
        {
            private bool _useFallback;
            public DoubleColumnAppender(string name) : base(name) { }
            public override void AppendFromReader(IDataReader reader, int ordinal)
            {
                if (reader.IsDBNull(ordinal)) { MarkNull(); return; }
                if (!_useFallback)
                {
                    try { AppendValid(reader.GetDouble(ordinal)); return; }
                    catch { _useFallback = true; }
                }
                AppendValid(FastConvert.AsDouble(reader.GetValue(ordinal), 0.0));
            }
        }

        private sealed class FloatColumnAppender : BaseColumnAppender<float>
        {
            private bool _useFallback;
            public FloatColumnAppender(string name) : base(name) { }
            public override void AppendFromReader(IDataReader reader, int ordinal)
            {
                if (reader.IsDBNull(ordinal)) { MarkNull(); return; }
                if (!_useFallback)
                {
                    try { AppendValid(reader.GetFloat(ordinal)); return; }
                    catch { _useFallback = true; }
                }
                AppendValid((float)FastConvert.AsDouble(reader.GetValue(ordinal), 0.0));
            }
        }

        private sealed class DecimalColumnAppender : BaseColumnAppender<decimal>
        {
            private bool _useFallback;
            public DecimalColumnAppender(string name) : base(name) { }
            public override void AppendFromReader(IDataReader reader, int ordinal)
            {
                if (reader.IsDBNull(ordinal)) { MarkNull(); return; }
                if (!_useFallback)
                {
                    try { AppendValid(reader.GetDecimal(ordinal)); return; }
                    catch { _useFallback = true; }
                }
                AppendValid(FastConvert.AsDecimal(reader.GetValue(ordinal), 0m));
            }
        }

        private sealed class BoolColumnAppender : BaseColumnAppender<bool>
        {
            private bool _useFallback;
            public BoolColumnAppender(string name) : base(name) { }
            public override void AppendFromReader(IDataReader reader, int ordinal)
            {
                if (reader.IsDBNull(ordinal)) { MarkNull(); return; }
                if (!_useFallback)
                {
                    try { AppendValid(reader.GetBoolean(ordinal)); return; }
                    catch { _useFallback = true; }
                }
                AppendValid(FastConvert.AsBool(reader.GetValue(ordinal), false));
            }
        }

        private sealed class DateTimeColumnAppender : BaseColumnAppender<DateTime>
        {
            private bool _useFallback;
            public DateTimeColumnAppender(string name) : base(name) { }
            public override void AppendFromReader(IDataReader reader, int ordinal)
            {
                if (reader.IsDBNull(ordinal)) { MarkNull(); return; }
                if (!_useFallback)
                {
                    try { AppendValid(reader.GetDateTime(ordinal)); return; }
                    catch { _useFallback = true; }
                }
                AppendValid(FastConvert.AsDateTime(reader.GetValue(ordinal), default));
            }
        }

        private sealed class GuidColumnAppender : BaseColumnAppender<Guid>
        {
            private bool _useFallback;
            public GuidColumnAppender(string name) : base(name) { }
            public override void AppendFromReader(IDataReader reader, int ordinal)
            {
                if (reader.IsDBNull(ordinal)) { MarkNull(); return; }
                if (!_useFallback)
                {
                    try { AppendValid(reader.GetGuid(ordinal)); return; }
                    catch { _useFallback = true; }
                }
                AppendValid(FastConvert.AsGuid(reader.GetValue(ordinal), Guid.Empty));
            }
        }

        private sealed class StringColumnAppender : BaseColumnAppender<string>
        {
            private bool _useFallback;
            public StringColumnAppender(string name) : base(name) { }
            public override void AppendFromReader(IDataReader reader, int ordinal)
            {
                if (reader.IsDBNull(ordinal)) { MarkNull(); return; }
                if (!_useFallback)
                {
                    try { AppendValid(reader.GetString(ordinal)); return; }
                    catch { _useFallback = true; }
                }
                AppendValid(reader.GetValue(ordinal)?.ToString() ?? string.Empty);
            }
        }

        private sealed class GenericColumnAppender : IColumnAppender
        {
            private readonly string _name;
            private readonly Type _type;
            private readonly List<object?> _values = new List<object?>();

            public GenericColumnAppender(string name, Type type)
            {
                _name = name;
                _type = type;
            }

            public void AppendFromReader(IDataReader reader, int ordinal)
            {
                _values.Add(reader.IsDBNull(ordinal) ? null : reader.GetValue(ordinal));
            }

            public IDataColumn Build()
            {
                int count = _values.Count;
                var col = CreateEmptyTypedColumn(_name, _type, count);
                for (int i = 0; i < count; i++)
                {
                    var v = _values[i];
                    if (v == null || v == DBNull.Value) col.SetNull(i);
                    else col.SetValue(i, v);
                }
                return col;
            }
        }

        #endregion
    }
}
