using System;
using System.Collections.Generic;
using ZeroData.Core;
using ZeroIoT.OpcUa;
using ZeroIoT.SparkplugB;

namespace ZeroIoT.Bridges
{
    /// <summary>
    /// Converts IoT metrics batches from OPC-UA or Sparkplug B into columnar ZeroData DataFrames.
    /// </summary>
    public static class IotDataFrameBridge
    {
        /// <summary>
        /// Converts a SparkplugPayload into a columnar DataFrame.
        /// </summary>
        public static DataFrame ToDataFrame(SparkplugPayload payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            int count = payload.Metrics.Count;
            var timestamps = new DateTime[count];
            var tagNames = new string[count];
            var dataTypes = new string[count];
            var values = new double[count];

            DateTime defaultDt = DateTimeOffset.FromUnixTimeMilliseconds((long)payload.Timestamp).UtcDateTime;

            for (int i = 0; i < count; i++)
            {
                var m = payload.Metrics[i];
                tagNames[i] = m.Name;
                dataTypes[i] = m.DataType.ToString();
                values[i] = m.AsDouble();
                timestamps[i] = m.Timestamp > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds((long)m.Timestamp).UtcDateTime
                    : defaultDt;
            }

            return new DataFrame(
                new DataColumn<DateTime>("Timestamp", timestamps),
                new DataColumn<string>("TagName", tagNames),
                new DataColumn<string>("DataType", dataTypes),
                new DataColumn<double>("Value", values)
            );
        }

        /// <summary>
        /// Converts a collection of OPC-UA tag readings into a columnar DataFrame.
        /// </summary>
        public static DataFrame ToDataFrame(IEnumerable<(NodeId NodeId, UaDataValue DataValue)> readings)
        {
            if (readings == null) throw new ArgumentNullException(nameof(readings));

            var timestamps = new List<DateTime>();
            var nodeIds = new List<string>();
            var values = new List<double>();
            var statusCodes = new List<uint>();

            foreach (var (nodeId, dataValue) in readings)
            {
                nodeIds.Add(nodeId.ToString());
                timestamps.Add(dataValue.SourceTimestamp ?? DateTime.UtcNow);
                values.Add(dataValue.Value.ToDouble());
                statusCodes.Add(dataValue.StatusCode);
            }

            return new DataFrame(
                new DataColumn<DateTime>("Timestamp", timestamps.ToArray()),
                new DataColumn<string>("NodeId", nodeIds.ToArray()),
                new DataColumn<double>("Value", values.ToArray()),
                new DataColumn<uint>("StatusCode", statusCodes.ToArray())
            );
        }
    }
}
