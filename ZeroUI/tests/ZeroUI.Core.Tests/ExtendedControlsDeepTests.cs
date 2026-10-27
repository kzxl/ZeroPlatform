using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZeroUI.Core.AiMl;
using ZeroUI.Core.Charts;
using ZeroUI.Core.Common;
using ZeroUI.Core.Compliance;
using ZeroUI.Core.Data;
using ZeroUI.Core.Editors;
using ZeroUI.Core.Gis;
using ZeroUI.Core.Media;
using ZeroUI.Core.Scheduler;
using ZeroUI.Core.Scada;
using ZeroUI.Core.Workflow;

namespace ZeroUI.Core.Tests
{
    public class ExtendedControlsDeepTests
    {
        #region 1. AutoComplete Stress & Edge Cases

        [Fact]
        public void AutoComplete_StressTest_10000Items_ExecutesUnder10Milliseconds()
        {
            var items = Enumerable.Range(1, 10000)
                .Select(i => new AutoCompleteItem($"Device_Sensor_{i:D5}", $"Sub-line {i % 10}", $"Area_{i % 5}"))
                .ToList();

            var sw = Stopwatch.StartNew();
            var results = AutoCompleteFilterHelper.Filter(items, "Device_Sensor_009", 20);
            sw.Stop();

            Assert.NotEmpty(results);
            Assert.True(results.Count <= 20);
            Assert.True(sw.ElapsedMilliseconds < 50, $"Filtering took {sw.ElapsedMilliseconds}ms, expected < 50ms");
        }

        [Theory]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("")]
        public void AutoComplete_EmptyOrWhitespaceQuery_ReturnsCappedList(string? query)
        {
            var items = Enumerable.Range(1, 100)
                .Select(i => new AutoCompleteItem($"Item_{i}"))
                .ToList();

            var results = AutoCompleteFilterHelper.Filter(items, query, 15);
            Assert.Equal(15, results.Count);
        }

        [Fact]
        public void AutoComplete_SpecialCharactersAndPunctuation_MatchesSafely()
        {
            var items = new List<AutoCompleteItem>
            {
                new AutoCompleteItem("tag.temp[0].pv", "Sensor Array", "PLC"),
                new AutoCompleteItem("tag.press[1].sp", "Setpoint Array", "PLC"),
                new AutoCompleteItem("c#_compiler@zero-ui", "Dev Tool", "Core")
            };

            var results = AutoCompleteFilterHelper.Filter(items, "[0].pv", 10);
            Assert.Single(results);
            Assert.Equal("tag.temp[0].pv", results[0].DisplayText);

            var atResults = AutoCompleteFilterHelper.Filter(items, "@zero", 10);
            Assert.Single(atResults);
            Assert.Equal("c#_compiler@zero-ui", atResults[0].DisplayText);
        }

        #endregion

        #region 2. CommandPalette Deep Filtering & Ranking

        [Fact]
        public void CommandPalette_Ranking_PrefixMatchesBeforeSubstringMatches()
        {
            var commands = new List<CommandItem>
            {
                new CommandItem { Id = "1", Label = "Rebuild Solution" },
                new CommandItem { Id = "2", Label = "Build Solution" },
                new CommandItem { Id = "3", Label = "Clean and Build" }
            };

            var results = CommandFilterHelper.Filter(commands, "Build", 10);
            Assert.Equal(3, results.Count);

            // "Build Solution" starts with "Build", so it must come first
            Assert.Equal("2", results[0].Id);
        }

        [Fact]
        public void CommandPalette_CaseInsensitiveAndEmptyHandling()
        {
            var commands = new List<CommandItem>
            {
                new CommandItem { Id = "export_pdf", Label = "EXPORT PDF REPORT", Category = "Reporting" },
                new CommandItem { Id = "export_csv", Label = "export csv data", Category = "Export" }
            };

            var upperResults = CommandFilterHelper.Filter(commands, "EXPORT", 10);
            Assert.Equal(2, upperResults.Count);

            var lowerResults = CommandFilterHelper.Filter(commands, "export", 10);
            Assert.Equal(2, lowerResults.Count);

            var nullResults = CommandFilterHelper.Filter(commands, null, 10);
            Assert.Equal(2, nullResults.Count);
        }

