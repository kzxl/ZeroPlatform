using System;
using System.Drawing;
using Xunit;

namespace ZeroUI.Desktop.Tests
{
    public class NewControlsLifecycleTests
    {
        #region WinForms Controls Lifecycle Tests

        [Fact]
        public void WinForms_ZAutoComplete_InstantiatesAndDisposesCleanly()
        {
            using var ctrl = new ZeroUI.WinForms.Editors.ZAutoComplete();
            Assert.NotNull(ctrl);
            ctrl.PlaceholderText = "Search...";
            Assert.Equal("Search...", ctrl.PlaceholderText);
            ctrl.DebounceMilliseconds = 150;
            Assert.Equal(150, ctrl.DebounceMilliseconds);
            ctrl.Text = "Hello";
            Assert.Equal("Hello", ctrl.Text);
        }

        [Fact]
        public void WinForms_ZNotificationCenter_PushAndClear_WorksCorrectly()
        {
            using var center = new ZeroUI.WinForms.Feedback.ZNotificationCenter();
            Assert.NotNull(center);
            Assert.Equal(0, center.UnreadCount);

            center.Push(new ZeroUI.Core.Editors.ZNotification
            {
                Title = "Test Notice",
                Message = "System operational",
                Severity = ZeroUI.Core.Editors.NotificationSeverity.Info
            });

            Assert.Equal(1, center.UnreadCount);
            center.MarkAllAsRead();
            Assert.Equal(0, center.UnreadCount);
            center.Clear();
            Assert.Empty(center.Notifications);
        }

        [Fact]
        public void WinForms_ZAlarmSummary_AcknowledgeAll_UpdatesRecords()
        {
            using var summary = new ZeroUI.WinForms.Industrial.ZAlarmSummary();
            Assert.NotNull(summary);

            var alarms = new System.Collections.Generic.List<ZeroUI.Core.Scada.AlarmRecord>
            {
                new ZeroUI.Core.Scada.AlarmRecord
                {
                    TagName = "PUMP_01",
                    Description = "Pressure high",
                    Priority = ZeroUI.Core.Scada.AlarmPriority.High,
                    State = ZeroUI.Core.Scada.AlarmState.Active
                }
            };

            summary.SetAlarms(alarms);
            summary.AcknowledgeAll();

            Assert.Equal(ZeroUI.Core.Scada.AlarmState.Acknowledged, alarms[0].State);
            Assert.NotNull(alarms[0].AcknowledgedTime);
        }

        [Fact]
        public void WinForms_ZRichTextEditor_Properties_WorkCorrectly()
        {
            using var editor = new ZeroUI.WinForms.Editors.ZRichTextEditor();
            Assert.NotNull(editor);
            editor.HtmlContent = "<b>Bold text</b>";
            Assert.NotNull(editor.PlainText);
        }

        [Fact]
        public void WinForms_ZScheduler_Navigation_UpdatesDate()
        {
            using var scheduler = new ZeroUI.WinForms.Data.ZScheduler();
            Assert.NotNull(scheduler);

            var initialDate = new DateTime(2026, 9, 25);
            scheduler.CurrentDate = initialDate;
            scheduler.ActiveView = ZeroUI.Core.Editors.SchedulerView.Day;

            scheduler.NavigateNext();
            Assert.Equal(initialDate.AddDays(1), scheduler.CurrentDate);

            scheduler.NavigatePrevious();
            Assert.Equal(initialDate, scheduler.CurrentDate);
        }

        [Fact]
        public void WinForms_NewCharts_InstantiateCleanly()
        {
            using var bubble = new ZeroUI.WinForms.Charts.ZBubbleChart();
            Assert.NotNull(bubble);

            using var gauge = new ZeroUI.WinForms.Charts.ZGaugeChart();
            Assert.NotNull(gauge);

            using var mini = new ZeroUI.WinForms.Charts.ZMiniChart();
            Assert.NotNull(mini);
        }

        [Fact]
        public void WinForms_SpecializedIndustrial_InstantiateCleanly()
        {
            using var bmsFloor = new ZeroUI.WinForms.Industrial.ZFloorPlanViewer();
            Assert.NotNull(bmsFloor);

            using var powerMeter = new ZeroUI.WinForms.Industrial.ZPowerQualityMeter();
            Assert.NotNull(powerMeter);

            using var waterMap = new ZeroUI.WinForms.Industrial.ZScadaMap();
            Assert.NotNull(waterMap);
        }

        #endregion

        #region WPF Controls Lifecycle Tests (STA Thread)

        [Fact]
        public void Wpf_ZAutoComplete_InstantiatesCleanly()
        {
            StaTestRunner.Run(() =>
            {
                var ctrl = new ZeroUI.Wpf.Editors.ZAutoComplete();
                Assert.NotNull(ctrl);
                ctrl.Placeholder = "Search items...";
                Assert.Equal("Search items...", ctrl.Placeholder);
                ctrl.Text = "Query";
                Assert.Equal("Query", ctrl.Text);
            });
        }

        [Fact]
        public void Wpf_ZNotificationCenter_PushAndClear_WorksCorrectly()
        {
            StaTestRunner.Run(() =>
            {
                var center = new ZeroUI.Wpf.Feedback.ZNotificationCenter();
                Assert.NotNull(center);
                Assert.Equal(0, center.UnreadCount);

                center.Push(new ZeroUI.Core.Editors.ZNotification
                {
                    Title = "WPF Notice",
                    Message = "High priority dispatch",
                    Severity = ZeroUI.Core.Editors.NotificationSeverity.Warning
                });

                Assert.Equal(1, center.UnreadCount);
                center.MarkAllAsRead();
                Assert.Equal(0, center.UnreadCount);
            });
        }

        [Fact]
        public void Wpf_ZAlarmSummary_InstantiatesCleanly()
        {
            StaTestRunner.Run(() =>
            {
                var summary = new ZeroUI.Wpf.Industrial.ZAlarmSummary();
                Assert.NotNull(summary);
                summary.MinPriority = ZeroUI.Core.Scada.AlarmPriority.High;
                Assert.Equal(ZeroUI.Core.Scada.AlarmPriority.High, summary.MinPriority);
            });
        }

        [Fact]
        public void Wpf_ZScheduler_InstantiatesAndNavigates()
        {
            StaTestRunner.Run(() =>
            {
                var scheduler = new ZeroUI.Wpf.Data.ZScheduler();
                Assert.NotNull(scheduler);
                var dt = new DateTime(2026, 9, 25);
                scheduler.CurrentDate = dt;
                scheduler.NavigateNext();
                Assert.True(scheduler.CurrentDate > dt);
            });
        }

        [Fact]
        public void Wpf_NewChartsAndIndustrial_InstantiateCleanly()
        {
            StaTestRunner.Run(() =>
            {
                var bubble = new ZeroUI.Wpf.Charts.ZBubbleChart();
                Assert.NotNull(bubble);

                var gauge = new ZeroUI.Wpf.Charts.ZGaugeChart();
                Assert.NotNull(gauge);

                var waterMap = new ZeroUI.Wpf.Industrial.ZScadaMap();
                Assert.NotNull(waterMap);
            });
        }

        #endregion
    }
}
