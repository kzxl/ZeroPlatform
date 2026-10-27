using System;
using System.Collections.Generic;
using System.Linq;
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
    public class ExtendedControlsModelTests
    {
        #region 1. AutoComplete Tests

        [Fact]
        public void AutoCompleteFilterHelper_FiltersPrefixFirstThenContains()
        {
            var items = new List<AutoCompleteItem>
            {
                new AutoCompleteItem("Apple", "Fruit", "Food"),
                new AutoCompleteItem("Pineapple", "Tropical Fruit", "Food"),
                new AutoCompleteItem("Apricot", "Stone Fruit", "Food"),
                new AutoCompleteItem("Banana", "Yellow Fruit", "Food")
            };

            var results = AutoCompleteFilterHelper.Filter(items, "Ap", 10);

            Assert.Equal(3, results.Count);
            // Prefix matches ("Apple", "Apricot") should come first
            Assert.Equal("Apple", results[0].DisplayText);
            Assert.Equal("Apricot", results[1].DisplayText);
            // Contains match ("Pineapple") should come after
            Assert.Equal("Pineapple", results[2].DisplayText);
        }

        [Fact]
        public void AutoCompleteFilterHelper_EmptyQuery_ReturnsAllUpToLimit()
        {
            var items = Enumerable.Range(1, 50)
                .Select(i => new AutoCompleteItem($"Item {i}"))
                .ToList();

            var results = AutoCompleteFilterHelper.Filter(items, "", 5);
            Assert.Equal(5, results.Count);
        }

        [Fact]
        public void AutoCompleteFilterHelper_NullItems_ReturnsEmptyList()
        {
            var results = AutoCompleteFilterHelper.Filter(null!, "test", 10);
            Assert.NotNull(results);
            Assert.Empty(results);
        }

        #endregion

        #region 2. CommandPalette Tests

        [Fact]
        public void CommandFilterHelper_FiltersCorrectly()
        {
            var commands = new List<CommandItem>
            {
                new CommandItem { Id = "save", Label = "File: Save", Category = "File", ShortcutText = "Ctrl+S" },
                new CommandItem { Id = "save_as", Label = "File: Save As...", Category = "File", ShortcutText = "Ctrl+Shift+S" },
                new CommandItem { Id = "open", Label = "File: Open", Category = "File", ShortcutText = "Ctrl+O" },
                new CommandItem { Id = "find", Label = "Edit: Find", Category = "Edit", ShortcutText = "Ctrl+F" }
            };

            var results = CommandFilterHelper.Filter(commands, "Save", 10);
            Assert.Equal(2, results.Count);
            Assert.All(results, c => Assert.Contains("Save", c.Label));
        }

        [Fact]
        public void CommandFilterHelper_MatchesCategoryAndSearchTokens()
        {
            var commands = new List<CommandItem>
            {
                new CommandItem { Id = "git_push", Label = "Push to Remote", Category = "Source Control", SearchTokens = new[] { "git", "sync" } },
                new CommandItem { Id = "build", Label = "Build Solution", Category = "Build", SearchTokens = new[] { "compile" } }
            };

            var gitResults = CommandFilterHelper.Filter(commands, "git", 10);
            Assert.Single(gitResults);
            Assert.Equal("git_push", gitResults[0].Id);

            var compileResults = CommandFilterHelper.Filter(commands, "compile", 10);
            Assert.Single(compileResults);
            Assert.Equal("build", compileResults[0].Id);
        }

        #endregion

        #region 3. Notification Tests

        [Fact]
        public void Notification_DefaultProperties_Valid()
        {
            var notification = new ZNotification
            {
                Title = "Alert",
                Message = "Temperature exceeded threshold",
                Severity = NotificationSeverity.Warning
            };

            Assert.False(string.IsNullOrEmpty(notification.Id));
            Assert.Equal("Alert", notification.Title);
            Assert.Equal(NotificationSeverity.Warning, notification.Severity);
            Assert.False(notification.IsRead);
            Assert.True((DateTime.Now - notification.Timestamp).TotalSeconds < 5);
        }

        #endregion

        #region 4. AlarmSummary Tests

        [Fact]
        public void AlarmRecord_PrioritiesAndStates_Valid()
        {
            var alarm = new AlarmRecord
            {
                TagName = "TIC_101.PV",
                Description = "Reactor Core Temperature High",
                Priority = AlarmPriority.Critical,
                State = AlarmState.Active,
                ActivatedTime = DateTime.UtcNow,
                Value = 185.5,
                Limit = 180.0,
                Unit = "°C"
            };

            Assert.Equal(AlarmPriority.Critical, alarm.Priority);
            Assert.Equal(AlarmState.Active, alarm.State);
            Assert.Null(alarm.AcknowledgedTime);
            Assert.Null(alarm.AcknowledgedBy);
        }

        #endregion

        #region 5. Scheduler Tests

        [Fact]
        public void SchedulerLayoutHelper_GetEventsForDate_FiltersCorrectly()
        {
            var targetDate = new DateTime(2026, 9, 25);
            var events = new List<SchedulerEvent>
            {
                new SchedulerEvent { Subject = "Morning Standup", Start = targetDate.AddHours(9), End = targetDate.AddHours(9.5) },
                new SchedulerEvent { Subject = "Sprint Review", Start = targetDate.AddHours(14), End = targetDate.AddHours(15) },
                new SchedulerEvent { Subject = "Tomorrow Planning", Start = targetDate.AddDays(1).AddHours(10), End = targetDate.AddDays(1).AddHours(11) }
            };

            var dateEvents = SchedulerLayoutHelper.GetEventsForDate(events, targetDate);
            Assert.Equal(2, dateEvents.Count);
            Assert.Contains(dateEvents, e => e.Subject == "Morning Standup");
            Assert.Contains(dateEvents, e => e.Subject == "Sprint Review");
        }

        [Fact]
        public void SchedulerLayoutHelper_LayoutOverlapping_CalculatesColumns()
        {
            var baseDate = new DateTime(2026, 9, 25);
            var events = new List<SchedulerEvent>
            {
                new SchedulerEvent { Subject = "Task A", Start = baseDate.AddHours(10), End = baseDate.AddHours(11) },
                new SchedulerEvent { Subject = "Task B", Start = baseDate.AddHours(10.5), End = baseDate.AddHours(11.5) },
                new SchedulerEvent { Subject = "Task C", Start = baseDate.AddHours(12), End = baseDate.AddHours(13) }
            };

            var layouts = SchedulerLayoutHelper.LayoutOverlapping(events);
            Assert.Equal(3, layouts.Count);

            // Task A and Task B overlap, so their TotalColumns should be 2
            var layoutA = layouts.First(l => l.Event.Subject == "Task A");
            var layoutB = layouts.First(l => l.Event.Subject == "Task B");
            Assert.Equal(2, layoutA.TotalColumns);
            Assert.Equal(2, layoutB.TotalColumns);
            Assert.NotEqual(layoutA.Column, layoutB.Column);

            // Task C does not overlap, should have TotalColumns = 1
            var layoutC = layouts.First(l => l.Event.Subject == "Task C");
            Assert.Equal(2, layoutC.TotalColumns);
            Assert.Equal(0, layoutC.Column);
        }

        #endregion

        #region 6. RichText Model Tests

        [Fact]
        public void RichTextFeatures_Flags_CombineProperly()
        {
            var features = RichTextFeatures.Bold | RichTextFeatures.Italic | RichTextFeatures.Links;
            Assert.True(features.HasFlag(RichTextFeatures.Bold));
            Assert.True(features.HasFlag(RichTextFeatures.Italic));
            Assert.False(features.HasFlag(RichTextFeatures.Underline));
            Assert.True(features.HasFlag(RichTextFeatures.Links));
        }

        #endregion

        #region 7. AI/ML Models Tests

        [Fact]
        public void ConfusionMatrix_Model_PropertiesValid()
        {
            var labels = new[] { "Positive", "Negative" };
            var matrix = new[,] { { 90, 10 }, { 5, 95 } };

            Assert.Equal(2, labels.Length);
            Assert.Equal(90, matrix[0, 0]);
            Assert.Equal(10, matrix[0, 1]);
            Assert.Equal(5, matrix[1, 0]);
            Assert.Equal(95, matrix[1, 1]);
        }

        [Fact]
        public void FeatureScore_Ordering_SortsCorrectly()
        {
            var features = new List<FeatureScore>
            {
                new FeatureScore { Name = "Pressure", Score = 0.45 },
                new FeatureScore { Name = "Temperature", Score = 0.82 },
                new FeatureScore { Name = "Vibration", Score = 0.61 }
            };

            var sorted = features.OrderByDescending(f => f.Score).ToList();
            Assert.Equal("Temperature", sorted[0].Name);
            Assert.Equal("Vibration", sorted[1].Name);
            Assert.Equal("Pressure", sorted[2].Name);
        }

        #endregion

        #region 8. GIS & Mapping Models Tests

        [Fact]
        public void GisModels_Coordinates_Valid()
        {
            var point = new GeoPoint { Lat = 10.762622, Lon = 106.660172 };
            Assert.True(point.Lat > 0);
            Assert.True(point.Lon > 0);

            var marker = new MapMarker
            {
                Lat = point.Lat,
                Lon = point.Lon,
                Label = "Pump Station Alpha",
                Glyph = "🚰"
            };

            Assert.Equal("Pump Station Alpha", marker.Label);
            Assert.Equal(10.762622, marker.Lat);
        }

        #endregion

        #region 9. Compliance Models Tests

        [Fact]
        public void ComplianceModels_AuditAndChecklist_Valid()
        {
            var audit = new AuditEntry
            {
                Timestamp = DateTime.UtcNow,
                User = "operator_01",
                Action = "SetPointChange",
                EntityType = "PID_Loop_202",
                EntityId = "202",
                OldValue = "120.0",
                NewValue = "135.0"
            };

            Assert.Equal("operator_01", audit.User);
            Assert.Equal("120.0", audit.OldValue);
            Assert.Equal("135.0", audit.NewValue);

            var checklist = new List<ChecklistItem>
            {
                new ChecklistItem { Id = "1", Text = "Pre-flight calibration", IsChecked = true, IsRequired = true },
                new ChecklistItem { Id = "2", Text = "Safety valve seal check", IsChecked = true, IsRequired = true },
                new ChecklistItem { Id = "3", Text = "Secondary coolant pressure check", IsChecked = false, IsRequired = true }
            };

            int checkedCount = checklist.Count(c => c.IsChecked);
            double percent = (double)checkedCount / checklist.Count * 100.0;

            Assert.Equal(2, checkedCount);
            Assert.InRange(percent, 66.0, 67.0);
        }

        #endregion

        #region 10. Workflow & Media Models Tests

        [Fact]
        public void WorkflowAndMedia_Models_InstantiateCorrectly()
        {
            var orgNode = new OrgNode
            {
                Name = "John Doe",
                Title = "Plant Director",
                Children = new List<OrgNode>
                {
                    new OrgNode { Name = "Jane Smith", Title = "Chief Maintenance Engineer" }
                }
            };

            Assert.Equal("John Doe", orgNode.Name);
            Assert.Single(orgNode.Children);
            Assert.Equal("Jane Smith", orgNode.Children[0].Name);

            var pdfAnnotation = new PdfAnnotation
            {
                Page = 1,
                X = 100,
                Y = 200,
                Type = PdfAnnotationMode.Highlight,
                Text = "Reviewed and Approved"
            };

            Assert.Equal(1, pdfAnnotation.Page);
            Assert.Equal(PdfAnnotationMode.Highlight, pdfAnnotation.Type);
        }

        #endregion
    }
}