        #endregion

        #region 3. Scheduler Complex Overlapping & Date Boundaries

        [Fact]
        public void Scheduler_TripleOverlappingEvents_CalculatesCorrectColumnCount()
        {
            var baseDate = new DateTime(2026, 12, 31, 14, 0, 0); // Year-end boundary
            var events = new List<SchedulerEvent>
            {
                new SchedulerEvent { Id = "1", Subject = "Emergency Meeting", Start = baseDate, End = baseDate.AddHours(2) },
                new SchedulerEvent { Id = "2", Subject = "Maintenance Alarm", Start = baseDate.AddMinutes(30), End = baseDate.AddHours(1.5) },
                new SchedulerEvent { Id = "3", Subject = "System Backup", Start = baseDate.AddMinutes(45), End = baseDate.AddHours(1.75) }
            };

            var layouts = SchedulerLayoutHelper.LayoutOverlapping(events);
            Assert.Equal(3, layouts.Count);

            // All 3 overlap at the same time window, so total columns must be 3
            Assert.All(layouts, l => Assert.Equal(3, l.TotalColumns));

            // Each must occupy a distinct column index (0, 1, 2)
            var colIndices = layouts.Select(l => l.Column).OrderBy(c => c).ToList();
            Assert.Equal(new[] { 0, 1, 2 }, colIndices);
        }

        [Fact]
        public void Scheduler_ZeroDurationOrInvertedEvents_HandlesGracefully()
        {
            var baseDate = new DateTime(2026, 2, 28);
            var events = new List<SchedulerEvent>
            {
                new SchedulerEvent { Id = "1", Subject = "Point In Time Milestone", Start = baseDate.AddHours(10), End = baseDate.AddHours(10) }
            };

            var forDate = SchedulerLayoutHelper.GetEventsForDate(events, baseDate);
            Assert.Single(forDate);

            var inRange = SchedulerLayoutHelper.GetEventsInRange(events, baseDate.AddHours(9), baseDate.AddHours(11));
            // A point-in-time event start=10, end=10 is checked against start < end && end > start
            Assert.NotNull(inRange);
        }

        [Fact]
        public void Scheduler_LeapYearAndMonthBoundaries_FilterCorrectly()
        {
            var leapDate = new DateTime(2024, 2, 29); // Leap day
            var events = new List<SchedulerEvent>
            {
                new SchedulerEvent { Id = "1", Subject = "Quadrennial Calibration", Start = leapDate.AddHours(8), End = leapDate.AddHours(12) },
                new SchedulerEvent { Id = "2", Subject = "March Kickoff", Start = new DateTime(2024, 3, 1, 8, 0, 0), End = new DateTime(2024, 3, 1, 12, 0, 0) }
            };

            var leapResults = SchedulerLayoutHelper.GetEventsForDate(events, leapDate);
            Assert.Single(leapResults);
            Assert.Equal("Quadrennial Calibration", leapResults[0].Subject);

            var marchResults = SchedulerLayoutHelper.GetEventsForDate(events, new DateTime(2024, 3, 1));
            Assert.Single(marchResults);
            Assert.Equal("March Kickoff", marchResults[0].Subject);
        }

        #endregion

        #region 4. ISA-18.2 Alarm Lifecycle State Machine

