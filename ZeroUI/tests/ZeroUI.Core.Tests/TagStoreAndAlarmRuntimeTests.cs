using System;
using System.Threading;
using Xunit;
using ZeroUI.Core.Runtime;
using ZeroUI.Core.Scada;

namespace ZeroUI.Core.Tests
{
    public class TagStoreAndAlarmRuntimeTests
    {
        [Fact]
        public void ZeroTagStore_RegistersTagsAndResolvesIds()
        {
            var store = new ZeroTagStore("TestStore", 128);

            int t1 = store.RegisterTag("Line1/Oven/Zone1_Temp", "°C", "Zone 1 Temperature");
            int t2 = store.RegisterTag("Line1/Oven/Zone2_Temp", "°C", "Zone 2 Temperature");

            Assert.True(t1 > 0);
            Assert.True(t2 > 0);
            Assert.NotEqual(t1, t2);

            Assert.True(store.TryGetTagId("Line1/Oven/Zone1_Temp", out int foundId));
            Assert.Equal(t1, foundId);

            var meta = store.GetMetadata(t1);
            Assert.NotNull(meta);
            Assert.Equal("°C", meta!.Unit);
            Assert.Equal("Zone1_Temp", meta.Name);
        }

        [Fact]
        public void ZeroTagStore_SetAndGet_UnboxedValues()
        {
            var store = new ZeroTagStore("TestStore2", 128);
            int tagId = store.RegisterTag("Plant/Tank1/Level", "%");

            store.Set(tagId, 85.5, ScadaQuality.Good);
            Assert.Equal(85.5, store.GetDouble(tagId));
            Assert.Equal(ScadaQuality.Good, store.GetQuality(tagId));

            // Integer accessor
            store.Set(tagId, 100L, ScadaQuality.Good);
            Assert.Equal(100L, store.GetInt64(tagId));

            // Boolean accessor
            store.Set(tagId, true, ScadaQuality.Good);
            Assert.True(store.GetBool(tagId));
        }

        [Fact]
        public void ZeroTagStore_AttachToBus_AutoIngestsUpdates()
        {
            var store = new ZeroTagStore("TestStoreBus", 128);
            var bus = new ZeroTelemetryBus("TestBusIngest");

            int t1 = store.RegisterTag("Sensor/Pressure");
            int t2 = store.RegisterTag("Sensor/FlowRate");

            store.AttachToBus(bus);

            Span<TagUpdate> batch = stackalloc TagUpdate[2];
            batch[0] = new TagUpdate(t1, new ScadaValue(6.8), 2000);
            batch[1] = new TagUpdate(t2, new ScadaValue(145.2), 2000);

            bus.Publish(batch);

            Assert.Equal(6.8, store.GetDouble(t1));
            Assert.Equal(145.2, store.GetDouble(t2));
        }

        [Fact]
        public void ZeroAlarmRuntime_AnalogLimit_TriggersActiveUnacknowledged()
        {
            var alarmRuntime = new ZeroAlarmRuntime("TestAlarms");
            int tagId = 10;

            alarmRuntime.RegisterAnalogLimits(
                prefixId: "OVEN_Z1",
                tagId: tagId,
                tagPath: "Line1/Oven/Z1",
                baseDesc: "Zone 1 Temp",
                high: 200.0,
                highHigh: 250.0,
                deadband: 2.0);

            // Normal value
            alarmRuntime.Evaluate(tagId, new ScadaValue(150.0));
            Assert.Equal(0, alarmRuntime.ActiveAlarmCount);

            // High alarm triggered
            alarmRuntime.Evaluate(tagId, new ScadaValue(205.0));
            Assert.Equal(1, alarmRuntime.ActiveAlarmCount);

            var active = alarmRuntime.GetActiveAlarms();
            Assert.Single(active);
            Assert.Equal(ScadaAlarmState.ActiveUnacknowledged, active[0].State);
            Assert.Equal(ScadaAlarmSeverity.Medium, active[0].Severity);

            // High-High alarm triggered
            alarmRuntime.Evaluate(tagId, new ScadaValue(255.0));
            Assert.Equal(2, alarmRuntime.ActiveAlarmCount);

            var counts = alarmRuntime.GetSeverityCounts();
            Assert.Equal(1, counts.Medium);
            Assert.Equal(1, counts.Critical);
        }

        [Fact]
        public void ZeroAlarmRuntime_Deadband_PreventsChatter()
        {
            var alarmRuntime = new ZeroAlarmRuntime("TestAlarmsDeadband");
            int tagId = 20;

            // High limit 100.0, Deadband 5.0
            alarmRuntime.RegisterRule(new AlarmRuleDefinition(
                "PUMP_TEMP_H", tagId, "Pump/Temp", "Pump Temp High",
                AlarmConditionType.High, ScadaAlarmSeverity.High,
                limitValue: 100.0, deadband: 5.0));

            // Exceeds limit -> Active
            alarmRuntime.Evaluate(tagId, new ScadaValue(101.0));
            Assert.Equal(1, alarmRuntime.ActiveAlarmCount);

            // Drops to 98.0 (below limit 100, but within deadband 95.0..100.0) -> Stays Active!
            alarmRuntime.Evaluate(tagId, new ScadaValue(98.0));
            Assert.Equal(1, alarmRuntime.ActiveAlarmCount);

            // Acknowledge alarm
            Assert.True(alarmRuntime.Acknowledge("PUMP_TEMP_H", "Operator_1"));
            var alarms = alarmRuntime.GetActiveAlarms();
            Assert.Equal(ScadaAlarmState.ActiveAcknowledged, alarms[0].State);

            // Drops to 94.0 (below deadband 95.0) -> Clears completely to Normal!
            alarmRuntime.Evaluate(tagId, new ScadaValue(94.0));
            Assert.Equal(0, alarmRuntime.ActiveAlarmCount);
        }
    }
}
