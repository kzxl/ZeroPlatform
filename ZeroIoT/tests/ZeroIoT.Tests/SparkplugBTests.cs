using System;
using Xunit;
using ZeroIoT.SparkplugB;

namespace ZeroIoT.Tests
{
    public class SparkplugBTests
    {
        [Fact]
        public void FormatTopic_NodeAndDevice_FollowsSpec()
        {
            string nodeTopic = SparkplugCodec.FormatTopic("AssemblyLine1", "NDATA", "EdgeNode_42");
            Assert.Equal("spBv1.0/AssemblyLine1/NDATA/EdgeNode_42", nodeTopic);

            string devTopic = SparkplugCodec.FormatTopic("AssemblyLine1", "DDATA", "EdgeNode_42", "RobotArm_01");
            Assert.Equal("spBv1.0/AssemblyLine1/DDATA/EdgeNode_42/RobotArm_01", devTopic);
        }

        [Fact]
        public void Payload_ProtobufEncodingAndDecoding_PreservesAllMetrics()
        {
            ulong sampleTs = 1726000000000UL;
            var payload = new SparkplugPayload(seq: 15, timestamp: sampleTs);

            payload.AddMetric(SparkplugMetric.CreateDouble("Furnace_Temp", 1250.75));
            payload.AddMetric(SparkplugMetric.CreateFloat("Chamber_Pressure", 101.325f));
            payload.AddMetric(SparkplugMetric.CreateInt32("Parts_Inspected", 45000));
            payload.AddMetric(SparkplugMetric.CreateInt64("Total_Runtime_Sec", 864000L));
            payload.AddMetric(SparkplugMetric.CreateBoolean("SafetyInterlock_Ok", true));
            payload.AddMetric(SparkplugMetric.CreateString("Current_Recipe", "RECIPE_ALUMINUM_A6061"));

            byte[] encodedBytes = SparkplugCodec.Encode(payload);
            Assert.NotNull(encodedBytes);
            Assert.True(encodedBytes.Length > 0);

            var decodedPayload = SparkplugCodec.Decode(encodedBytes);
            Assert.Equal(15UL, decodedPayload.SequenceNumber);
            Assert.Equal(sampleTs, decodedPayload.Timestamp);
            Assert.Equal(6, decodedPayload.Metrics.Count);

            var mTemp = decodedPayload.GetMetric("Furnace_Temp");
            Assert.NotNull(mTemp);
            Assert.Equal(SparkplugDataType.Double, mTemp.DataType);
            Assert.Equal(1250.75, (double)mTemp.Value!, 2);

            var mPress = decodedPayload.GetMetric("Chamber_Pressure");
            Assert.NotNull(mPress);
            Assert.Equal(SparkplugDataType.Float, mPress.DataType);
            Assert.Equal(101.325f, (float)mPress.Value!, 2);

            var mParts = decodedPayload.GetMetric("Parts_Inspected");
            Assert.NotNull(mParts);
            Assert.Equal(SparkplugDataType.Int32, mParts.DataType);
            Assert.Equal(45000, (int)mParts.Value!);

            var mRuntime = decodedPayload.GetMetric("Total_Runtime_Sec");
            Assert.NotNull(mRuntime);
            Assert.Equal(SparkplugDataType.Int64, mRuntime.DataType);
            Assert.Equal(864000L, (long)mRuntime.Value!);

            var mSafety = decodedPayload.GetMetric("SafetyInterlock_Ok");
            Assert.NotNull(mSafety);
            Assert.Equal(SparkplugDataType.Boolean, mSafety.DataType);
            Assert.True((bool)mSafety.Value!);

            var mRecipe = decodedPayload.GetMetric("Current_Recipe");
            Assert.NotNull(mRecipe);
            Assert.Equal(SparkplugDataType.String, mRecipe.DataType);
            Assert.Equal("RECIPE_ALUMINUM_A6061", mRecipe.Value?.ToString());
        }
    }
}