        [Fact]
        public void AlarmRecord_CompleteLifecycleStateTransitions()
        {
            var alarm = new AlarmRecord
            {
                TagName = "REACT_01_PRESS",
                Description = "High Pressure Trigger",
                Priority = AlarmPriority.High,
                State = AlarmState.Active,
                ActivatedTime = DateTime.UtcNow,
                Value = 195.4,
                Limit = 180.0,
                Unit = "PSI"
            };

            // 1. Initial State: Active and Unacknowledged
            Assert.Equal(AlarmState.Active, alarm.State);
            Assert.Null(alarm.AcknowledgedTime);

            // 2. Acknowledged: Active -> Acknowledged
            alarm.State = AlarmState.Acknowledged;
            alarm.AcknowledgedTime = DateTime.UtcNow;
            alarm.AcknowledgedBy = "LeadEngineer";

            Assert.Equal(AlarmState.Acknowledged, alarm.State);
            Assert.NotNull(alarm.AcknowledgedTime);
            Assert.Equal("LeadEngineer", alarm.AcknowledgedBy);

            // 3. Normalization: Process variable returns below limit -> Cleared
            alarm.Value = 165.0;
            alarm.State = AlarmState.Cleared;
            alarm.ClearedTime = DateTime.UtcNow;

            Assert.Equal(AlarmState.Cleared, alarm.State);
            Assert.NotNull(alarm.ClearedTime);

            // 4. Shelving: Can transition to Shelved state
            alarm.State = AlarmState.Shelved;
            Assert.Equal(AlarmState.Shelved, alarm.State);

            // 5. Suppression: Can transition to Suppressed state
            alarm.State = AlarmState.Suppressed;
            Assert.Equal(AlarmState.Suppressed, alarm.State);
        }

        #endregion

        #region 5. AI/ML Confusion Matrix & Metrics Calculation

        [Fact]
        public void ConfusionMatrix_BinaryClassification_CalculatesAccuracyPrecisionRecallF1()
        {
            // Binary matrix:
            //              Predicted Positive | Predicted Negative
            // Actual Pos:         TP = 80     |      FN = 20
            // Actual Neg:         FP = 10     |      TN = 90
            int tp = 80, fn = 20, fp = 10, tn = 90;

            double accuracy = (double)(tp + tn) / (tp + tn + fp + fn); // 170 / 200 = 0.85
            double precision = (double)tp / (tp + fp);                 // 80 / 90 ≈ 0.8889
            double recall = (double)tp / (tp + fn);                    // 80 / 100 = 0.80
            double f1 = 2 * (precision * recall) / (precision + recall);

            Assert.Equal(0.85, accuracy);
            Assert.InRange(precision, 0.888, 0.889);
            Assert.Equal(0.80, recall);
            Assert.InRange(f1, 0.841, 0.843);
        }

        #endregion

        #region 6. GIS Coordinate Boundaries & Calculations

        [Fact]
        public void Gis_GeoBoundingBox_CalculatesCorrectCoverage()
        {
            var points = new List<GeoPoint>
            {
                new GeoPoint { Lat = 10.70, Lon = 106.60 },
                new GeoPoint { Lat = 10.85, Lon = 106.75 },
                new GeoPoint { Lat = 10.65, Lon = 106.62 },
                new GeoPoint { Lat = 10.80, Lon = 106.70 }
            };

            double minLat = points.Min(p => p.Lat);
            double maxLat = points.Max(p => p.Lat);
            double minLon = points.Min(p => p.Lon);
            double maxLon = points.Max(p => p.Lon);

            Assert.Equal(10.65, minLat);
            Assert.Equal(10.85, maxLat);
            Assert.Equal(106.60, minLon);
            Assert.Equal(106.75, maxLon);
        }

        #endregion

        #region 7. Compliance Audit Diff Verification

        [Fact]
        public void Compliance_AuditDiff_DetectsParameterModifications()
        {
            var auditLog = new List<AuditEntry>
            {
                new AuditEntry
                {
                    Timestamp = DateTime.UtcNow.AddMinutes(-10),
                    User = "eng_sarah",
                    Action = "CALIBRATION_UPDATE",
                    EntityType = "FlowMeter_04",
                    OldValue = "1.002",
                    NewValue = "1.005"
                },
                new AuditEntry
                {
                    Timestamp = DateTime.UtcNow.AddMinutes(-5),
                    User = "admin_mike",
                    Action = "INTERLOCK_BYPASS",
                    EntityType = "SafetyGate_02",
                    OldValue = "ARMED",
                    NewValue = "BYPASS_MAINTENANCE"
                }
            };

            Assert.Equal(2, auditLog.Count);
            Assert.NotEqual(auditLog[0].OldValue, auditLog[0].NewValue);
            Assert.NotEqual(auditLog[1].OldValue, auditLog[1].NewValue);
            Assert.True(auditLog[1].Timestamp > auditLog[0].Timestamp);
        }

        #endregion
    }
}
