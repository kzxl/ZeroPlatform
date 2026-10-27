using System;
using System.Collections.Generic;

namespace ZeroStorage.Core.TimeSeries
{
    public enum MultiMetricType : byte
    {
        Float64 = 1,
        Int64 = 2,
        Int32 = 3,
        Boolean = 4,
        Decimal = 5,
        String = 6
    }

    public sealed class MultiMetricField
    {
        public string Name { get; }
        public MultiMetricType Type { get; }

        public MultiMetricField(string name, MultiMetricType type)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type;
        }
    }

    public sealed class MultiMetricSchema
    {
        public string SeriesName { get; }
        public IReadOnlyList<MultiMetricField> Fields { get; }

        public MultiMetricSchema(string seriesName, params MultiMetricField[] fields)
            : this(seriesName, (IReadOnlyList<MultiMetricField>)fields)
        {
        }

        public MultiMetricSchema(string seriesName, IReadOnlyList<MultiMetricField> fields)
        {
            SeriesName = seriesName ?? throw new ArgumentNullException(nameof(seriesName));
            Fields = fields ?? throw new ArgumentNullException(nameof(fields));
        }
    }

    public sealed class MultiMetricRow
    {
        public long TimestampMs { get; }
        public object?[] Values { get; }

        public MultiMetricRow(long timestampMs, params object?[] values)
        {
            TimestampMs = timestampMs;
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public T GetValue<T>(int fieldIndex)
        {
            var val = Values[fieldIndex];
            if (val == null) return default!;
            return (T)val;
        }
    }
}
